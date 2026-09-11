using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public struct CurrentPatterns : INetworkSerializable
{
    public int SkillId;
    public Vector3Int[] TargetCells;  // world positions — translated + filtered by server
    public Vector2Int[] AoePattern;   // offsets — client rotates on hover

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref SkillId);

        if (serializer.IsReader)
        {
            int count = 0;
            serializer.SerializeValue(ref count);
            TargetCells = new Vector3Int[count];
            for (int i = 0; i < count; i++)
                serializer.SerializeValue(ref TargetCells[i]);

            serializer.SerializeValue(ref count);
            AoePattern = new Vector2Int[count];
            for (int i = 0; i < count; i++)
                serializer.SerializeValue(ref AoePattern[i]);
        }
        else
        {
            int count = TargetCells?.Length ?? 0;
            serializer.SerializeValue(ref count);
            if (TargetCells != null)
                for (int i = 0; i < count; i++)
                    serializer.SerializeValue(ref TargetCells[i]);

            count = AoePattern?.Length ?? 0;
            serializer.SerializeValue(ref count);
            if (AoePattern != null)
                for (int i = 0; i < count; i++)
                    serializer.SerializeValue(ref AoePattern[i]);
        }
    }
}