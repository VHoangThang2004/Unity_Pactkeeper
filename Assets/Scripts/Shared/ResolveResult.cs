using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public struct ResolveResult : INetworkSerializable
{
    public int EffectId;
    public int SourceUnitId;         // -1 if source is a cell
    public Vector3Int SourceCell;
    public List<Vector3Int> TargetCells;
    public SessionSnapshotData Partial;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref EffectId);
        serializer.SerializeValue(ref SourceUnitId);
        serializer.SerializeValue(ref SourceCell);

        if (serializer.IsReader)
        {
            int count = 0;
            serializer.SerializeValue(ref count);
            TargetCells = new List<Vector3Int>(count);
            for (int i = 0; i < count; i++)
            {
                Vector3Int cell = default;
                serializer.SerializeValue(ref cell);
                TargetCells.Add(cell);
            }
        }
        else
        {
            int count = TargetCells?.Count ?? 0;
            serializer.SerializeValue(ref count);
            if (TargetCells != null)
                foreach (var cell in TargetCells)
                {
                    Vector3Int c = cell;
                    serializer.SerializeValue(ref c);
                }
        }

        Partial.NetworkSerialize(serializer);
    }
}