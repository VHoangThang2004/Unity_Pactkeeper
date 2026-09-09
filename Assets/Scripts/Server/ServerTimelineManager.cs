using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

/// <summary>
/// Server-only. Drives the timeline: ticking instants, pausing when units are
/// ready, running the sequential act/wait decision loop, and resolving instants.
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
    [SerializeField] private float instantDuration = 2f;       // seconds per instant

    [Header("Decision Timeouts")]
    [SerializeField] private float actWaitWindow = 5f;         // seconds to decide act/wait
    [SerializeField] private float actionWindow = 30f;         // seconds to decide action
    [SerializeField] private float overtimePerTeam = 200f;     // total overtime bank per team

    // -------------------------------------------------------
    // Runtime State (server-only elite knowledge)
    // -------------------------------------------------------

    private int currentInstant = 0;
    private int flag = 0;                   // which team has priority (0 or 1)
    private int consecutiveWaits = 0;
    private bool isPaused = false;

    private List<int> readyUnitIds = new List<int>();

    // Decision loop state
    private int activeDecisionTeam = -1;   // team currently being asked
    private bool waitingForDecision = false;
    private bool? pendingDecision = null;   // null = not answered, true = act, false = wait
    private bool waitingForAction = false;
    private (int unitId, Vector3Int target)? pendingAction = null;

    private float[] overtimeRemaining;     // indexed by team

    // -------------------------------------------------------
    // Init
    // -------------------------------------------------------

    public void StartTimeline()
    {
        overtimeRemaining = new float[] { overtimePerTeam, overtimePerTeam };
        StartCoroutine(TimelineLoop());
    }

    // -------------------------------------------------------
    // Main Loop
    // -------------------------------------------------------

    IEnumerator TimelineLoop()
    {
        while (currentInstant <= maxInstant)
        {
            // --- Tick all unit steps ---
            TickAllUnits();

            // --- Gather ready units ---
            readyUnitIds = GetReadyUnitIds();

            // --- Broadcast tick to all clients ---
            BroadcastTick();

            if (readyUnitIds.Count == 0)
            {
                // No one ready — advance instant freely
                currentInstant++;
                yield return new WaitForSeconds(instantDuration);
                continue;
            }

            // --- Pause and run decision loop ---
            isPaused = true;
            yield return RunDecisionLoop();
            isPaused = false;

            currentInstant++;
            yield return new WaitForSeconds(instantDuration);
        }

        Debug.Log("[Timeline] Match ended — max instant reached.");
        // TODO: trigger match end
    }

    // -------------------------------------------------------
    // Decision Loop
    // -------------------------------------------------------

    IEnumerator RunDecisionLoop()
    {
        consecutiveWaits = 0;

        while (true)
        {
            // Determine which teams have ready units
            bool team0HasReady = TeamHasReady(0);
            bool team1HasReady = TeamHasReady(1);

            if (!team0HasReady && !team1HasReady)
                yield break; // instant fully resolved

            // Determine asking order: flagged team first if both ready, otherwise whoever has units
            int firstTeam  = (team0HasReady && team1HasReady) ? flag : (team0HasReady ? 0 : 1);
            int secondTeam = 1 - firstTeam;

            // --- Ask first team ---
            bool firstActed = false;
            if (TeamHasReady(firstTeam))
            {
                yield return AskTeam(firstTeam);

                if (pendingDecision == true)
                {
                    // Team chose to act — run their action
                    yield return RunAction(firstTeam);
                    firstActed = true;
                    consecutiveWaits = 0;
                    flag = secondTeam; // flag flips after action
                }
                else
                {
                    // Team waited
                    consecutiveWaits++;
                }
            }

            // --- Check 2 consecutive waits ---
            if (consecutiveWaits >= 2)
                yield break;

            // --- Ask second team ---
            if (TeamHasReady(secondTeam))
            {
                yield return AskTeam(secondTeam);

                if (pendingDecision == true)
                {
                    yield return RunAction(secondTeam);
                    consecutiveWaits = 0;
                    flag = firstTeam; // flag flips after action
                }
                else
                {
                    consecutiveWaits++;
                }
            }
            else if (!firstActed)
            {
                // Only one team had ready units and they waited — that counts as 2 consecutive
                // (other team implicitly waited too since they had nothing)
                consecutiveWaits++;
            }

            // --- Check 2 consecutive waits again ---
            if (consecutiveWaits >= 2)
                yield break;

            // Loop: check if anyone still ready
            readyUnitIds = GetReadyUnitIds();
            bool stillReady = TeamHasReady(0) || TeamHasReady(1);
            if (!stillReady)
                yield break;
        }
    }

    // -------------------------------------------------------
    // Ask a team: act or wait?
    // -------------------------------------------------------

    IEnumerator AskTeam(int team)
    {
        activeDecisionTeam = team;
        pendingDecision = null;
        waitingForDecision = true;

        float deadline = Time.time + actWaitWindow;

        // Initial broadcast
        BroadcastDecisionRequest(team, deadline, -1f);

        // Resend every 2s and wait for response or timeout
        float nextResend = Time.time + 2f;
        while (waitingForDecision && Time.time < deadline)
        {
            if (Time.time >= nextResend)
            {
                BroadcastDecisionRequest(team, deadline, -1f);
                nextResend = Time.time + 2f;
            }

            // Drain overtime if in overtime (deadline passed normal window)
            DrainOvertime(team, Time.deltaTime);

            yield return null;
        }

        // Timeout → auto-wait
        if (pendingDecision == null)
        {
            Debug.Log($"[Timeline] Team {team} timed out on act/wait — auto-wait.");
            pendingDecision = false;
        }

        waitingForDecision = false;
    }

    // -------------------------------------------------------
    // Run action for a team (pick unit + perform)
    // -------------------------------------------------------

    IEnumerator RunAction(int team)
    {
        pendingAction = null;
        waitingForAction = true;

        float deadline = Time.time + actionWindow;
        BroadcastDecisionRequest(team, -1f, deadline);

        float nextResend = Time.time + 2f;
        while (waitingForAction && Time.time < deadline)
        {
            if (Time.time >= nextResend)
            {
                BroadcastDecisionRequest(team, -1f, deadline);
                nextResend = Time.time + 2f;
            }

            DrainOvertime(team, Time.deltaTime);

            yield return null;
        }

        if (pendingAction == null)
        {
            // Timeout — auto-resolve: use first ready unit, stay in place
            int fallbackId = GetFirstReadyUnitForTeam(team);
            if (fallbackId != -1)
            {
                var unit = session.GetUnit(fallbackId);
                pendingAction = (fallbackId, unit.CurrentCell);
                Debug.Log($"[Timeline] Team {team} timed out on action — auto-resolve unit {fallbackId} in place.");
            }
            else
            {
                waitingForAction = false;
                yield break;
            }
        }

        waitingForAction = false;

        // Apply the action
        var (unitId, target) = pendingAction.Value;
        ApplyUnitAction(unitId, target);
    }

    // -------------------------------------------------------
    // Apply unit action: move + reset step
    // -------------------------------------------------------

    void ApplyUnitAction(int unitId, Vector3Int target)
    {
        var unit = session.GetUnit(unitId);
        if (unit == null)
        {
            Debug.LogError($"[Timeline] ApplyUnitAction: unit {unitId} not found!");
            return;
        }

        // Apply move (reuse session auth — already validated on send)
        session.ApplyMove(unitId, target);
        bridge.MoveConfirmedClientRpc(unitId, target);

        // Reset step — alternate low/high
        Vector2Int stepRange = speedConfig.GetStepRange(unit.Speed);
        int newStep = unit.stepAlt ? stepRange.y : stepRange.x;
        unit.stepAlt = !unit.stepAlt;
        unit.CurrentStep = newStep;

        // Remove from ready list
        readyUnitIds.Remove(unitId);

        // If new step is 0, unit is immediately ready again in this instant
        if (newStep == 0)
        {
            readyUnitIds.Add(unitId);
            Debug.Log($"[Timeline] Unit {unitId} step reset to 0 — immediately re-ready.");
        }

        // Sync step to clients (not the formula, just the resulting step value)
        bridge.BroadcastUnitStepResetClientRpc(unitId, newStep);

        Debug.Log($"[Timeline] Unit {unitId} acted → moved to {target}, new step={newStep}");
    }

    // -------------------------------------------------------
    // Handlers (called by SyncedBridge from client RPCs)
    // -------------------------------------------------------

    public void HandleActWaitDecision(ulong senderClientId, bool wantsToAct)
    {
        if (!waitingForDecision) return;

        int senderTeam = session.GetTeamNumber(senderClientId);
        if (senderTeam != activeDecisionTeam)
        {
            Debug.LogWarning($"[Timeline] Client {senderClientId} (team {senderTeam}) sent decision but it's team {activeDecisionTeam}'s turn. Ignored.");
            return;
        }

        pendingDecision = wantsToAct;
        waitingForDecision = false;
    }

    public void HandleActionDecision(ulong senderClientId, int unitId, Vector3Int target)
    {
        if (!waitingForAction) return;

        int senderTeam = session.GetTeamNumber(senderClientId);
        if (senderTeam != activeDecisionTeam)
        {
            Debug.LogWarning($"[Timeline] Client {senderClientId} tried to submit action but it's not their turn. Ignored.");
            return;
        }

        // Validate: unit must be ready and owned by this team
        if (!readyUnitIds.Contains(unitId))
        {
            Debug.LogWarning($"[Timeline] Unit {unitId} is not in ready list. Action rejected.");
            return;
        }

        var unit = session.GetUnit(unitId);
        if (unit == null || unit.team != senderTeam)
        {
            Debug.LogWarning($"[Timeline] Unit {unitId} does not belong to team {senderTeam}. Action rejected.");
            return;
        }

        // Validate move
        var moveResult = session.AuthorizeMove(senderClientId, unitId, target);
        if (moveResult != ServerMatchSession.MoveResult.Ok)
        {
            Debug.LogWarning($"[Timeline] Move denied for unit {unitId}: {moveResult}");
            // Desync — send snapshot to clear client error
            bridge.SendInitialStateClientRpc(BuildSnapshot(), GetClientIdForTeam(senderTeam), default);
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
        {
            if (unit.CurrentStep > 0)
                unit.CurrentStep--;
        }
    }

    // -------------------------------------------------------
    // Ready Unit Queries
    // -------------------------------------------------------

    List<int> GetReadyUnitIds()
    {
        var ready = new List<int>();
        foreach (var unit in session.units)
        {
            if (unit.CurrentStep == 0)
                ready.Add(unit.Id);
        }
        return ready;
    }

    bool TeamHasReady(int team)
    {
        foreach (int id in readyUnitIds)
        {
            var unit = session.GetUnit(id);
            if (unit != null && unit.team == team)
                return true;
        }
        return false;
    }

    int GetFirstReadyUnitForTeam(int team)
    {
        foreach (int id in readyUnitIds)
        {
            var unit = session.GetUnit(id);
            if (unit != null && unit.team == team)
                return id;
        }
        return -1;
    }

    // -------------------------------------------------------
    // Broadcasts
    // -------------------------------------------------------

    void BroadcastTick()
    {
        var data = new TimelineData
        {
            currentInstant    = currentInstant,
            maxInstant        = maxInstant,
            flag              = flag,           // flag IS in TimelineData but server controls it — send it, clients can display it if desired
            consecutiveWaits  = consecutiveWaits,
            isPaused          = isPaused,
            readyUnitIds      = new List<int>(readyUnitIds),
        };
        bridge.BroadcastTimelineTickClientRpc(data);
    }

    void BroadcastDecisionRequest(int team, float actWaitDeadline, float actionDeadline)
    {
        var data = new DecisionRequestData
        {
            Instant          = currentInstant,
            DecisionTeam     = team,
            ActWaitDeadline  = actWaitDeadline,
            ActionDeadline   = actionDeadline,
            OvertimeTeam0    = overtimeRemaining[0],
            OvertimeTeam1    = overtimeRemaining[1],
        };
        bridge.BroadcastDecisionRequestClientRpc(data);
    }

    // -------------------------------------------------------
    // Overtime
    // -------------------------------------------------------

    void DrainOvertime(int team, float delta)
    {
        overtimeRemaining[team] = Mathf.Max(0f, overtimeRemaining[team] - delta);
        if (overtimeRemaining[team] <= 0f)
        {
            // Force decision / action resolution immediately
            if (waitingForDecision) { pendingDecision = false; waitingForDecision = false; }
            if (waitingForAction)   { waitingForAction = false; }
            Debug.LogWarning($"[Timeline] Team {team} overtime exhausted — forcing resolution.");
        }
    }

    // -------------------------------------------------------
    // Snapshot Helper
    // -------------------------------------------------------

    SessionSnapshotData BuildSnapshot()
    {
        return new SessionSnapshotData
        {
            Turn               = session.Turn,
            CurrentPlayerTurn  = session.CurrentPlayerTurn,
            Teams              = session.teams,
            Units              = session.GetAllUnits(),
            Timeline           = new TimelineData
            {
                currentInstant   = currentInstant,
                maxInstant       = maxInstant,
                flag             = flag,
                consecutiveWaits = consecutiveWaits,
                isPaused         = isPaused,
                readyUnitIds     = new List<int>(readyUnitIds),
            }
        };
    }

    ulong GetClientIdForTeam(int team)
    {
        if (team < 0 || team >= session.teams.Count) return 0;
        return session.teams[team].clientId;
    }
}