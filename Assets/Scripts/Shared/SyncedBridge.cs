using UnityEngine;
using Unity.Netcode;

public class SyncedBridge : NetworkBehaviour
{
    [SerializeField] private ClientController client;
    [SerializeField] private ServerController server;

    // -------------------------------------------------------
    // CLIENT -> SERVER
    // -------------------------------------------------------

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void RequestInitialStateServerRpc(RpcParams rpcParams = default)
    {
        server.HandleAllInitialStateRequest(rpcParams.Receive.SenderClientId);
    }

    /// <summary>
    /// Client sends decision. unitId = -1 means wait. Valid unitId means act with that unit moving to target.
    /// Invalid decisions (wrong unit, out of range, etc.) are silently ignored — timeout counts as wait.
    /// </summary>
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void SendDecisionServerRpc(int unitId, Vector3Int target, DecisionType decisionType, int skillCardId, int clientToken, RpcParams rpcParams = default)
    {
        server.HandleDecision(rpcParams.Receive.SenderClientId, unitId, target, decisionType, skillCardId, clientToken);
    }

    // -------------------------------------------------------
    // SERVER -> ALL CLIENTS
    // -------------------------------------------------------

    [ClientRpc]
    public void SpawnUnitClientRpc(UnitData unitData, ClientRpcParams clientRpcParams = default)
    {
        if (client == null) return;
        client.OnUnitSpawned(unitData);
    }

    [ClientRpc]
    public void BroadcastTimelineTickClientRpc(TimelineData timelineData)
    {
        if (client == null) return;
        client.OnTimelineTick(timelineData);
    }

    [ClientRpc]
    public void SendSnapshotToClientRpc(SessionSnapshotData snapshot, ResolveData resolve, SecretData secret, DecisionRequestData decision, int token, ClientRpcParams clientRpcParams = default)
    {
        if (client == null) return;
        client.OnSnapshotReceived(snapshot, resolve, secret, decision, token);
    }
}