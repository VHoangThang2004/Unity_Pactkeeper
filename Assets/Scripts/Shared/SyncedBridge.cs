using UnityEngine;
using Unity.Netcode;

public class SyncedBridge : NetworkBehaviour
{
    private ISyncedBridgeServer server;
    private ISyncedBridgeClient client;

    public void RegisterServer(ISyncedBridgeServer serverInstance) => server = serverInstance;
    public void RegisterClient(ISyncedBridgeClient clientInstance) => client = clientInstance;

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            foreach (var mb in FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Exclude))
            {
                if (mb is ISyncedBridgeServer s)
                {
                    RegisterServer(s);
                    s.OnBridgeReady(this);
                    break;
                }
            }
        }
        else
        {
            foreach (var mb in FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Exclude))
            {
                if (mb is ISyncedBridgeClient c)
                {
                    RegisterClient(c);
                    c.OnBridgeReady(this);
                    break;
                }
            }
        }
    }

    // -------------------------------------------------------
    // CLIENT -> SERVER
    // -------------------------------------------------------

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void RequestInitialStateServerRpc(RpcParams rpcParams = default)
    {
        server?.HandleAllInitialStateRequest(rpcParams.Receive.SenderClientId);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void SendDecisionServerRpc(int unitId, Vector3Int target, DecisionType decisionType, int skillCardId, int clientToken, RpcParams rpcParams = default)
    {
        server?.HandleDecision(rpcParams.Receive.SenderClientId, unitId, target, decisionType, skillCardId, clientToken);
    }

    // -------------------------------------------------------
    // SERVER -> ALL CLIENTS
    // -------------------------------------------------------

    [ClientRpc]
    public void SpawnUnitClientRpc(UnitData unitData, ClientRpcParams clientRpcParams = default)
    {
        client?.OnUnitSpawned(unitData);
    }

    [ClientRpc]
    public void BroadcastTimelineTickClientRpc(TimelineData timelineData)
    {
        client?.OnTimelineTick(timelineData);
    }

    [ClientRpc]
    public void SendSnapshotToClientRpc(SessionSnapshotData before, ResolveData resolve, SecretData secret, DecisionRequestData decision, int token, ClientRpcParams clientRpcParams = default)
    {
        client?.OnSnapshotReceived(before, resolve, secret, decision, token);
    }
}