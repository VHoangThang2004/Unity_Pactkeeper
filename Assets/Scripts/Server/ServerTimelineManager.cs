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

    [Header("Timeline Config")]
    [SerializeField] private int maxInstant = 100;
    [SerializeField] private float instantDuration = 2f;

    [Header("Decision Timeouts")]
    [SerializeField] private float actWaitWindowPerInstant = 60f;
    [SerializeField] private float overtimePerTeam = 200f;

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
            team.Overtime = (int)overtimePerTeam;

        session.FlaggedTeamId = session.GetOtherTeamId(0);

        TransitionTo(ServerSessionState.Flowing);

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

        while (session.CurrentInstant <= maxInstant)
        {
            TransitionTo(ServerSessionState.Flowing);
            session.CurrentTeamTurnId = -1;

            // Reset per-instant tracking
            foreach (var team in teams)
                team.WaitDuration = (int)actWaitWindowPerInstant;
            actionRecord.Clear();
            effectRecord.Clear();
            session.ResetInstantSkillUsage();

            // Tick
            TickAllUnits();
            bool effectsChanged = TickAllEffects();
            bool newUnitsReady = UpdateReadyQueue();

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
                yield return new WaitForSeconds(instantDuration);
                continue;
            }

            // Instant paused — run decision loop
            TransitionTo(ServerSessionState.InstantPaused);
            yield return RunDecisionLoop();

            // Always broadcast after decision loop — even if no decisions made
            PackFinal();
            BroadcastSnapshot();


            foreach (var unit in session.units)
                Debug.Log($"[PreResolve] Unit {unit.Id} HP={unit.CurrentHP}");

            // End of instant — resolve non-instant effects
            yield return ResolveEffectRecord();

            // Apply step costs + final broadcast
            ApplyStepCosts();
            PackFinal();
            BroadcastSnapshot();

            session.CurrentInstant++;
            yield return new WaitForSeconds(instantDuration);
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
            Debug.LogWarning($"[Timeline] Skill {skillCardId} usage limit reached for unit {unitId}");
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
        //TODO: reduce skill points as cost
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

            Vector2Int stepRange = speedConfig.GetStepRange(unit.Speed);
            float baseStep = unit.stepAlt ? stepRange.y : stepRange.x;
            unit.stepAlt = !unit.stepAlt;

            int newStep = Mathf.Max(0, Mathf.RoundToInt(baseStep * kvp.Value));
            unit.CurrentStep = newStep;

            if (newStep == 0)
                session.ReadyUnitIds.Add(unit.Id);

            Debug.Log($"[Timeline] Unit {unit.Id} step: base={baseStep} x mult={kvp.Value} = {newStep}");
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
            maxInstant = maxInstant,
            flag = session.FlaggedTeamId,
            isPaused = session.TimelineState != ServerSessionState.Flowing,
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
                maxInstant = maxInstant,
                flag = session.FlaggedTeamId,
                isPaused = session.TimelineState != ServerSessionState.Flowing,
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
        bridge.SendSnapshotToClientRpc(
            session.LastSnapshot,
            session.LastResolve,
            secret,
            session.LastDecision,
            Token,
            rpcParams);
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
        List<TeamData> teams = session.GetAllTeamData();
        return session.CurrentTeamTurnId == -1 ? default : new DecisionRequestData
        {
            Instant = session.CurrentInstant,
            DecisionTeam = session.CurrentTeamTurnId,
            WaitDuration = teams[session.CurrentTeamTurnId].WaitDuration,
            Overtime = teams[session.CurrentTeamTurnId].Overtime
        };
    }

    public int GetSetNewToken() => Token++;

    ulong GetClientIdForTeam(int teamId)
    {
        if (teamId < 0 || teamId >= session.GetAllTeamData().Count) return 0;
        return session.GetTeamDataByTeamId(teamId).clientId;
    }

}