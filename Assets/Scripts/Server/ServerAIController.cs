using System.Collections;
using UnityEngine;

public class ServerAIController : MonoBehaviour
{
    [SerializeField] private ServerTimelineManager timeline;
    [SerializeField] private ServerMatchSession session;
    [SerializeField] private StoryEncounterConfig encounterConfig;

    private const int AI_TEAM_ID = 1;

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

            MakeDecision();
        }
    }

    void MakeDecision()
    {
        if (encounterConfig != null && encounterConfig.alwaysWait)
        {
            SendWait();
            return;
        }

        // Future: smarter decision logic per encounter config goes here.
        SendWait();
    }

    void SendWait()
    {
        Debug.Log("[ServerAI] Deciding: Wait.");
        timeline.HandleDecision(ServerMatchSession.AI_CLIENT_ID, unitId: -1, default, DecisionType.Wait, skillcardId: -1, timeline.CurrentToken);
    }
}