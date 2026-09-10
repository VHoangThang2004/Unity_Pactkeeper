using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

public enum ServerSessionState
{
    None,
    Flowing,
    InstantPaused,
    DecisionWaiting,
    Resolving,
    Finished,
}

/// <summary>
/// Server-only. Drives the match timeline with an explicit state machine.
/// </summary>
public class ServerTimelineManager : MonoBehaviour
{
    // -------------------------------------------------------
    // Config
    // -------------------------------------------------------

    [Header("Refs")]
    [SerializeField] private SyncedBridge bridge;
    [SerializeField] private ServerMatchSession session;
    [SerializeField] private SpeedConfig speedConfig;
    [SerializeField] private ActionLibrary actionLibrary;

    [Header("Timeline Config")]
    [SerializeField] private int maxInstant = 100;
    [SerializeField] private float instantDuration = 2f;

    [Header("Decision Timeouts")]
    [SerializeField] private float actWaitWindowPerInstant = 60f;
    [SerializeField] private float overtimePerTeam = 200f;

    [Header("Resolving")]
    [SerializeField] private float resolveDuration = 2.5f;

    private int Token = 0;

    // -------------------------------------------------------
    // State Machine
    // -------------------------------------------------------

    public ServerSessionState State { get; private set; } = ServerSessionState.None;

    void TransitionTo(ServerSessionState next)
    {
        Debug.Log($"[Timeline] {State} -> {next} at instant : {currentInstant}");
        State = next;
    }

    // -------------------------------------------------------
    // Runtime Data
    // -------------------------------------------------------

    private int currentInstant = 0;
    private int flagedTeamId = 0;
    private List<int> readyUnitIds = new List<int>();
    private bool waitingForDecision = false;
    private (int unitId, Vector3Int target, DecisionType action, int skillCardId)? pendingDecision = null;

    // Action record — every decision this instant, for step cost calculation
    private List<(int unitId, ActionDefinition actionDef, List<EffectResult> results)> actionRecord
        = new List<(int, ActionDefinition, List<EffectResult>)>();

    // Effect record — non-instant effects only, resolved at end of instant by speed order
    private List<(int unitId, Vector3Int target, EffectBase effect, int skillCardId)> effectRecord
        = new List<(int, Vector3Int, EffectBase, int)>();

    // -------------------------------------------------------
    // Init
    // -------------------------------------------------------

    public void StartTimeline()
    {
        List<TeamData> teams = session.GetAllTeamData();
        foreach (var team in teams)
            team.Overtime = (int)overtimePerTeam;
        flagedTeamId = session.GetOtherTeamId(0);
        actionLibrary.Init();
        TransitionTo(ServerSessionState.Flowing);
        StartCoroutine(TimelineLoop());
        StartCoroutine(WaitingTimeProcess());
    }

    IEnumerator WaitingTimeProcess()
    {
        while (true)
        {
            TeamData currentTeam = session.GetCurrentTurnTeamData();
            if (waitingForDecision && currentTeam!=null)
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
        while (currentInstant <= maxInstant)
        {
            TransitionTo(ServerSessionState.Flowing);
            session.CurrentTeamTurnId = -1;

            // Reset WaitDuration and instant tracking at instant start
            foreach (var team in teams)
                team.WaitDuration = (int)actWaitWindowPerInstant;
            actionRecord.Clear();
            effectRecord.Clear();

            TickAllUnits();
            bool newUnitsReady = UpdateReadyQueue();
            BroadcastTick();

            bool allReady = session.units.Count > 0 && readyUnitIds.Count >= session.units.Count;
            if (!newUnitsReady && !allReady)
            {
                currentInstant++;
                yield return new WaitForSeconds(instantDuration);
                continue;
            }

            TransitionTo(ServerSessionState.InstantPaused);
            yield return RunDecisionLoop();

            // End of instant — resolve effect record then apply step costs
            yield return ResolveEffectRecord();
            ApplyStepCosts();

            GetSetNewToken();
            BroadcastSnapshot(); // after resolving this, sends the state after resolving all

            currentInstant++;
            yield return new WaitForSeconds(instantDuration);
        }

        TransitionTo(ServerSessionState.Finished);
        Debug.Log("[Timeline] Match ended — max instant reached.");
        BuildSnapshot();

        BroadcastSnapshot();

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
        session.CurrentTeamTurnId = flagedTeamId;

        while (true)
        {
            if (!TeamHasReady(0) && !TeamHasReady(1))
                yield break;
            if (teams[0].isInstantEnded && teams[1].isInstantEnded)
                yield break;


            for (int i = 0; i < 2; i++)
            {
                int currentTeam = session.CurrentTeamTurnId;

                if (!teams[currentTeam].isInstantEnded && TeamHasReady(currentTeam))
                {
                    TransitionTo(ServerSessionState.DecisionWaiting);
                    BuildSnapshot();
                    BroadcastSnapshot();
                    yield return WaitForDecision(currentTeam);

                    if (pendingDecision != null)
                    {
                        yield return ResolveActionInstantEffects(currentTeam);
                        flagedTeamId = session.GetOtherTeamId(flagedTeamId);
                    }
                    else
                    {
                        teams[currentTeam].isInstantEnded = true;
                    }
                }
                else
                {
                    teams[currentTeam].isInstantEnded = true;
                }

                session.CurrentTeamTurnId = 1 - session.CurrentTeamTurnId;
            }
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
            else Debug.Log($"[Timeline] Team {team} Decided to wait.");
        }
        else
        {
            Debug.Log($"WaitDecision result: [Team{team}] decided to {pendingDecision.ToString()}");
        }
    }

    // -------------------------------------------------------
    // Resolve (per decision)
    // -------------------------------------------------------

    IEnumerator ResolveActionInstantEffects(int team)
    {
        if (pendingDecision == null) yield break;

        var (unitId, target, action, skillCardId) = pendingDecision.Value;
        ActionDefinition actionDef = actionLibrary.Get(skillCardId != -1 ? skillCardId : (int)action);

        if (actionDef == null)
        {
            Debug.LogError($"[Timeline] No ActionDefinition found for action {action} skillCardId {skillCardId}!");
            yield break;
        }

        // Add to actionRecord for step cost calculation at end of instant
        var actionResults = new List<EffectResult>();
        actionRecord.Add((unitId, actionDef, actionResults));

        // Check if any effect is non-instant — if so, remove unit from ready queue
        if (actionDef.HasNonInstantEffect())
            readyUnitIds.Remove(unitId);

        // Process each effect
        foreach (var effect in actionDef.effects)
        {
            if (effect == null) continue;

            if (!effect.IsInstant)
            {
                // Queue for end of instant
                effectRecord.Add((unitId, target, effect, skillCardId));
            }
            else
            {
                // Apply instantly
                TransitionTo(ServerSessionState.Resolving);

                // Capture before, apply, capture after — no LastSnapshot shuffling
                var beforeSnap = GetSnapshot();
                var result = effect.Apply(session, unitId, target);
                actionResults.Add(result);
                var afterSnap = GetSnapshot();

                session.LastSnapshot = beforeSnap;
                session.LastResolve = new ResolveData
                {
                    HasResolve = true,
                    UnitId = unitId,
                    Target = target,
                    decision = action,
                    SkillCardId = skillCardId,
                    EffectResults = new List<EffectResult> { result },
                    AfterSnapshot = afterSnap,
                };
                GetSetNewToken();
                BroadcastSnapshot();

                yield return new WaitForSeconds(effect.resolveDuration);
                TransitionTo(ServerSessionState.DecisionWaiting);
            }
        }
    }

    // -------------------------------------------------------
    // Resolve Effect Record (end of instant)
    // -------------------------------------------------------

    IEnumerator ResolveEffectRecord()
    {
        if (effectRecord.Count == 0) yield break;

        // Sort by unit speed descending
        effectRecord.Sort((a, b) =>
        {
            var unitA = session.GetUnit(a.unitId);
            var unitB = session.GetUnit(b.unitId);
            return (unitB?.Speed ?? 0).CompareTo(unitA?.Speed ?? 0);
        });

        foreach (var (unitId, target, effect, skillCardId) in effectRecord)
        {
            TransitionTo(ServerSessionState.Resolving);

            var beforeSnap = GetSnapshot();
            var result = effect.Apply(session, unitId, target);

            foreach (var record in actionRecord)
                if (record.unitId == unitId) { record.results.Add(result); break; }

            var afterSnap = GetSnapshot();

            var actionDef = actionRecord.Find(r => r.unitId == unitId).actionDef;

            session.LastSnapshot = beforeSnap;
            session.LastResolve = new ResolveData
            {
                HasResolve = true,
                UnitId = unitId,
                Target = target,
                SkillCardId = skillCardId,
                EffectResults = new List<EffectResult> { result },
                AfterSnapshot = afterSnap,
            };
            GetSetNewToken();
            BroadcastSnapshot();

            yield return new WaitForSeconds(resolveDuration);
        }
    }

    // -------------------------------------------------------
    // Step Costs (end of instant)
    // -------------------------------------------------------

    void ApplyStepCosts()
    {
        // Sum effect multipliers per unit from actionRecord
        var totals = new Dictionary<int, float>();

        foreach (var (unitId, actionDef, results) in actionRecord)
        {
            if (!totals.ContainsKey(unitId)) totals[unitId] = 0f;

            var unit = session.GetUnit(unitId);
            for (int i = 0; i < actionDef.effects.Length && i < results.Count; i++)
            {
                var effect = actionDef.effects[i];
                if (effect == null) continue;
                totals[unitId] += effect.GetStepMultiplier(results[i], unit);
            }
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
                readyUnitIds.Add(unit.Id);

            Debug.Log($"[Timeline] Unit {unit.Id} step: baseStep={baseStep} x multiplier={kvp.Value} = {newStep}");
        }
    }

    // -------------------------------------------------------
    // Handler (called by ServerController)
    // -------------------------------------------------------

    public void HandleDecision(ulong senderClientId, int unitId, Vector3Int target, DecisionType decisionType, int skillcardId, int clientToken)
    {
        int senderTeam = session.GetTeamNumberByClientId(senderClientId);
        Debug.Log($"[Server] Received decision response: {senderClientId}-{unitId}-{target}-{decisionType}-{skillcardId}");
        // Token mismatch — client is out of sync, resend without incrementing token
        if (clientToken != Token)
        {
            Debug.LogWarning($"[Timeline] Client {senderClientId} token mismatch (client={clientToken} server={Token}) — resyncing.");
            SendSnapshotToClient(senderClientId);
            return;
        }

        Debug.Log($"[Server] State={State} waitingForDecision={waitingForDecision}");
        if (State != ServerSessionState.DecisionWaiting || !waitingForDecision)
        {
            Debug.LogWarning($"[Timeline] Decision received in wrong state ({State}) By client[{senderClientId}]-team[{senderTeam}] Ignored.");
            SendSnapshotToClient(senderClientId);
            return;
        }

        Debug.Log($"[Server] senderTeam={senderTeam} currentTeamTurn={session.CurrentTeamTurnId}");
        if (senderTeam != session.CurrentTeamTurnId)
        {
            Debug.LogWarning($"[Timeline] Team {senderTeam} sent decision but team {session.CurrentTeamTurnId}'s turn. Ignored.");
            SendSnapshotToClient(senderClientId);
            return;
        }

        Debug.Log($"[Server] unitId={unitId}");
        if (unitId == -1)
        {
            pendingDecision = null;
            waitingForDecision = false;
            Debug.Log($"Server authorized the wait decision By client[{senderClientId}]-team[{senderTeam}]");
            return;
        }

        Debug.Log($"[Server] readyUnitIds=[{string.Join(",", readyUnitIds)}] contains {unitId}={readyUnitIds.Contains(unitId)}");
        if (!readyUnitIds.Contains(unitId))
        {
            Debug.LogWarning($"[Server] Unit {unitId} not in readyUnitIds");
            SendSnapshotToClient(senderClientId);
            return;
        }


        Debug.Log("Validated decision, about to apply");

        var unit = session.GetUnit(unitId);
        Debug.Log($"[Server] unit={unit?.Id} unit.team={unit?.team} senderTeam={senderTeam}");
        if (unit == null || unit.team != senderTeam) { Debug.LogWarning($"[Server] Unit null or wrong team"); return; }

        var moveResult = session.AuthorizeMove(senderClientId, unitId, target);
        Debug.Log($"[Server] moveResult={moveResult}");
        if (moveResult != ServerMatchSession.MoveResult.Ok) { Debug.LogWarning($"[Server] Move denied: {moveResult}"); return; }

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

    // -------------------------------------------------------
    // Ready Unit Queries
    // -------------------------------------------------------

    bool UpdateReadyQueue()
    {
        bool anyNew = false;
        foreach (var unit in session.units)
        {
            if (unit.CurrentStep == 0 && !readyUnitIds.Contains(unit.Id))
            {
                readyUnitIds.Add(unit.Id);
                anyNew = true;
            }
            if (unit.CurrentStep > 0 && readyUnitIds.Contains(unit.Id))
            {
                readyUnitIds.Remove(unit.Id);
            }
        }
        return anyNew;
    }

    bool TeamHasReady(int team)
    {
        foreach (int id in readyUnitIds)
        {
            var unit = session.GetUnit(id);
            if (unit != null && unit.team == team) return true;
        }
        return false;
    }

    // -------------------------------------------------------
    // Broadcasts
    // -------------------------------------------------------

    void BroadcastTick()
    {
        bridge.BroadcastTimelineTickClientRpc(new TimelineData
        {
            currentInstant = currentInstant,
            maxInstant = maxInstant,
            flag = flagedTeamId,
            isPaused = State != ServerSessionState.Flowing,
        });
    }

    private DecisionRequestData NullDecision;

    /// <summary>
    /// Builds snapshot and stores as LastSnapshot + clears LastResolve.
    /// Use for decision snapshots where no resolve is needed.
    /// </summary>
    void BuildSnapshot()
    {
        GetSetNewToken();
        session.LastSnapshot = GetSnapshot();
        session.LastResolve = default;
    }

    /// <summary>
    /// Builds and returns snapshot without storing or touching LastSnapshot/LastResolve.
    /// Use in resolve contexts where you need before/after snapshots separately.
    /// </summary>
    SessionSnapshotData GetSnapshot()
    {
        return new SessionSnapshotData
        {
            MapId = session.MapId ?? string.Empty,
            CurrentTeamTurn = session.CurrentTeamTurnId,
            Teams = session.GetAllTeamData(),
            Units = session.GetAllUnits(),
            Timeline = new TimelineData
            {
                currentInstant = currentInstant,
                maxInstant = maxInstant,
                flag = flagedTeamId,
                isPaused = State != ServerSessionState.Flowing,
            }
        };
    }

    /// <summary>Send latest snapshot to a specific client — for initial state and resync.</summary>
    public void SendSnapshotToClient(ulong clientId) => SendSnapshot(clientId);

    /// <summary>Send session.LastSnapshot to one specific client.</summary>
    public void SendSnapshot(ulong clientId)
    {
        int team = session.GetTeamNumberByClientId(clientId);
        if (team == -1)
        {
            Debug.Log($"[Timeline] SendSnapshot: client {clientId} has no team. Maybe this client is specating the match.");
        }
        SecretData secret = session.GetSecretData(team);

        ClientRpcParams rpcParams = new ClientRpcParams
        {
            Send = new ClientRpcSendParams { TargetClientIds = new ulong[] { clientId } }
        };
        var snapshotToSend = session.LastResolve.HasResolve ? session.LastSnapshot : GetSnapshot();
        bridge.SendSnapshotToClientRpc(snapshotToSend, session.LastResolve, secret, BuildDecision(), Token, rpcParams);
    }

    /// <summary>Broadcast session.LastSnapshot to all clients.</summary>
    public void BroadcastSnapshot()
    {
        //broadcast to all connected clients (except self, server doesnt receive this)
        foreach (var team in session.GetAllTeamData())
            SendSnapshot(team.clientId);
    }

    DecisionRequestData BuildDecision()
    {
        List<TeamData> teams = session.GetAllTeamData();
        return session.CurrentTeamTurnId == -1 ? NullDecision : new DecisionRequestData
        {
            Instant = currentInstant,
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