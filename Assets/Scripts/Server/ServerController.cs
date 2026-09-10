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
    private int clientsReceivedInit = 0;
    private int requiredClients = 2;

    [Header("Config")]
    [SerializeField] private float postInitGracePeriod = 3f;
    [SerializeField] private float clientInitTimeout = 30f;

    IEnumerator InitSequence()
    {
        // 1. Init session
        while (!session.Init())
        {
            Debug.LogError("[ServerController] MatchSession failed, retrying...");
            IncrementErrors();
            yield return new WaitForSeconds(1f);
        }

        // 2. Spawn all units
        while (!spawnManager.spawnAll())
        {
            Debug.LogError("[ServerController] SpawnManager failed, retrying...");
            IncrementErrors();
            yield return new WaitForSeconds(1f);
        }

        // 3. Init timeline — sets up records, inits skill library, transitions to Flowing
        timeline.InitTimeline();

        // 4. Recalculate all units — applies passive buffs onto stats
        spawnManager.RecalculateAll();

        // 5. Build full pack — snapshot captures fully buffed unit states
        timeline.PackFinal();

        Debug.Log("[ServerController] Ready — waiting for clients to request init.");

        // 6. Wait for all clients to request init
        float elapsed = 0f;
        while (clientsReceivedInit < requiredClients)
        {
            elapsed += Time.deltaTime;
            if (elapsed >= clientInitTimeout)
            {
                Debug.LogError($"[ServerController] Timeout — only {clientsReceivedInit}/{requiredClients} clients. Shutting down.");
                // TODO: signal backend
                NetworkManager.Singleton.Shutdown();
                Application.Quit();
                yield break;
            }
            yield return null;
        }

        // 7. Grace period — let clients finish their own init
        Debug.Log($"[ServerController] All clients initialized — grace period {postInitGracePeriod}s.");
        yield return new WaitForSeconds(postInitGracePeriod);

        // 8. Start timeline loop
        Debug.Log("[ServerController] Starting timeline.");
        timeline.RunTimeline();
    }

    public void HandleAllInitialStateRequest(ulong clientId)
    {
        if (clientId == NetworkManager.Singleton.LocalClientId) return;

        timeline.SendSnapshotToClient(clientId);
        clientsReceivedInit++;

        Debug.Log($"[ServerController] Client {clientId} received init ({clientsReceivedInit}/{requiredClients})");
    }
    public void HandleDecision(ulong clientId, int unitId, Vector3Int target, DecisionType decisionType, int skillCardId, int clientToken)
    {
        timeline.HandleDecision(clientId, unitId, target, decisionType, skillCardId, clientToken);
    }
}