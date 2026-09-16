using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using System.Linq;

public enum ServerSessionState
{
    None,
    Flowing,
    InstantPaused,
    DecisionWaiting,
    Resolving,
    Finished,
}

public class ServerTimelineManager : MonoBehaviour
{
    // -------------------------------------------------------
    // Config
    // -------------------------------------------------------

    [Header("Refs")]
    [SerializeField] private SyncedBridge bridge;
    [SerializeField] private ServerMatchSession session;
    [SerializeField] private SpeedConfig speedConfig;
    [SerializeField] private SkillLibrary skillLibrary;
    [SerializeField] private ServerSpawnManager spawnManager;



    // Internal coroutine control — not observable, not session data
    private bool waitingForDecision = false;
    private bool lastDecisionResolved = false;
    private (int unitId, Vector3Int target, DecisionType decisionType, int skillCardId)? pendingDecision = null;

    // Transient per-instant records — live and die within one instant
    private List<(int unitId, SkillDefinition skilldef, List<ResolveResult> results)> actionRecord;
    private List<(int unitId, Vector3Int target, int effectId, int skillCardId)> effectRecord;
    // Sync token — control concern, client/server handshake only
    private int Token = 0;

    // -------------------------------------------------------
    // State Machine
    // -------------------------------------------------------

    void TransitionTo(ServerSessionState next)
    {
        Debug.Log($"[Timeline] {session.TimelineState} -> {next} at instant {session.CurrentInstant}");
        session.TimelineState = next;
    }

    // -------------------------------------------------------
    // Pack builders — ONLY way to update LastSnapshot/LastResolve/LastDecision
    // Always update all three together — never partial
    // -------------------------------------------------------

    /// <summary>
    /// Non-resolve broadcast — snapshot is final current state.
    /// </summary>
    public void PackFinal()
    {
        UnitRecalculator.RecalculateAll(session);
        session.LastSnapshot = GetSnapshot();
        session.LastResolve = default;
        session.LastDecision = BuildDecision();
        GetSetNewToken();
    }

    /// <summary>
    /// Resolve broadcast — snapshotBefore captured before effects applied.
    /// </summary>
    void PackResolve(SessionSnapshotData snapshotBefore, ResolveData resolve)
    {
        UnitRecalculator.RecalculateAll(session);
        session.LastSnapshot = snapshotBefore;
        session.LastResolve = resolve;
        session.LastDecision = default;
        GetSetNewToken();
    }

    // -------------------------------------------------------
    // Init
    // -------------------------------------------------------
    public void InitTimeline()
    {
        actionRecord = new List<(int, SkillDefinition, List<ResolveResult>)>();
        effectRecord = new List<(int, Vector3Int, int, int)>();
        List<TeamData> teams = session.GetAllTeamData();
        foreach (var team in teams)
            team.Overtime = (int)session.matchConfig.overtimePerTeam;

        session.FlaggedTeamId = session.GetOtherTeamId(0);

        TransitionTo(ServerSessionState.Flowing);
        LogInstantFlowing(session.CurrentInstant);

        Debug.Log("[Timeline] Initialized and pack ready.");
    }
    public void RunTimeline()
    {
        StartCoroutine(TimelineLoop());
        StartCoroutine(WaitingTimeProcess());
        Debug.Log("[Timeline] Running.");
    }

    IEnumerator WaitingTimeProcess()
    {
        while (true)
        {
            TeamData currentTeam = session.GetCurrentTurnTeamData();
            if (waitingForDecision && currentTeam != null)
            {
                if (currentTeam.WaitDuration > 0)
                    currentTeam.WaitDuration--;
                else
                    currentTeam.Overtime--;
            }
            yield return new WaitForSeconds(1f);
        }
    }

    // -------------------------------------------------------
    // Main Loop
    // -------------------------------------------------------

    IEnumerator TimelineLoop()
    {
        List<TeamData> teams = session.GetAllTeamData();

        while (session.CurrentInstant <= session.matchConfig.maxInstant)
        {
            TransitionTo(ServerSessionState.Flowing);
            session.CurrentTeamTurnId = -1;

            AllRegenSP();
            actionRecord.Clear();
            effectRecord.Clear();
            session.ResetInstantSkillUsage();

            // Tick
            TickAllUnits();
            bool effectsChanged = TickAllEffects();
            bool newUnitsReady = UpdateReadyQueue();
            // Reset per-instant tracking
            foreach (var team in teams)
                team.WaitDuration = (int)session.matchConfig.actWaitWindowPerInstantPerReadyUnit * session.OwnedReadyUnitCount(team.teamId);

            // Broadcast — full pack if something changed, cheap tick otherwise
            if (effectsChanged)
            {
                PackFinal();
                BroadcastSnapshot();
                yield return new WaitForSeconds(2f);
            }
            else
            {
                BroadcastTick();
            }

            bool allReady = session.units.Count > 0 && session.ReadyUnitIds.Count >= session.units.Count;
            if (!newUnitsReady && !allReady)
            {
                session.CurrentInstant++;
                yield return new WaitForSeconds(session.matchConfig.instantDuration);
                continue;
            }

            // Instant paused — run decision loop
            if (!newUnitsReady)
                session.ConsecutivePassInstants++;
            else
                session.ConsecutivePassInstants = 0;

            if (session.ConsecutivePassInstants > 2)
            {
                yield return StartCoroutine(EndMatch());
                yield break;
            }
            TransitionTo(ServerSessionState.InstantPaused);
            LogInstantPaused(session.CurrentInstant);
            yield return RunDecisionLoop();

            // Always broadcast after decision loop — even if no decisions made
            PackFinal();
            BroadcastSnapshot();


            // foreach (var unit in session.units)
            //     Debug.Log($"[PreResolve] Unit {unit.Id} HP={unit.CurrentHP}");

            // End of instant — resolve non-instant effects
            yield return ResolveEffectRecord();

            // Apply step costs + final broadcast
            ApplyStepCosts();
            PackFinal();
            BroadcastSnapshot();

            session.CurrentInstant++;
            yield return new WaitForSeconds(session.matchConfig.instantDuration);
        }

        // Match end
        TransitionTo(ServerSessionState.Finished);
        PackFinal();
        BroadcastSnapshot();
        Debug.Log("[Timeline] Match ended — max instant reached.");
        // TODO: trigger match end flow
    }

    // -------------------------------------------------------
    // Decision Loop
    // -------------------------------------------------------
    IEnumerator RunDecisionLoop()
    {
        List<TeamData> teams = session.GetAllTeamData();
        teams[0].isInstantEnded = false;
        teams[1].isInstantEnded = false;
        session.CurrentTeamTurnId = session.FlaggedTeamId;

        while (true)
        {
            if (!TeamHasReady(0) && !TeamHasReady(1)) yield break;
            if (teams[0].isInstantEnded && teams[1].isInstantEnded) yield break;
            yield return HandleTeamTurn(session.CurrentTeamTurnId, teams);
        }
    }
    IEnumerator HandleTeamTurn(int currentTeam, List<TeamData> teams)
    {
        if (teams[currentTeam].isInstantEnded || !TeamHasReady(currentTeam))
        {
            teams[currentTeam].isInstantEnded = true;
            session.CurrentTeamTurnId = 1 - currentTeam;
            yield break;
        }

        TransitionTo(ServerSessionState.DecisionWaiting);
        PackFinal();
        BroadcastSnapshot(); // broadcast ONCE when turn opens

        while (true)
        {
            yield return WaitForDecision(currentTeam);

            if (pendingDecision == null)
            {
                // waited or timed out
                teams[currentTeam].isInstantEnded = true;
                PackFinal();
                BroadcastSnapshot();
                yield break;
            }

            yield return ResolveDecisionEffects(currentTeam);

            if (lastDecisionResolved)
            {
                session.CurrentTeamTurnId = 1 - currentTeam;
                session.FlaggedTeamId = session.CurrentTeamTurnId;
                yield break;
            }
            // invalid resolution — loop silently, no rebroadcast
            // client already got resync from HandleDecision
        }
    }

    // -------------------------------------------------------
    // Wait For Decision
    // -------------------------------------------------------

    IEnumerator WaitForDecision(int team)
    {
        List<TeamData> teams = session.GetAllTeamData();
        pendingDecision = null;
        waitingForDecision = true;
        session.CurrentTeamTurnId = team;

        while (waitingForDecision && (teams[team].WaitDuration > 0 || teams[team].Overtime > 0))
            yield return null;

        waitingForDecision = false;

        if (pendingDecision == null)
        {
            if (teams[team].WaitDuration <= 0 && teams[team].Overtime <= 0)
                Debug.Log($"[Timeline] Team {team} timed out — auto-wait.");
            else
                Debug.Log($"[Timeline] Team {team} decided to wait.");
        }
        else
        {
            Debug.Log($"[Timeline] Team {team} decided: {pendingDecision}");
        }
    }

    // -------------------------------------------------------
    // Resolve Decision Effects
    // -------------------------------------------------------

    IEnumerator ResolveDecisionEffects(int team)
    {
        lastDecisionResolved = false;
        if (pendingDecision == null) yield break;

        var (unitId, target, decisionType, skillCardId) = pendingDecision.Value;
        SkillDefinition skillDef = skillLibrary.Get(skillCardId);

        if (skillDef == null)
        {
            Debug.LogError($"[Timeline] No SkillDefinition for action={decisionType} skillCardId={skillCardId}!");
            yield break;
        }
        // Check skill usage limit
        if (!session.CanUseSkill(unitId, skillCardId, skillDef))
        {
            Debug.LogWarning($"[Timeline] Unit {unitId} cannot use skill {skillCardId}");
            yield break;
        }

        var actionResults = new List<ResolveResult>();

        // Sort effects into buckets
        var immediateEffectIds = new List<int>();
        bool removeFromRQ = false;

        foreach (var effectId in skillDef.effectIds)
        {
            var effect = session.effectRegistry.Get(effectId);
            if (effect == null) continue;
            switch (effect.InstantType)
            {
                case InstantType.Instant:
                    immediateEffectIds.Add(effectId);
                    break;
                case InstantType.HalfInstant:
                    immediateEffectIds.Add(effectId);
                    removeFromRQ = true;
                    break;
                case InstantType.NonInstant:
                    effectRecord.Add((unitId, target, effectId, skillCardId));
                    removeFromRQ = true;
                    break;
                case InstantType.PassiveBuff:
                    break;
            }
        }


        // if (immediateEffectIds.Count == 0) yield break;

        TransitionTo(ServerSessionState.Resolving);

        var snapshotBefore = GetSnapshot();

        float totalDuration = 0f;
        foreach (var effectId in immediateEffectIds)
        {
            var effect = session.effectRegistry.Get(effectId);
            if (effect is ServerActiveEffectBase serverEffect)
            {
                var result = serverEffect.Apply(session, unitId, target);
                if (result.EffectId == -1)
                {
                    TransitionTo(ServerSessionState.DecisionWaiting);
                    yield break;
                }
                actionResults.Add(result);
                totalDuration += effect.resolveDuration;
            }
        }
        lastDecisionResolved = true;
        // wait till resolved to mark, otherwise its not a valid request (doesnt count)
        actionRecord.Add((unitId, skillDef, actionResults));
        session.RecordSkillUsage(unitId, skillCardId);
        var actingUnit = session.GetUnit(unitId);
        if (actingUnit != null)
        {
            actingUnit.CurrentSkillPoint = Mathf.Max(0, actingUnit.CurrentSkillPoint - skillDef.skillPointCost);
            actingUnit.NextStepMultiplier += skillDef.stepCostMultiplier;

            actingUnit.NextStep = Mathf.Max(0, Mathf.RoundToInt(actingUnit.CurrentStepBase * actingUnit.NextStepMultiplier));
        }
        if (removeFromRQ)
            session.ReadyUnitIds.Remove(unitId);

        // Atomic full pack write
        PackResolve(snapshotBefore, new ResolveData
        {
            HasResolve = true,
            SkillCardId = skillCardId,
            decisionType = decisionType,
            ResolveResults = actionResults,
        });
        BroadcastSnapshot();

        yield return new WaitForSeconds(totalDuration);
        TransitionTo(ServerSessionState.DecisionWaiting);
    }

    // -------------------------------------------------------
    // Resolve Effect Record
    // -------------------------------------------------------

    IEnumerator ResolveEffectRecord()
    {
        if (effectRecord.Count == 0) yield break;

        // Capture before state FIRST
        var snapshotBefore = GetSnapshot();

        effectRecord.Sort((a, b) =>
        {
            var unitA = session.GetUnit(a.unitId);
            var unitB = session.GetUnit(b.unitId);

            int speedCompare = (unitB?.Speed ?? 0).CompareTo(unitA?.Speed ?? 0);
            if (speedCompare != 0) return speedCompare;

            int teamA = session.GetTeamIdByUnitId(a.unitId);
            int teamB = session.GetTeamIdByUnitId(b.unitId);
            if (teamA == teamB) return 0;

            bool aGoesFirst = teamA == session.FlaggedTeamId;
            session.FlaggedTeamId = session.GetOtherTeamId(session.FlaggedTeamId);
            return aGoesFirst ? -1 : 1;
        });

        TransitionTo(ServerSessionState.Resolving);

        var allResults = new List<ResolveResult>();
        float totalDuration = 0f;

        foreach (var (unitId, target, effectId, skillCardId) in effectRecord)
        {
            var effect = session.effectRegistry.Get(effectId);
            if (effect is ServerActiveEffectBase serverEffect)
            {
                var result = serverEffect.Apply(session, unitId, target);
                if (result.EffectId == -1) continue;
                allResults.Add(result);
                totalDuration += effect.resolveDuration;
            }
        }

        PackResolve(snapshotBefore, new ResolveData
        {
            HasResolve = true,
            ResolveResults = allResults,
        });
        BroadcastSnapshot();

        yield return new WaitForSeconds(totalDuration);
    }

    // -------------------------------------------------------
    // Step Costs
    // -------------------------------------------------------

    void ApplyStepCosts()
    {
        var totals = new Dictionary<int, float>();

        foreach (var (unitId, skillDef, results) in actionRecord)
        {
            if (!totals.ContainsKey(unitId)) totals[unitId] = 0f;
            totals[unitId] += skillDef.stepCostMultiplier;
        }

        foreach (var kvp in totals)
        {
            var unit = session.GetUnit(kvp.Key);
            if (unit == null) continue;


            int newStep = Mathf.Max(0, Mathf.RoundToInt(unit.CurrentStepBase * kvp.Value));
            unit.CurrentStep = newStep;
            unit.NextStep = 0;
            unit.NextStepMultiplier = 0f;
            unit.StepAlt = !unit.StepAlt;
            Vector2Int stepRange = speedConfig.GetStepRange(unit.Speed);
            unit.CurrentStepBase = unit.StepAlt ? stepRange.y : stepRange.x;

            if (newStep == 0)
                session.ReadyUnitIds.Add(unit.Id);


            Debug.Log($"[Timeline] Unit {unit.Id} step: base={unit.CurrentStepBase} x mult={kvp.Value} = {newStep}");
        }
    }

    // -------------------------------------------------------
    // Handler (called by SyncedBridge)
    // -------------------------------------------------------

    public void HandleDecision(ulong senderClientId, int unitId, Vector3Int target, DecisionType decisionType, int skillcardId, int clientToken)
    {
        int senderTeam = session.GetTeamNumberByClientId(senderClientId);
        Debug.Log($"[Server] Decision received: client={senderClientId} unit={unitId} target={target} type={decisionType} skill={skillcardId}");

        if (clientToken != Token)
        {
            Debug.LogWarning($"[Timeline] Token mismatch (client={clientToken} server={Token}) — resyncing.");
            SendSnapshotToClient(senderClientId);
            return;
        }

        if (session.TimelineState != ServerSessionState.DecisionWaiting || !waitingForDecision)
        {
            Debug.LogWarning($"[Timeline] Wrong state ({session.TimelineState}) — ignored.");
            SendSnapshotToClient(senderClientId);
            return;
        }

        if (senderTeam != session.CurrentTeamTurnId)
        {
            Debug.LogWarning($"[Timeline] Team {senderTeam} sent but team {session.CurrentTeamTurnId}'s turn — ignored.");
            SendSnapshotToClient(senderClientId);
            return;
        }

        if (unitId == -1)
        {
            pendingDecision = null;
            waitingForDecision = false;
            Debug.Log($"[Timeline] Team {senderTeam} chose wait.");
            session.AddLog($"Team {senderTeam} — wait.");
            return;
        }

        if (!session.ReadyUnitIds.Contains(unitId))
        {
            Debug.LogWarning($"[Server] Unit {unitId} not in readyUnitIds");
            SendSnapshotToClient(senderClientId);
            return;
        }

        var unit = session.GetUnit(unitId);
        if (unit == null)
        {
            Debug.LogWarning($"[Server] Unit {unitId} not found");
            SendSnapshotToClient(senderClientId);
            return;
        }

        if (session.GetTeamIdByUnitId(unitId) != senderTeam)
        {
            Debug.LogWarning($"[Server] Unit {unitId} not owned by team {senderTeam}");
            SendSnapshotToClient(senderClientId);
            return;
        }
        SkillDefinition skillDef = session.skillLibrary.Get(skillcardId);
        if (skillDef == null && decisionType != DecisionType.Wait)
        {
            Debug.LogWarning($"[Server] SkillDefinition {skillcardId} not found");
            SendSnapshotToClient(senderClientId);
            return;
        }
        if (skillDef != null)
        {
            //decides to use a skill, need to validate 
            if (!session.CanUseSkill(unitId, skillcardId, skillDef))
            {
                Debug.LogWarning($"[Server] Invalid skill usage: unit={unitId} skill={skillcardId}");
                SendSnapshotToClient(senderClientId);
                return;
            }
        }

        //Logs the decision
        LogTeamCommand(senderTeam, unit, skillDef, target);



        pendingDecision = (unitId, target, decisionType, skillcardId);
        waitingForDecision = false;
        Debug.Log($"[Server] Decision accepted: unit={unitId} target={target}");
    }

    // -------------------------------------------------------
    // Tick
    // -------------------------------------------------------

    void TickAllUnits()
    {
        foreach (var unit in session.units)
            if (unit.CurrentStep > 0)
                unit.CurrentStep--;
    }
    void AllRegenSP()
    {
        // SP regen
        foreach (var unit in session.units)
        {
            bool wasCommanded = actionRecord.Exists(a => a.unitId == unit.Id);
            if (wasCommanded || unit.CurrentSkillPoint >= unit.MaxSkillPoint)
            {
                unit.ConsecutiveRegenInstants = 0;
            }
            else
            {
                unit.ConsecutiveRegenInstants++;
                if (unit.ConsecutiveRegenInstants >= session.matchConfig.conseRegenCap)
                {
                    unit.ConsecutiveRegenInstants = 0;
                    unit.CurrentSkillPoint += 1;
                }
            }
        }
    }

    bool TickAllEffects()
    {
        bool anyChanged = false;

        foreach (var unit in session.units)
        {
            var activeEffects = session.GetUnitActiveEffects(unit.Id);
            bool anyRemoved = false;

            for (int i = activeEffects.Count - 1; i >= 0; i--)
            {
                if (activeEffects[i] is not ServerPassiveEffectBase statusEffect) continue;
                if (statusEffect.isPermanent) continue;

                statusEffect.durationInstants--;
                if (statusEffect.durationInstants <= 0)
                {
                    activeEffects.RemoveAt(i);
                    anyRemoved = true;
                }
            }

            if (anyRemoved)
            {
                unit.ActiveEffectIds = activeEffects.Select(e => e.effectId).ToArray();
                var def = session.unitLibrary.Get(unit.UId);
                anyChanged = true;
            }
        }

        return anyChanged;
    }

    // -------------------------------------------------------
    // Ready Unit Queries
    // -------------------------------------------------------

    bool UpdateReadyQueue()
    {
        bool anyNew = false;
        foreach (var unit in session.units)
        {
            if (unit.CurrentStep == 0 && !session.ReadyUnitIds.Contains(unit.Id))
            {
                session.ReadyUnitIds.Add(unit.Id);
                anyNew = true;
            }
            if (unit.CurrentStep > 0 && session.ReadyUnitIds.Contains(unit.Id))
                session.ReadyUnitIds.Remove(unit.Id);
        }
        return anyNew;
    }

    bool TeamHasReady(int team)
    {
        foreach (int id in session.ReadyUnitIds)
            if (session.GetTeamIdByUnitId(id) == team) return true;
        return false;
    }

    // -------------------------------------------------------
    // Broadcasts
    // -------------------------------------------------------

    void BroadcastTick()
    {
        bridge.BroadcastTimelineTickClientRpc(new TimelineData
        {
            currentInstant = session.CurrentInstant,
            maxInstant = session.matchConfig.maxInstant,
            flag = session.FlaggedTeamId,
            isPaused = session.TimelineState != ServerSessionState.Flowing,
            consecutivePassInstants = session.ConsecutivePassInstants,
            timelinelog = session.GetTimelineLog()
        });
    }

    SessionSnapshotData GetSnapshot()
    {
        return SessionSnapshotData.Full(
            session.MapId ?? string.Empty,
            session.CurrentTeamTurnId,
            session.GetAllCopyTeamData(),
            session.GetAllCopyUnits(),
            new TimelineData
            {
                currentInstant = session.CurrentInstant,
                maxInstant = session.matchConfig.maxInstant,
                flag = session.FlaggedTeamId,
                isPaused = session.TimelineState != ServerSessionState.Flowing,
                consecutivePassInstants = session.ConsecutivePassInstants,
                timelinelog = session.GetTimelineLog()
            }
        );
    }

    public void SendSnapshotToClient(ulong clientId) => SendSnapshot(clientId);

    public void SendSnapshot(ulong clientId)
    {
        int team = session.GetTeamNumberByClientId(clientId);
        SecretData secret = session.GetSecretData(team);
        ClientRpcParams rpcParams = new ClientRpcParams
        {
            Send = new ClientRpcSendParams { TargetClientIds = new ulong[] { clientId } }
        };

        var snapshot = session.LastSnapshot;
        if (snapshot.Teams != null)
            foreach (var snapTeam in snapshot.Teams)
            {
                var liveTeam = session.GetTeamDataByTeamId(snapTeam.teamId);
                if (liveTeam != null) snapTeam.clientId = liveTeam.clientId;
            }

        bridge.SendSnapshotToClientRpc(snapshot, session.LastResolve, secret, session.LastDecision, Token, rpcParams);
    }

    public void BroadcastSnapshot()
    {
        foreach (var clientId in NetworkManager.Singleton.ConnectedClientsIds)
        {
            if (clientId == NetworkManager.Singleton.LocalClientId) continue;
            SendSnapshot(clientId);
        }
    }

    DecisionRequestData BuildDecision()
    {
        if (session.CurrentTeamTurnId == -1) return default;

        List<TeamData> teams = session.GetAllTeamData();
        Debug.Log($"Wait dur{teams[session.CurrentTeamTurnId].WaitDuration} - max wd {session.matchConfig.actWaitWindowPerInstantPerReadyUnit * 5}");
        return session.CurrentTeamTurnId == -1 ? default : new DecisionRequestData
        {
            Instant = session.CurrentInstant,
            DecisionTeam = session.CurrentTeamTurnId,
            RemainingWaitDuration = teams[session.CurrentTeamTurnId].WaitDuration,
            MaxWaitDuration = (int)session.matchConfig.actWaitWindowPerInstantPerReadyUnit * 5,
            RemainingOvertime = teams[session.CurrentTeamTurnId].Overtime,
            MaxOvertime = (int)session.matchConfig.overtimePerTeam,
            ReadyUnitIds = session.ReadyUnitIds.ToArray()
        };
    }

    public int GetSetNewToken() => Token++;

    ulong GetClientIdForTeam(int teamId)
    {
        if (teamId < 0 || teamId >= session.GetAllTeamData().Count) return 0;
        return session.GetTeamDataByTeamId(teamId).clientId;
    }
    void LogInstantFlowing(int instant)
    {
        session.AddLog($"~ Instant {instant} flows by ~");
    }

    void LogInstantPaused(int instant)
    {
        session.AddLog($"Instant {instant} paused.");
    }

    void LogTeamWait(int teamId)
    {
        session.AddLog($"Team {teamId} — wait.");
    }

    void LogTeamCommand(int teamId, UnitData unit, SkillDefinition skill, Vector3Int target)
    {
        UnitDefinition unitDefinition = session.unitLibrary.Get(unit.UId);
        string unitLink = $"<link=\"ID {unit.Id}\"><u>{unitDefinition.unitName}</u></link>";
        string skillLink = $"<link=\"ID {unit.Id} SID {skill.skillId}\"><u>{skill.skillName}</u></link>";
        string cellLink = $"<link=\"Cell {target.x} {target.y}\"><u>({target.x},{target.y})</u></link>";
        session.AddLog($"Team {teamId} — {unitLink} used {skillLink} on {cellLink}.");
    }
    // -------------------------------------------------------
    // Match End
    // -------------------------------------------------------
    IEnumerator EndMatch()
    {
        TransitionTo(ServerSessionState.Finished);

        var teams = session.GetAllTeamData();
        int team0Units = teams[0].unitIds.Count;
        int team1Units = teams[1].unitIds.Count;

        string winnerId = string.Empty;
        if (team0Units > team1Units)
            winnerId = MatchIdentityRegistry.GetPlayerId(teams[0].clientId);
        else if (team1Units > team0Units)
            winnerId = MatchIdentityRegistry.GetPlayerId(teams[1].clientId);

        Debug.Log($"[Timeline] Match ended — winner={winnerId}");

        // Notify clients before shutting down

        var backendClient = session.backendClient;
        if (backendClient != null)
            yield return StartCoroutine(backendClient.ReportResult(winnerId, session.CurrentInstant, session.CurrentInstant));
        NetworkManager.Singleton.Shutdown();
    }

}