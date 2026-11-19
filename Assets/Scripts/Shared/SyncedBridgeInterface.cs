using Unity.Netcode;
using UnityEngine;

// Shared interfaces that SyncedBridge can safely see
// Lets Shared code (ServerBootstrap) end the match without referencing the Server assembly (ServerBackendClient)
public interface IMatchEnder
{
    void EndMatch(string reason);
}

public interface ISyncedBridgeServer
{
    void HandleAllInitialStateRequest(ulong senderClientId);
    void HandleDecision(ulong senderClientId, int unitId, Vector3Int target, DecisionType decisionType, int skillCardId, int clientToken);
    void HandleAdvanceTutorialPhase(int newSceneId, bool forceCleanReset = false);
    void HandlePrepSubmit(ulong senderClientId, int[] uIds, int[] spawnIndices, bool ready);
    void HandleForfeit(ulong senderClientId);
    void OnBridgeReady(SyncedBridge bridge);
}

public interface ISyncedBridgeClient
{
    void OnUnitSpawned(UnitData unitData);
    void OnTimelineTick(TimelineData timelineData);
    void OnSnapshotReceived(SessionSnapshotData before, ResolveData resolve, SecretData secret, DecisionRequestData decision, int token);
    void OnPrepInfo(string mapId, int teamId, float secondsLeft);
    void OnPrepEnded();
    void OnStoryMatchEnded(bool playerWon, string failReason, int instantsUsed, int unitsLost);
    void OnBridgeReady(SyncedBridge bridge);
}
