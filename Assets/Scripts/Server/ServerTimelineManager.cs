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

public enum WinCondition
{
    MostUnitsAlive,  // PvP default — whoever has more units wins on timeout
    EliminateAll,    // PvE default — must eliminate all enemies to win
}

public class ServerTimelineManager : MonoBehaviour
{
    // -------------------------------------------------------
    // Config
    // -------------------------------------------------------

    [Header("Refs")]
    private SyncedBridge bridge;
    [SerializeField] private ServerMatchSession session;
    [SerializeField] private SpeedConfig speedConfig;
    [SerializeField] private SkillLibrary skillLibrary;
    [SerializeField] private ServerSpawnManager spawnManager;

    [Header("Match Settings")]
    [SerializeField] public bool isPvE = false;
    [SerializeField] public WinCondition winCondition = WinCondition.MostUnitsAlive;



    // Internal coroutine control — not observable, not session data
    private Coroutine timelineLoopCoroutine;
    private int timelineGenerationId = 0;
    private bool waitingForDecision = false;
    private bool lastDecisionResolved = false;
    private (int unitId, Vector3Int sourceCell, Vector3Int targetCell, DecisionType decisionType, int skillCardId)? pendingDecision = null;

    // Transient per-instant records — live and die within one instant
    private List<(int unitId, SkillDefinition skilldef, List<ResolveResult> results)> actionRecord;
    private List<(int unitId, Vector3Int sourceCell, Vector3Int targetCell, int effectId, int skillCardId)> effectRecord;
    // Sync token — control concern, client/server handshake only
    private int Token = 0;
    public int CurrentToken => Token;
    private float _matchStartTime = 0f;
    private const float MAX_MATCH_DURATION = 3600f; // 60 minutes
    private const float DISCONNECT_TIMEOUT = 60f;   // 60s after all clients disconnect

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
    public void InitTimeline(SyncedBridge bridge)
    {
        this.bridge = bridge;
        actionRecord = new List<(int, SkillDefinition, List<ResolveResult>)>();
        effectRecord = new List<(int, Vector3Int, Vector3Int, int, int)>();
        List<TeamData> teams = session.GetAllTeamData();
        foreach (var team in teams)
            team.Overtime = (int)session.matchConfig.overtimePerTeam;

        if (session.backendClient != null && session.backendClient.Mode == "story")
            isPvE = true;

        session.FlaggedTeamId = session.GetOtherTeamId(0);

        TransitionTo(ServerSessionState.Flowing);
        LogInstantFlowing(session.CurrentInstant);

        Debug.Log("[Timeline] Initialized and pack ready.");
    }
    public void RunTimeline()
    {
        _matchStartTime = Time.time;
        StopAllCoroutines();
        int gen = ++timelineGenerationId;
        StartCoroutine(WaitingTimeProcess());
        StartCoroutine(DisconnectWatchdog());
        StartCoroutine(MatchTimeoutWatchdog());
        timelineLoopCoroutine = StartCoroutine(TimelineLoop(gen));
        Debug.Log("[Timeline] Running.");
    }

    IEnumerator WaitingTimeProcess()
    {
        // PvE: no time pressure — player and AI can take as long as needed
        if (isPvE) yield break;

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

    IEnumerator TimelineLoop(int gen)
    {
        List<TeamData> teams = session.GetAllTeamData();

        while (session.CurrentInstant <= session.matchConfig.maxInstant)
        {
            if (gen != timelineGenerationId) yield break;
            TransitionTo(ServerSessionState.Flowing);
            session.CurrentTeamTurnId = -1;

            AllRegenSP();
            actionRecord.Clear();
            effectRecord.Clear();
            session.ResetInstantSkillUsage();

            // Tick
            TickAllUnits();
            bool effectsChanged = TickAllEffects();
            PassiveHooks.InstantStart(session); // after the tick, so 1-instant buffs have already expired
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

            if (gen != timelineGenerationId) yield break;

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
            bool gameEnded = true;
            foreach (var unit in session.GetAllUnits())
            {
                if (session.GetTeamIdByUnitId(unit.Id) != session.GetTeamIdByUnitId(session.units.First().Id))
                {
                    gameEnded = false;
                }
            }
            if (gameEnded)
            {
                yield return StartCoroutine(EndMatch());
                yield break;
            }
            TransitionTo(ServerSessionState.InstantPaused);
            LogInstantPaused(session.CurrentInstant);
            yield return RunDecisionLoop(gen);

            if (gen != timelineGenerationId) yield break;

            // Always broadcast after decision loop — even if no decisions made
            PackFinal();
            BroadcastSnapshot();


            // foreach (var unit in session.units)
            //     Debug.Log($"[PreResolve] Unit {unit.Id} HP={unit.CurrentHP}");

            // End of instant — resolve non-instant effects
            yield return ResolveEffectRecord();

            // Apply step costs + final broadcast
            ApplyStepCosts();
            PassiveHooks.InstantEnd(session); // last stage of a paused instant — after all resolves
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
    IEnumerator RunDecisionLoop(int gen)
    {
        List<TeamData> teams = session.GetAllTeamData();
        session.ReloadInstant();
        session.CurrentTeamTurnId = session.FlaggedTeamId;

        while (true)
        {
            if (gen != timelineGenerationId) yield break;
            if (!TeamHasReady(0) && !TeamHasReady(1)) yield break;
            if (teams[0].isInstantEnded && teams[1].isInstantEnded) yield break;
            yield return HandleTeamTurn(session.CurrentTeamTurnId, gen);
        }
    }
    IEnumerator HandleTeamTurn(int currentTeamId, int gen)
    {
        if (gen != timelineGenerationId) yield break;
        TeamData currentTeam = session.GetTeamDataByTeamId(currentTeamId);
        if (currentTeam.isInstantEnded || !TeamHasReady(currentTeamId))
        {
            session.CurrentTeamTurnId = session.GetOppositeTeamId(currentTeamId);
            yield break;
        }

        TransitionTo(ServerSessionState.DecisionWaiting);
        PackFinal();
        BroadcastSnapshot(); // broadcast ONCE when turn opens

        while (true)
        {
            if (gen != timelineGenerationId) yield break;
            TransitionTo(ServerSessionState.DecisionWaiting);
            yield return WaitForDecision(currentTeamId, gen);

            if (pendingDecision == null)
            {
                // waited or timed out
                currentTeam.isInstantEnded = true;
                PackFinal();
                BroadcastSnapshot();
                yield return new WaitForSeconds(session.matchConfig.eyeAnimDur);
                yield break;
            }

            yield return ResolveDecisionEffects(currentTeamId);

            if (lastDecisionResolved)
            {
                session.CurrentTeamTurnId = session.GetOppositeTeamId(currentTeamId);
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

    IEnumerator WaitForDecision(int team, int gen)
    {
        yield return new WaitForSeconds(2f);
        if (gen != timelineGenerationId) yield break;
        List<TeamData> teams = session.GetAllTeamData();
        pendingDecision = null;
        waitingForDecision = true;
        session.CurrentTeamTurnId = team;

        while (waitingForDecision && (teams[team].WaitDuration > 0 || teams[team].Overtime > 0))
        {
            if (gen != timelineGenerationId) yield break;
            yield return null;
        }

        waitingForDecision = false;
        if (gen != timelineGenerationId) yield break;

        if (pendingDecision == null)
        {
            if (teams[team].WaitDuration <= 0 && teams[team].Overtime <= 0)
                Debug.Log($"[Timeline] Team {team} timed out — auto-wait.");
            else
                Debug.Log($"[Timeline] Team {team} decided to wait.");

            LogTeamWait(team);
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

        var (unitId, sourceCell, targetCell, decisionType, skillCardId) = pendingDecision.Value;
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
                    effectRecord.Add((unitId, sourceCell, targetCell, effectId, skillCardId));
                    removeFromRQ = true;
                    break;
                case InstantType.PassiveBuff:
                    break;
            }
        }


        TransitionTo(ServerSessionState.Resolving);

        var snapshotBefore = GetSnapshot();

        float totalDuration = 0f;
        foreach (var effectId in immediateEffectIds)
        {
            var effect = session.effectRegistry.Get(effectId);
            if (effect is ServerActiveEffectBase serverEffect)
            {
                var result = serverEffect.Apply(session, unitId, sourceCell, targetCell);
                if (result.EffectId == -1)
                {
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
        var actingUnit = session.GetUnitByUnitId(unitId);
        if (actingUnit != null)
        {
            actingUnit.CurrentSkillPoint = Mathf.Max(0, actingUnit.CurrentSkillPoint - skillDef.skillPointCost);
            actingUnit.NextStepMultiplier += skillDef.stepCostMultiplier;

            actingUnit.NextStep = Mathf.Max(0, Mathf.RoundToInt(actingUnit.CurrentStepBase * actingUnit.NextStepMultiplier));
        }
        if (removeFromRQ)
        {
            session.ReadyUnitIds.Remove(unitId);
            TeamHasReady(session.GetTeamByUnitId(unitId).teamId);
        }

        // Post command timing
        if (actingUnit != null)
        {
            PassiveHooks.Command(session, actingUnit, new CommandContext
            {
                Skill = skillDef,
                Slot = actingUnit.GetSlot(skillCardId),
                IsAttack = System.Array.Exists(skillDef.effectIds, id => session.effectRegistry.Get(id) is DamageEffectServerBase),
                SourceCell = sourceCell,
                TargetCell = targetCell,
            });
        }

        // Atomic full pack write
        PackResolve(snapshotBefore, new ResolveData
        {
            HasResolve = true,
            SkillCardId = skillCardId,
            decisionType = decisionType,
            ResolveResults = actionResults,
        });
        BroadcastSnapshot();

        yield return new WaitForSeconds(totalDuration + 0.5f);
    }

    // -------------------------------------------------------
    // Resolve Effect Record
    // -------------------------------------------------------

    IEnumerator ResolveEffectRecord()
    {
        if (effectRecord.Count == 0) yield break;

        // Capture before state FIRST
        var snapshotBefore = GetSnapshot();

        // Comparer must be pure: read the flag once, flip it once after sorting if it decided a tie
        int flaggedTeam = session.FlaggedTeamId;
        bool flagUsed = false;
        effectRecord.Sort((a, b) =>
        {
            var unitA = session.GetUnitByUnitId(a.unitId);
            var unitB = session.GetUnitByUnitId(b.unitId);

            int speedCompare = (unitB?.Speed ?? 0).CompareTo(unitA?.Speed ?? 0);
            if (speedCompare != 0) return speedCompare;

            int teamA = session.GetTeamIdByUnitId(a.unitId);
            int teamB = session.GetTeamIdByUnitId(b.unitId);
            if (teamA == teamB) return a.unitId.CompareTo(b.unitId); // deterministic within a team

            flagUsed = true;
            return teamA == flaggedTeam ? -1 : 1;
        });
        if (flagUsed)
            session.FlaggedTeamId = session.GetOtherTeamId(flaggedTeam);

        TransitionTo(ServerSessionState.Resolving);

        var allResults = new List<ResolveResult>();
        float totalDuration = 0f;

        foreach (var (unitId, sourceCell, targetCell, effectId, skillCardId) in effectRecord)
        {
            var effect = session.effectRegistry.Get(effectId);
            if (effect is ServerActiveEffectBase serverEffect)
            {
                var result = serverEffect.Apply(session, unitId, sourceCell, targetCell);
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

        yield return new WaitForSeconds(totalDuration + 0.5f);
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
            var unit = session.GetUnitByUnitId(kvp.Key);
            if (unit == null) continue;


            int newStep = Mathf.Max(0, Mathf.RoundToInt(unit.CurrentStepBase * kvp.Value));
            unit.CurrentStep += newStep;
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
        int senderTeamId = session.GetTeamNumberByClientId(senderClientId);
        Debug.Log($"[Server] Decision received: client={senderClientId} teamId = {senderTeamId} unit={unitId} target={target} type={decisionType} skill={skillcardId}");

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

        if (senderTeamId != session.CurrentTeamTurnId)
        {
            Debug.LogWarning($"[Timeline] Team {senderTeamId} sent but team {session.CurrentTeamTurnId}'s turn — ignored.");
            SendSnapshotToClient(senderClientId);
            return;
        }

        if (unitId == -1)
        {
            pendingDecision = null;
            waitingForDecision = false;
            Debug.Log($"[Timeline] Team {senderTeamId} chose wait.");
            // session.AddLog($"Team {senderTeam} — wait."); // truncated (log later in decision loop)
            return;
        }

        if (!session.ReadyUnitIds.Contains(unitId))
        {
            Debug.LogWarning($"[Server] Unit {unitId} not in readyUnitIds");
            SendSnapshotToClient(senderClientId);
            return;
        }

        var unit = session.GetUnitByUnitId(unitId);
        if (unit == null)
        {
            Debug.LogWarning($"[Server] Unit {unitId} not found");
            SendSnapshotToClient(senderClientId);
            return;
        }

        if (session.GetTeamIdByUnitId(unitId) != senderTeamId)
        {
            Debug.LogWarning($"[Server] Unit {unitId} not owned by team {senderTeamId}");
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
            if (!session.IsValidSkillTarget(unit, skillcardId, target))
            {
                Debug.LogWarning($"[Server] Invalid skill/target: unit={unitId} skill={skillcardId} target={target}");
                SendSnapshotToClient(senderClientId);
                return;
            }
        }

        //Logs the decision
        LogTeamCommand(senderTeamId, unit, skillDef, target);



        pendingDecision = (unitId, unit.CurrentCell, target, decisionType, skillcardId);
        waitingForDecision = false;
        Debug.Log($"[Server] Decision accepted: unit={unitId} target={target}");
    }

    // Read-only state ServerAIController polls to know when it's the AI's turn.
    public bool IsWaitingForDecision => waitingForDecision;
    public int CurrentDecisionTeamId => session.CurrentTeamTurnId;

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
                if (activeEffects[i] is not ActiveEffectInstance instance) continue;
                bool expired = false;

                // Stack loss — applies to permanent statuses too (0 = none, -1 = all)
                int loss = instance.effect.stacksLostPerInstant;
                if (loss != 0)
                {
                    instance.stacks = loss < 0 ? 0 : instance.stacks - loss;
                    expired |= instance.stacks <= 0;
                }

                // Duration
                if (!instance.isPermanent)
                {
                    instance.remainingInstants--;
                    expired |= instance.remainingInstants <= 0;
                }

                if (expired)
                {
                    activeEffects.RemoveAt(i);
                    anyRemoved = true;
                }
            }

            if (anyRemoved)
            {
                anyChanged = true; // views refresh in PackFinal → RecalculateAll
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

    bool TeamHasReady(int teamId)
    {
        foreach (int id in session.ReadyUnitIds)
            if (session.GetTeamIdByUnitId(id) == teamId) return true;
        session.GetTeamDataByTeamId(teamId).isInstantEnded = true;
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

    SessionSnapshotData GetSnapshot() => session.FullSnapshot();

    public void SendSnapshotToClient(ulong clientId) => SendSnapshot(clientId);

    public void SendSnapshot(ulong clientId)
    {
        if (clientId == ServerMatchSession.AI_CLIENT_ID) return; // AI has no network client
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

    public void RestartTimelineForTutorialPhase()
    {
        Debug.Log($"[Timeline] Restarting timeline for tutorial phase from Instant 0 (was at Instant {session.CurrentInstant})...");

        StopAllCoroutines();
        int gen = ++timelineGenerationId;
        timelineLoopCoroutine = null;

        waitingForDecision = false;
        pendingDecision = null;
        lastDecisionResolved = false;

        actionRecord?.Clear();
        effectRecord?.Clear();
        session.ClearLogs();
        session.ResetInstantSkillUsage();

        session.CurrentInstant = 0;
        session.ConsecutivePassInstants = 0;
        session.CurrentTeamTurnId = -1;

        List<TeamData> teams = session.GetAllTeamData();
        foreach (var team in teams)
        {
            team.isInstantEnded = false;
            team.WaitDuration = 0;
        }

        // Re-check ready units in case any unit has CurrentStep == 0
        session.ReadyUnitIds.Clear();
        foreach (var unit in session.GetAllUnits())
        {
            if (unit.CurrentStep == 0 && !session.ReadyUnitIds.Contains(unit.Id))
            {
                session.ReadyUnitIds.Add(unit.Id);
            }
        }

        TransitionTo(ServerSessionState.Flowing);
        LogInstantFlowing(session.CurrentInstant);

        PackFinal();
        BroadcastSnapshot();

        StartCoroutine(WaitingTimeProcess());
        StartCoroutine(DisconnectWatchdog());
        StartCoroutine(MatchTimeoutWatchdog());
        timelineLoopCoroutine = StartCoroutine(TimelineLoop(gen));
    }

    DecisionRequestData BuildDecision()
    {
        if (session.CurrentTeamTurnId == -1) return default;

        List<TeamData> teams = session.GetAllTeamData();
        int teamId = session.CurrentTeamTurnId;
        int waitDur = 0;
        int overtime = 0;
        if (teamId >= 0 && teamId < teams.Count)
        {
            waitDur = teams[teamId].WaitDuration;
            overtime = teams[teamId].Overtime;
        }

        return new DecisionRequestData
        {
            Instant = session.CurrentInstant,
            DecisionTeam = teamId,
            RemainingWaitDuration = waitDur,
            MaxWaitDuration = (int)session.matchConfig.actWaitWindowPerInstantPerReadyUnit * 5,
            RemainingOvertime = overtime,
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
        string unitLink = $"<link=\"ID {unit.Id}\"><u>Unit {unit.UId}</u></link>";
        string skillLink = $"<link=\"ID {unit.Id} SID {skill.skillId}\"><u>{skill.skillName}</u></link>";
        string cellLink = $"<link=\"Cell {target.x} {target.y}\"><u>({target.x},{target.y})</u></link>";
        session.AddLog($"Team {teamId} — {unitLink} used {skillLink} on {cellLink}.");
    }
    // -------------------------------------------------------
    // Watchdogs
    // -------------------------------------------------------

    // Cancel match if all real clients disconnect for 60s
    IEnumerator DisconnectWatchdog()
    {
        float disconnectedSince = -1f;

        while (session.TimelineState != ServerSessionState.Finished)
        {
            int realClients = 0;
            foreach (var id in NetworkManager.Singleton.ConnectedClientsIds)
                if (id != NetworkManager.Singleton.LocalClientId) realClients++;

            if (realClients == 0)
            {
                float timeout = isPvE ? 3f : DISCONNECT_TIMEOUT;
                if (disconnectedSince < 0)
                    disconnectedSince = Time.time;
                else if (Time.time - disconnectedSince >= timeout)
                {
                    Debug.Log($"[Timeline] All clients disconnected for {timeout}s — cancelling match.");
                    yield return StartCoroutine(EndMatch());
                    yield break;
                }
            }
            else
            {
                disconnectedSince = -1f; // reset if someone reconnects
            }

            yield return new WaitForSeconds(1f);
        }
    }

    // Cancel match if running longer than 60 minutes
    IEnumerator MatchTimeoutWatchdog()
    {
        while (session.TimelineState != ServerSessionState.Finished)
        {
            if (Time.time - _matchStartTime >= MAX_MATCH_DURATION)
            {
                Debug.Log("[Timeline] Match exceeded 60 minutes — ending match.");
                yield return StartCoroutine(EndMatch());
                yield break;
            }
            yield return new WaitForSeconds(30f);
        }
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
        if (winCondition == WinCondition.EliminateAll)
        {
            // PvE: only wins if opponent is fully eliminated
            if (team1Units == 0 && team0Units > 0)
                winnerId = MatchIdentityRegistry.GetPlayerId(teams[0].clientId);
            else if (team0Units == 0 && team1Units > 0)
                winnerId = MatchIdentityRegistry.GetPlayerId(teams[1].clientId);
            // else: no winner (timeout/draw — no progress reported for story)
        }
        else
        {
            // PvP: more units alive = winner
            if (team0Units > team1Units)
                winnerId = MatchIdentityRegistry.GetPlayerId(teams[0].clientId);
            else if (team1Units > team0Units)
                winnerId = MatchIdentityRegistry.GetPlayerId(teams[1].clientId);
        }

        Debug.Log($"[Timeline] Match ended — winner={winnerId}");

        var afterMatchData = new AfterMatchPlayerReport[]
        {
            new AfterMatchPlayerReport
            {
                playerId = teams[0].playerId,
                unitsAlive = team0Units,
                totalUnits = session.GetTeamLoadoutDataByTeamId(teams[0].teamId)?.Units?.Count ?? 0
            },
            new AfterMatchPlayerReport
            {
                playerId = teams[1].playerId,
                unitsAlive = team1Units,
                totalUnits = session.GetTeamLoadoutDataByTeamId(teams[1].teamId)?.Units?.Count ?? 0
            }
        };

        NetworkManager.Singleton.Shutdown();

        // Notify backend before shutting down

        var backendClient = session.backendClient;
        if (backendClient != null)
        {
            if (backendClient.Mode == "story")
            {
                // Player is always team 0 in story mode. Player wins if AI team (1) is wiped.
                bool playerWon = team1Units == 0 && team0Units > 0;
                string storyPlayerId = teams[0].playerId;

                if (playerWon)
                    yield return StartCoroutine(backendClient.ReportBattleComplete(storyPlayerId));
                else
                    Debug.Log("[Timeline] Story battle not won — no progress reported.");

                yield return StartCoroutine(backendClient.ReportResult(
                    winnerId, session.CurrentInstant, session.CurrentInstant, afterMatchData));
            }
            else
            {
                yield return StartCoroutine(backendClient.ReportResult(
                    winnerId, session.CurrentInstant, session.CurrentInstant, afterMatchData));
            }
        }

        if (isPvE)
            Application.Quit();
    }

}