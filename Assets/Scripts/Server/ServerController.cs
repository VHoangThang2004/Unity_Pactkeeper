using System.Collections;
using Unity.Netcode;
using UnityEngine;

public class ServerController : MonoBehaviour, ISyncedBridgeServer
{
    [Header("Bridge")]
    [SerializeField] private SyncedBridge bridgePrefab;
    public SyncedBridge bridge;
    [SerializeField] private SceneConfig sceneConfig;
    [SerializeField] ServerMatchSession session;
    [SerializeField] ServerSpawnManager spawnManager;
    [SerializeField] ServerTimelineManager timeline;
    [SerializeField] private ServerAIController aiController; // story mode only — leave null in PvP scenes


    private ServerBackendClient backendClient;
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
                yield return StartCoroutine(Shutdown());
                yield break;
            }
            yield return new WaitForSeconds(60f);
            trialErrorCount = 0;
        }
    }
    IEnumerator Shutdown()
    {
        NetworkManager.Singleton.Shutdown();
        yield return StartCoroutine(backendClient.ReportCancelled());
    }




    void IncrementErrors()
    {
        trialErrorCount++;
        totalErrorCount++;
    }

    // -------------------------------------------------------
    // Init
    // -------------------------------------------------------

    void Start()
    {
        if (!NetworkManager.Singleton.IsServer)
            return;

        var bridge = Instantiate(bridgePrefab);
        bridge.NetworkObject.Spawn();

        Debug.Log("[Server] SyncedBridge spawned");
    }

    public void OnBridgeReady(SyncedBridge bridge)
    {
        this.bridge = bridge;
        Debug.Log("[ServerController] Bridge registered.");
        StartSequence();
    }
    private void StartSequence()
    {
        backendClient = FindAnyObjectByType<ServerBackendClient>();
        if (backendClient == null)
            Debug.LogError("[ServerController] ServerBackendClient not found!");

        StartCoroutine(SessionGuard());
        StartCoroutine(InitSequence());
    }
    private int clientsReceivedInit = 0;


    private bool serverReady = false;

    IEnumerator InitSequence()
    {

        // 0. Wait for backend client to finish fetching
        var bc = FindAnyObjectByType<ServerBackendClient>();
        if (bc != null)
            yield return new WaitUntil(() => bc.IsReady);
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
        timeline.InitTimeline(bridge);

        // 4. Recalculate all units — applies passive buffs onto stats
        UnitRecalculator.RecalculateAll(session);

        // 5. Build full pack — snapshot captures fully buffed unit states
        timeline.PackFinal();

        Debug.Log("[ServerController] Ready — waiting for clients to request init.");

        // 6. Wait for all clients to request init
        serverReady = true;
        while (clientsReceivedInit < session.matchConfig.requiredClients)
        {
            yield return new WaitForSeconds(1f);
            IncrementErrors();
        }

        // 7. Grace period — let clients finish their own init
        Debug.Log($"[ServerController] All clients initialized — grace period {session.matchConfig.postInitGracePeriod}s.");
        yield return new WaitForSeconds(session.matchConfig.postInitGracePeriod);

        // 8. Start timeline loop
        Debug.Log("[ServerController] Starting timeline.");
        timeline.RunTimeline();

        // 9. Start AI controller — story mode only
        if (backendClient.Mode == "story" && aiController != null)
        {
            aiController.StartAI();
            Debug.Log("[ServerController] AI controller started.");
        }
    }

    public void HandleAllInitialStateRequest(ulong clientId)
    {
        if (!serverReady) return; // ignore request if not ready
        if (clientId == NetworkManager.Singleton.LocalClientId) return;

        timeline.SendSnapshotToClient(clientId);
        clientsReceivedInit++;

        Debug.Log($"[ServerController] Client {clientId} received init ({clientsReceivedInit}/{session.matchConfig.requiredClients})");
    }
    public void HandleDecision(ulong clientId, int unitId, Vector3Int target, DecisionType decisionType, int skillCardId, int clientToken)
    {
        timeline.HandleDecision(clientId, unitId, target, decisionType, skillCardId, clientToken);
    }
}