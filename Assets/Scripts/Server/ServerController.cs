using System.Collections;
using Unity.Netcode;
using UnityEngine;

public class ServerController : MonoBehaviour
{
    [Header("Bridge")]
    [SerializeField] private SyncedBridge bridge;

    [SerializeField] ServerMatchSession session;
    [SerializeField] ServerSpawnManager spawnManager;

    /// <summary>
    /// SessionGuard is a safety mechanism to prevent the server from getting stuck in an infinite error loop, which can happen when there is an unexpected critical error in the code. It will monitor the number of errors that occur during the initialization phase and if it exceeds a certain threshold, it will force shutdown the server to prevent further damage.
    /// </summary>
    private int trialErrorCount = 0;
    IEnumerator SessionGuard()
    {
        while (true)
        {
            if (trialErrorCount > 10000)
            {
                //force shutdown server to protect server from infinite error loop, which can happen when there is an unexpected critical error in the code
                Debug.LogError("[ServerController] Too many errors, shutting down server to prevent further damage!");
                //TODO: sends clients to main menu
                //shuts down the server
                NetworkManager.Singleton.Shutdown();
                Application.Quit();
            }
            yield return new WaitForSeconds(60f);
            trialErrorCount = 0;
        }
    }

    private void Start()
    {
        StartCoroutine(SessionGuard());

        while (!session.Init())
        {
            Debug.LogError("[ServerController] Failed to initialize MatchSession, retrying...");
            trialErrorCount++;
        }

        while (!spawnManager.spawnAll())
        {
            Debug.LogError("[ServerController] Failed to initialize SpawnManager, retrying...");
            trialErrorCount++;
        }

        // Send snapshot to all connected clients
        foreach (ulong clientId in NetworkManager.Singleton.ConnectedClientsIds)
        {
            HandleAllInitialStateRequest(clientId);
        }
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
        Debug.Log($"[Server] Received initial state request from client {clientId}");

        SessionSnapshotData snapshot = new SessionSnapshotData
        {
            Turn = session.Turn,
            CurrentPlayerTurn = session.CurrentPlayerTurn,
            Teams = session.teams,
            Units = session.GetAllUnits()
        };

        bridge.SendInitialStateClientRpc(snapshot, clientId);
    }
}