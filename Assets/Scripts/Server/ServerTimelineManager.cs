using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

/// <summary>
/// Server-only. Drives the match timeline with an explicit state machine.
/// Controls all sync decisions based on current state.
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

    [Header("Timeline Config")]
    [SerializeField] private int maxInstant = 100;
    [SerializeField] private float instantDuration = 2f;

    [Header("Decision Timeouts")]
    [SerializeField] private float actWaitWindow = 5f;
    [SerializeField] private float actionWindow = 30f;
    [SerializeField] private float overtimePerTeam = 200f;

    [Header("Resolving")]
    [SerializeField] private float resolveDuration = 2.5f;

    // -------------------------------------------------------
    // State Machine
    // -------------------------------------------------------

    public ServerSessionState State { get; private set; } = ServerSessionState.None;

    void TransitionTo(ServerSessionState next)
    {
        Debug.Log($"[Timeline] {State} -> {next}");
        State = next;
    }

    // -------------------------------------------------------
    // Runtime Data
    // -------------------------------------------------------

    private int currentInstant = 0;
    private int flag = 0;
    private int consecutiveWaits = 0;
    private List<int> readyUnitIds = new List<int>();
    private float[] overtimeRemaining;

    private int activeDecisionTeam = -1;
    private bool waitingForDecision = false;
    private bool? pendingDecision = null;
    private bool waitingForAction = false;
    private (int unitId, Vector3Int target)? pendingAction = null;

    // -------------------------------------------------------
    // Init
    // -------------------------------------------------------

    public void StartTimeline()
    {
        overtimeRemaining = new float[] { overtimePerTeam, overtimePerTeam };
        TransitionTo(ServerSessionState.Flowing);
        StartCoroutine(TimelineLoop());
    }

    // -------------------------------------------------------
    // Main Loop
    // -------------------------------------------------------

    IEnumerator TimelineLoop()
    {
        while (currentInstant <= maxInstant)
        {
            TransitionTo(ServerSessionState.Flowing);

            TickAllUnits();
            bool newUnitsReady = UpdateReadyQueue();
            BroadcastTick();

            // Pause if new units hit 0 OR all alive units are in the ready queue
            bool allReady = session.units.Count > 0 && readyUnitIds.Count >= session.units.Count;
            if (!newUnitsReady && !allReady)
            {
                currentInstant++;
                yield return new WaitForSeconds(instantDuration);
                continue;
            }

            // Pause this instant
            TransitionTo(ServerSessionState.InstantPaused);
            yield return RunDecisionLoop();

            currentInstant++;
            yield return new WaitForSeconds(instantDuration);
        }

        TransitionTo(ServerSessionState.Finished);
        Debug.Log("[Timeline] Match ended — max instant reached.");
        BroadcastFullSnapshot();
        // TODO: trigger match end flow
    }

    // -------------------------------------------------------
    // Decision Loop
    // -------------------------------------------------------

    IEnumerator RunDecisionLoop()
    {
        consecutiveWaits = 0;

        while (true)
        {
            if (!TeamHasReady(0) && !TeamHasReady(1))
                yield break;

            if (consecutiveWaits >= 2)
                yield break;

            for (int i = 0; i < 2; i++)
            {
                int team = (flag + i) % 2;

                if (TeamHasReady(team))
                {
                    TransitionTo(ServerSessionState.DecisionWaiting);
                    BroadcastMidSnapshotWithDecision(team, (int)actWaitWindow, -1);
                    yield return WaitForActWait(team);

                    if (pendingDecision == true)
                    {
                        TransitionTo(ServerSessionState.ActionWaiting);
                        BroadcastMidSnapshotWithDecision(team, -1, (int)actionWindow);
                        yield return WaitForAction(team);
                        yield return Resolve(team);

                        consecutiveWaits = 0;
                        flag = 1 - team;
                    }
                    else
                    {
                        consecutiveWaits++;
                    }
                }
                else
                {
                    consecutiveWaits++;
                }

                if (consecutiveWaits >= 2) yield break;
            }
        }
    }

    // -------------------------------------------------------
    // Wait For Act/Wait
    // -------------------------------------------------------

    IEnumerator WaitForActWait(int team)
    {
        activeDecisionTeam = team;
        pendingDecision = null;
        waitingForDecision = true;

        // Normal window — check every frame, count down every second
        float remaining = actWaitWindow;
        while (waitingForDecision && remaining > 0f)
        {
            remaining -= Time.deltaTime;
            yield return null;
        }

        // Overtime — only if no decision yet
        while (waitingForDecision && overtimeRemaining[team] > 0f)
        {
            DrainOvertime(team, Time.deltaTime);
            yield return null;
        }

        if (pendingDecision == null)
        {
            Debug.Log($"[Timeline] Team {team} timed out on act/wait — auto-wait.");
            pendingDecision = false;
        }

        waitingForDecision = false;
    }

    // -------------------------------------------------------
    // Wait For Action
    // -------------------------------------------------------

    IEnumerator WaitForAction(int team)
    {
        pendingAction = null;
        waitingForAction = true;

        // Normal window — check every frame, count down every second
        float remaining = actionWindow;
        while (waitingForAction && remaining > 0f)
        {
            remaining -= Time.deltaTime;
            yield return null;
        }

        // Overtime — only if no action yet
        while (waitingForAction && overtimeRemaining[team] > 0f)
        {
            DrainOvertime(team, Time.deltaTime);
            yield return null;
        }

        waitingForAction = false;

        if (pendingAction == null)
        {
            int fallbackId = GetFirstReadyUnitForTeam(team);
            if (fallbackId != -1)
            {
                var unit = session.GetUnit(fallbackId);
                pendingAction = (fallbackId, unit.CurrentCell);
                Debug.Log($"[Timeline] Team {team} timed out — StandByWithoutAction unit {fallbackId}.");
            }
        }
    }

    // -------------------------------------------------------
    // Resolve
    // -------------------------------------------------------

    IEnumerator Resolve(int team)
    {
        if (pendingAction == null) yield break;

        TransitionTo(ServerSessionState.Resolving);

        var (unitId, target) = pendingAction.Value;
        ApplyUnitAction(unitId, target);

        yield return new WaitForSeconds(resolveDuration);
    }

    // -------------------------------------------------------
    // Apply Action
    // -------------------------------------------------------

    void ApplyUnitAction(int unitId, Vector3Int target)
    {
        var unit = session.GetUnit(unitId);
        if (unit == null)
        {
            Debug.LogError($"[Timeline] ApplyUnitAction: unit {unitId} not found!");
            return;
        }

        session.ApplyMove(unitId, target);
        bridge.MoveConfirmedClientRpc(unitId, target);

        Vector2Int stepRange = speedConfig.GetStepRange(unit.Speed);
        int newStep = unit.stepAlt ? stepRange.y : stepRange.x;
        unit.stepAlt = !unit.stepAlt;
        unit.CurrentStep = newStep;

        readyUnitIds.Remove(unitId);

        if (newStep == 0)
        {
            readyUnitIds.Add(unitId);
            Debug.Log($"[Timeline] Unit {unitId} step reset to 0 — immediately re-ready.");
        }

        bridge.BroadcastUnitStepResetClientRpc(unitId, newStep);
        Debug.Log($"[Timeline] Unit {unitId} acted -> {target}, new step={newStep}");
    }

    // -------------------------------------------------------
    // Handlers (called by ServerController)
    // -------------------------------------------------------

    public void HandleActWaitDecision(ulong senderClientId, bool wantsToAct)
    {
        if (State != ServerSessionState.DecisionWaiting)
        {
            Debug.LogWarning($"[Timeline] ActWait received in wrong state ({State}). Ignored.");
            return;
        }
        if (!waitingForDecision) return;

        int senderTeam = session.GetTeamNumber(senderClientId);
        if (senderTeam != activeDecisionTeam)
        {
            Debug.LogWarning($"[Timeline] Team {senderTeam} sent decision but team {activeDecisionTeam}'s turn. Ignored.");
            return;
        }

        pendingDecision = wantsToAct;
        waitingForDecision = false;
    }

    public void HandleActionDecision(ulong senderClientId, int unitId, Vector3Int target)
    {
        if (State != ServerSessionState.ActionWaiting)
        {
            Debug.LogWarning($"[Timeline] Action received in wrong state ({State}). Ignored.");
            return;
        }
        if (!waitingForAction) return;

        int senderTeam = session.GetTeamNumber(senderClientId);
        if (senderTeam != activeDecisionTeam)
        {
            Debug.LogWarning($"[Timeline] Team {senderTeam} sent action but team {activeDecisionTeam}'s turn. Ignored.");
            return;
        }

        if (!readyUnitIds.Contains(unitId))
        {
            Debug.LogWarning($"[Timeline] Unit {unitId} not in ready list. Rejected.");
            return;
        }

        var unit = session.GetUnit(unitId);
        if (unit == null || unit.team != senderTeam)
        {
            Debug.LogWarning($"[Timeline] Unit {unitId} not owned by team {senderTeam}. Rejected.");
            return;
        }

        var moveResult = session.AuthorizeMove(senderClientId, unitId, target);
        if (moveResult != ServerMatchSession.MoveResult.Ok)
        {
            Debug.LogWarning($"[Timeline] Move denied for unit {unitId}: {moveResult}");
            BroadcastMidSnapshotWithDecision(senderTeam, -1, (int)actionWindow);
            return;
        }

        pendingAction = (unitId, target);
        waitingForAction = false;
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
        }
        return anyNew;
    }

    List<int> GetReadyUnitIds()
    {
        var ready = new List<int>();
        foreach (var unit in session.units)
            if (unit.CurrentStep == 0)
                ready.Add(unit.Id);
        return ready;
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

    int GetFirstReadyUnitForTeam(int team)
    {
        foreach (int id in readyUnitIds)
        {
            var unit = session.GetUnit(id);
            if (unit != null && unit.team == team) return id;
        }
        return -1;
    }

    // -------------------------------------------------------
    // Broadcasts
    // -------------------------------------------------------

    void BroadcastTick()
    {
        bridge.BroadcastTimelineTickClientRpc(new TimelineData
        {
            currentInstant   = currentInstant,
            maxInstant       = maxInstant,
            flag             = flag,
            consecutiveWaits = consecutiveWaits,
            isPaused         = State != ServerSessionState.Flowing,
            readyUnitIds     = new List<int>(readyUnitIds),
        });
    }

    void BroadcastMidSnapshotWithDecision(int team, int actWaitDuration, int actionDuration)
    {
        var snapshot = new SessionSnapshotData
        {
            Type              = SnapshotType.Mid,
            MapId             = session.MapId,
            Turn              = session.Turn,
            CurrentPlayerTurn = session.CurrentPlayerTurn,
            Teams             = session.teams,
            Units             = session.GetAllUnits(),
            Timeline          = new TimelineData
            {
                currentInstant   = currentInstant,
                maxInstant       = maxInstant,
                flag             = flag,
                consecutiveWaits = consecutiveWaits,
                isPaused         = true,
                readyUnitIds     = new List<int>(readyUnitIds),
            }
        };

        var decision = new DecisionRequestData
        {
            Instant         = currentInstant,
            DecisionTeam    = team,
            ActWaitDuration = actWaitDuration,
            ActionDuration  = actionDuration,
            OvertimeTeam0   = (int)overtimeRemaining[0],
            OvertimeTeam1   = (int)overtimeRemaining[1],
        };

        bridge.BroadcastMidSnapshotWithDecisionClientRpc(snapshot, decision);
    }

    void BroadcastFullSnapshot()
    {
        bridge.BroadcastSnapshotClientRpc(new SessionSnapshotData
        {
            Type              = SnapshotType.Full,
            MapId             = session.MapId,
            Turn              = session.Turn,
            CurrentPlayerTurn = session.CurrentPlayerTurn,
            Teams             = session.teams,
            Units             = session.GetAllUnits(),
            Timeline          = new TimelineData
            {
                currentInstant   = currentInstant,
                maxInstant       = maxInstant,
                flag             = flag,
                consecutiveWaits = consecutiveWaits,
                isPaused         = false,
                readyUnitIds     = new List<int>(readyUnitIds),
            }
        });
    }

    // -------------------------------------------------------
    // Overtime
    // -------------------------------------------------------

    void DrainOvertime(int team, float delta)
    {
        overtimeRemaining[team] = Mathf.Max(0f, overtimeRemaining[team] - delta);
        if (overtimeRemaining[team] <= 0f)
        {
            if (waitingForDecision) { pendingDecision = false; waitingForDecision = false; }
            if (waitingForAction)   { waitingForAction = false; }
            Debug.LogWarning($"[Timeline] Team {team} overtime exhausted — forcing resolution.");
        }
    }

    ulong GetClientIdForTeam(int team)
    {
        if (team < 0 || team >= session.teams.Count) return 0;
        return session.teams[team].clientId;
    }
}