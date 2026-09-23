using System.Collections;
using UnityEngine;

/// <summary>
/// Controls the AI team's decisions in story/PvE matches.
/// Runs in-process on the server — calls ServerTimelineManager.HandleAIDecision()
/// directly, the same logical path a real player's RPC would take, just without
/// going through the network or token validation (server-trusted).
///
/// Chapter 0 behaviour: always Wait. The AI exists only as a punching bag so the
/// player can learn movement and skills without being threatened. Smarter
/// behaviour (attack if in range, move closer otherwise) can be added per-encounter
/// later via StoryEncounterConfig without changing this controller's structure.
/// </summary>
public class ServerAIController : MonoBehaviour
{
    [SerializeField] private ServerTimelineManager timeline;
    [SerializeField] private ServerMatchSession session;
    [SerializeField] private StoryEncounterConfig encounterConfig;

    private const int AI_TEAM_ID = 1; // AI is always team 1 — see StoryEncounterConfig.ToAITeamLoadout()
    private const float DECISION_DELAY = 1.5f; // small delay so it doesn't feel instant/robotic

    private Coroutine watchCoroutine;

    public void StartAI()
    {
        watchCoroutine = StartCoroutine(WatchForTurn());
        Debug.Log("[ServerAI] Started watching for AI team's turn.");
    }

    public void StopAI()
    {
        if (watchCoroutine != null)
            StopCoroutine(watchCoroutine);
    }

    IEnumerator WatchForTurn()
    {
        while (true)
        {
            yield return null;

            if (!timeline.IsWaitingForDecision) continue;
            if (timeline.CurrentDecisionTeamId != AI_TEAM_ID) continue;

            yield return StartCoroutine(MakeDecision());
        }
    }

    IEnumerator MakeDecision()
    {
        yield return new WaitForSeconds(DECISION_DELAY);

        // Re-check state after the delay — turn may have ended already
        // (e.g. team has no ready units anymore, or timed out)
        if (!timeline.IsWaitingForDecision || timeline.CurrentDecisionTeamId != AI_TEAM_ID)
            yield break;

        if (encounterConfig != null && encounterConfig.alwaysWait)
        {
            SendWait();
            yield break;
        }

        // Future: smarter decision logic per encounter config goes here.
        // For now, fall back to Wait for any encounter that isn't explicitly alwaysWait.
        SendWait();
    }

    void SendWait()
    {
        Debug.Log("[ServerAI] Deciding: Wait.");
        timeline.HandleAIDecision(AI_TEAM_ID, unitId: -1, default,
            DecisionType.Wait, skillCardId: -1);
    }
}