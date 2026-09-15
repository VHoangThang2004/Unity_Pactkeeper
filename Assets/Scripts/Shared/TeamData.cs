using System.Collections.Generic;
using Unity.Netcode;

public class TeamData : INetworkSerializable
{
    public int teamId;
    public ulong clientId;
    // Server only — not serialized, never sent to clients
    [System.NonSerialized] public string playerId = string.Empty;
    public List<int> unitIds;

    // Instant decision data
    public int WaitDuration;
    public int Overtime;
    public bool isInstantEnded;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref teamId);
        serializer.SerializeValue(ref clientId);
        serializer.SerializeValue(ref WaitDuration);
        serializer.SerializeValue(ref Overtime);
        serializer.SerializeValue(ref isInstantEnded);

        if (serializer.IsReader)
        {
            int count = 0;
            serializer.SerializeValue(ref count);
            unitIds = new List<int>(count);
            for (int i = 0; i < count; i++)
            {
                int id = 0;
                serializer.SerializeValue(ref id);
                unitIds.Add(id);
            }
        }
        else
        {
            int count = unitIds?.Count ?? 0;
            serializer.SerializeValue(ref count);
            if (unitIds != null)
                foreach (int id in unitIds)
                {
                    int copy = id;
                    serializer.SerializeValue(ref copy);
                }
        }
    }
}