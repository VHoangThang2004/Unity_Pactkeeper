using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Describes a resolved action for client to animate.
/// Contains the decision, all effect results, and the final game state.
/// Client: apply BeforeSnapshot -> replay effects -> apply AfterSnapshot.
/// </summary>
public struct ResolveData : INetworkSerializable
{
    public bool HasResolve;

    // The decision
    public int UnitId;
    public Vector3Int Target;
    public DecisionType decision;
    public int SkillCardId;

    // Effect results — one per effect in the action definition
    // Client uses these to replay animations in order
    public List<EffectResult> EffectResults;

    // Final state after all effects applied
    public SessionSnapshotData AfterSnapshot;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref HasResolve);
        serializer.SerializeValue(ref UnitId);
        serializer.SerializeValue(ref Target);

        byte actionByte = (byte)decision;
        serializer.SerializeValue(ref actionByte);
        decision = (DecisionType)actionByte;

        serializer.SerializeValue(ref SkillCardId);

        // EffectResults
        if (serializer.IsReader)
        {
            int count = 0;
            serializer.SerializeValue(ref count);
            EffectResults = new List<EffectResult>(count);
            for (int i = 0; i < count; i++)
            {
                var result = new EffectResult();
                result.NetworkSerialize(serializer);
                EffectResults.Add(result);
            }
        }
        else
        {
            int count = EffectResults?.Count ?? 0;
            serializer.SerializeValue(ref count);
            if (EffectResults != null)
                foreach (var r in EffectResults)
                {
                    var copy = r;
                    copy.NetworkSerialize(serializer);
                }
        }
        if (!serializer.IsReader)
            AfterSnapshot.MapId = AfterSnapshot.MapId ?? string.Empty;
        AfterSnapshot.NetworkSerialize(serializer);
    }
}