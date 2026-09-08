using UnityEngine;
using Unity.Netcode;

public class SyncedBridge : NetworkBehaviour
{
    [SerializeField] private ClientController client;
    [SerializeField] private ServerController server;

    // -------------------------------------------------------
    // CLIENT → SERVER
    // -------------------------------------------------------

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void SendMoveServerRpc(int unitId, Vector3Int target, RpcParams rpcParams = default)
    {
        server.HandleMoveRequest(rpcParams.Receive.SenderClientId, unitId, target);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void TestCellServerRpc(int x, int y, RpcParams rpcParams = default)
    {
        server.HandleTestCell(rpcParams.Receive.SenderClientId, x, y);
    }


    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void RequestInitialStateServerRpc(RpcParams rpcParams = default)
    {
        server.HandleAllInitialStateRequest(rpcParams.Receive.SenderClientId);
    }

    // -------------------------------------------------------
    // SERVER → ONE CLIENT
    // -------------------------------------------------------

    [ClientRpc]
    public void SendTestCellResultClientRpc(ulong clientId, int x, int y, bool walkable, ClientRpcParams clientRpcParams = default)
    {
        // Only the target client processes this
        if (NetworkManager.Singleton.LocalClientId != clientId) return;
        client.OnServerResult(x, y, walkable);
    }

    [ClientRpc]
    public void SendMoveDeniedClientRpc(ulong clientId, int unitId, ClientRpcParams clientRpcParams = default)
    {
        if (NetworkManager.Singleton.LocalClientId != clientId) return;
        Debug.LogWarning($"[Bridge] Move denied for unit {unitId}");
        // TODO: client.OnMoveDenied(unitId) — show feedback to player
    }

    [ClientRpc]
    public void SendInitialStateClientRpc(SessionSnapshotData snapshot, ulong clientId, ClientRpcParams clientRpcParams = default)
    {
        if (NetworkManager.Singleton.LocalClientId != clientId) return;
        client.OnInitialStateReceived(snapshot);
    }

    // -------------------------------------------------------
    // SERVER → ALL CLIENTS
    // -------------------------------------------------------

    [ClientRpc]
    public void SpawnUnitClientRpc(UnitData unitData, ClientRpcParams clientRpcParams = default)
    {
        if (client == null) return; // skip if no client (headless server)
        client.OnUnitSpawned(unitData);
    }

    [ClientRpc]
    public void MoveConfirmedClientRpc(int unitId, Vector3Int target)
    {
        if (client == null) return;
        client.OnMoveConfirmed(unitId, target);
    }
}