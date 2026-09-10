using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Result of a single effect execution.
/// Collected into ResolveData so clients can replay exactly what happened.
/// Meaning: a move effect may be affected by many factors, but this results will help client replicate exactly the role of these factors
/// </summary>
public struct EffectResult : INetworkSerializable
{
    public EffectType Type;
    public int SourceUnitId;
    public int TargetUnitId;    // -1 if targeting a cell, not a unit
    public Vector3Int Cell;     // target cell
    public int Value;           // damage dealt, heal amount, steps moved, etc.

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        byte typeByte = (byte)Type;
        serializer.SerializeValue(ref typeByte);
        Type = (EffectType)typeByte;

        serializer.SerializeValue(ref SourceUnitId);
        serializer.SerializeValue(ref TargetUnitId);
        serializer.SerializeValue(ref Cell);
        serializer.SerializeValue(ref Value);
    }
}