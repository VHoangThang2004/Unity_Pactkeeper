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

    /// <summary>Client responds to act/wait prompt.</summary>
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void SendActWaitDecisionServerRpc(bool wantsToAct, RpcParams rpcParams = default)
    {
        server.HandleActWaitDecision(rpcParams.Receive.SenderClientId, wantsToAct);
    }

    /// <summary>Client submits which unit to act with and where to move.</summary>
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void SendActionDecisionServerRpc(int unitId, Vector3Int target, RpcParams rpcParams = default)
    {
        server.HandleActionDecision(rpcParams.Receive.SenderClientId, unitId, target);
    }

    // -------------------------------------------------------
    // SERVER → ONE CLIENT
    // -------------------------------------------------------

    [ClientRpc]
    public void SendTestCellResultClientRpc(ulong clientId, int x, int y, bool walkable, ClientRpcParams clientRpcParams = default)
    {
        if (NetworkManager.Singleton.LocalClientId != clientId) return;
        client.OnServerResult(x, y, walkable);
    }

    [ClientRpc]
    public void SendMoveDeniedClientRpc(ulong clientId, int unitId, ClientRpcParams clientRpcParams = default)
    {
        if (NetworkManager.Singleton.LocalClientId != clientId) return;
        Debug.LogWarning($"[Bridge] Move denied for unit {unitId}");
        // TODO: client.OnMoveDenied(unitId)
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
        if (client == null) return;
        client.OnUnitSpawned(unitData);
    }

    [ClientRpc]
    public void MoveConfirmedClientRpc(int unitId, Vector3Int target)
    {
        if (client == null) return;
        client.OnMoveConfirmed(unitId, target);
    }

    /// <summary>
    /// Broadcast every instant tick. All clients update their timeline display.
    /// </summary>
    [ClientRpc]
    public void BroadcastTimelineTickClientRpc(TimelineData timelineData)
    {
        if (client == null) return;
        client.OnTimelineTick(timelineData);
    }

    /// <summary>
    /// Broadcast every 2 seconds during a paused instant for timer sync.
    /// Also sent immediately when the active decision team changes.
    /// </summary>
    [ClientRpc]
    public void BroadcastDecisionRequestClientRpc(DecisionRequestData data)
    {
        if (client == null) return;
        client.OnDecisionRequest(data);
    }

    /// <summary>
    /// Sent to all clients when a unit's step resets after acting.
    /// </summary>
    [ClientRpc]
    public void BroadcastUnitStepResetClientRpc(int unitId, int newStep)
    {
        if (client == null) return;
        client.OnUnitStepReset(unitId, newStep);
    }

    /// <summary>
    /// Mid snapshot bundled with decision request — sent on every DecisionWaiting enter.
    /// All clients sync unit state before any decision is submitted.
    /// </summary>
    [ClientRpc]
    public void BroadcastMidSnapshotWithDecisionClientRpc(SessionSnapshotData snapshot, DecisionRequestData decision)
    {
        if (client == null) return;
        client.OnMidSnapshotWithDecision(snapshot, decision);
    }

    /// <summary>
    /// Full snapshot broadcast — sent on match end or full resync.
    /// </summary>
    [ClientRpc]
    public void BroadcastSnapshotClientRpc(SessionSnapshotData snapshot)
    {
        if (client == null) return;
        client.OnInitialStateReceived(snapshot);
    }
}