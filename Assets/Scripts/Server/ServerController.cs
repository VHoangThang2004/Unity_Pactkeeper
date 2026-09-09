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

    private int trialErrorCount = 0;   // resets every 60s — burst detection
    private int totalErrorCount = 0;   // never resets — absolute limit

    // -------------------------------------------------------
    // Session Guard
    // -------------------------------------------------------

    IEnumerator SessionGuard()
    {
        while (true)
        {
            if (trialErrorCount > 10 || totalErrorCount > 5000)
            {
                Debug.LogError("[ServerController] Too many errors — shutting down to prevent further damage!");
                // TODO: send clients to main menu
                NetworkManager.Singleton.Shutdown();
                Application.Quit();
            }
            yield return new WaitForSeconds(60f);
            trialErrorCount = 0;
            // totalErrorCount intentionally never resets
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
        // --- Init session ---
        while (!session.Init())
        {
            Debug.LogError("[ServerController] MatchSession failed to initialize, retrying...");
            IncrementErrors();
            yield return new WaitForSeconds(1f);
        }

        // --- Spawn units ---
        while (!spawnManager.spawnAll())
        {
            Debug.LogError("[ServerController] SpawnManager failed, retrying...");
            IncrementErrors();
            yield return new WaitForSeconds(1f);
        }

        // --- Send snapshot to all connected clients ---
        foreach (ulong clientId in NetworkManager.Singleton.ConnectedClientsIds)
            HandleAllInitialStateRequest(clientId);

        // --- Start timeline ---
        timeline.StartTimeline();

        Debug.Log("[ServerController] Server initialized successfully.");
    }

    // -------------------------------------------------------
    // Handlers (called by SyncedBridge)
    // -------------------------------------------------------

    public void HandleTestCell(ulong clientId, int x, int y)
    {
        bool walkable = session.Map.IsWalkable(x, y);
        bridge.SendTestCellResultClientRpc(clientId, x, y, walkable);
    }

    public void HandleMoveRequest(ulong clientId, int unitId, Vector3Int target)
    {
        var result = session.AuthorizeMove(clientId, unitId, target);

        if (result != ServerMatchSession.MoveResult.Ok)
        {
            Debug.LogWarning($"[Server] Move denied for client {clientId}: {result}");
            bridge.SendMoveDeniedClientRpc(clientId, unitId);
            return;
        }

        session.ApplyMove(unitId, target);
        Debug.Log($"[Server] Unit {unitId} moved to {target}");
        bridge.MoveConfirmedClientRpc(unitId, target);
    }

    public void HandleAllInitialStateRequest(ulong clientId)
    {
        Debug.Log($"[Server] Sending initial state to client {clientId}");

        SessionSnapshotData snapshot = new SessionSnapshotData
        {
            MapId             = session.MapId,
            Turn              = session.Turn,
            CurrentPlayerTurn = session.CurrentPlayerTurn,
            Teams             = session.teams,
            Units             = session.GetAllUnits(),
        };

        bridge.SendInitialStateClientRpc(snapshot, clientId);
    }
}