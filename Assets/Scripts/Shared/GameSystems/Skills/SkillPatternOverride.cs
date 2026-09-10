using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public struct SkillPatternOverride : INetworkSerializable
{
    public int SkillId;
    public Vector2Int[] TargetPatternCells; // modified target pattern
    public Vector2Int[] AoEPatternCells;    // modified aoe pattern

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref SkillId);

        if (serializer.IsReader)
        {
            int count = 0;
            serializer.SerializeValue(ref count);
            TargetPatternCells = new Vector2Int[count];
            for (int i = 0; i < count; i++)
                serializer.SerializeValue(ref TargetPatternCells[i]);

            serializer.SerializeValue(ref count);
            AoEPatternCells = new Vector2Int[count];
            for (int i = 0; i < count; i++)
                serializer.SerializeValue(ref AoEPatternCells[i]);
        }
        else
        {
            int count = TargetPatternCells?.Length ?? 0;
            serializer.SerializeValue(ref count);
            if (TargetPatternCells != null)
                for (int i = 0; i < count; i++)
                    serializer.SerializeValue(ref TargetPatternCells[i]);

            count = AoEPatternCells?.Length ?? 0;
            serializer.SerializeValue(ref count);
            if (AoEPatternCells != null)
                for (int i = 0; i < count; i++)
                    serializer.SerializeValue(ref AoEPatternCells[i]);
        }
    }
}