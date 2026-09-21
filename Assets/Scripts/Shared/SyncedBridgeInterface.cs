using Unity.Netcode;
using UnityEngine;

// Shared interfaces that SyncedBridge can safely see
public interface ISyncedBridgeServer
{
    void HandleAllInitialStateRequest(ulong senderClientId);
    void HandleDecision(ulong senderClientId, int unitId, Vector3Int target, DecisionType decisionType, int skillCardId, int clientToken);
    void OnBridgeReady(SyncedBridge bridge);

}

public interface ISyncedBridgeClient
{
    void OnUnitSpawned(UnitData unitData);
    void OnTimelineTick(TimelineData timelineData);
    void OnSnapshotReceived(SessionSnapshotData before, ResolveData resolve, SecretData secret, DecisionRequestData decision, int token);
    void OnBridgeReady(SyncedBridge bridge);
}