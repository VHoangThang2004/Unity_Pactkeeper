using System.Collections;
using Unity.Netcode;
using UnityEngine;

public class ServerController : MonoBehaviour
{
    [Header("Bridge")]
    [SerializeField] private SyncedBridge bridge;

    [SerializeField] ServerMatchSession session;
    [SerializeField] ServerSpawnManager spawnManager;
    [SerializeField] ServerTimelineManager timeline;

    private int trialErrorCount = 0;
    private int totalErrorCount = 0;

    // -------------------------------------------------------
    // Session Guard
    // -------------------------------------------------------

    IEnumerator SessionGuard()
    {
        while (true)
        {
            if (trialErrorCount > 10 || totalErrorCount > 50)
            {
                Debug.LogError("[ServerController] Too many errors — shutting down!");
                // TODO: send clients to main menu
                NetworkManager.Singleton.Shutdown();
                Application.Quit();
            }
            yield return new WaitForSeconds(60f);
            trialErrorCount = 0;
        }
    }

    void IncrementErrors()
    {
        trialErrorCount++;
        totalErrorCount++;
    }

    // -------------------------------------------------------
    // Init
    // -------------------------------------------------------

    private void Start()
    {
        StartCoroutine(SessionGuard());
        StartCoroutine(InitSequence());
    }

    IEnumerator InitSequence()
    {
        while (!session.Init())
        {
            Debug.LogError("[ServerController] MatchSession failed to initialize, retrying...");
            IncrementErrors();
            yield return new WaitForSeconds(1f);
        }

        while (!spawnManager.spawnAll())
        {
            Debug.LogError("[ServerController] SpawnManager failed, retrying...");
            IncrementErrors();
            yield return new WaitForSeconds(1f);
        }

        foreach (ulong clientId in NetworkManager.Singleton.ConnectedClientsIds)
            HandleAllInitialStateRequest(clientId);

        timeline.StartTimeline();

        Debug.Log("[ServerController] Server initialized successfully.");
    }

    // -------------------------------------------------------
    // Handlers (called by SyncedBridge)
    // -------------------------------------------------------

    /// <summary>
    /// Single entry point for all client decisions.
    /// unitId = -1 means wait. Valid unitId means act.
    /// </summary>
    public void HandleDecision(ulong clientId, int unitId, Vector3Int target, DecisionType decisionType, int skillCardId, int clientToken)
    {
        timeline.HandleDecision(clientId, unitId, target, decisionType, skillCardId, clientToken);
    }

    public void HandleAllInitialStateRequest(ulong clientId)
    {
        timeline.SendSnapshotToClient(clientId);
    }
}