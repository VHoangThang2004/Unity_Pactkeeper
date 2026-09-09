using System.Collections.Generic;
using Unity.Netcode;

public struct TimelineData : INetworkSerializable
{
    public int currentInstant;
    public int maxInstant;
    public int flag;
    public int consecutiveWaits;
    public bool isPaused;
    public List<int> readyUnitIds;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref currentInstant);
        serializer.SerializeValue(ref maxInstant);
        serializer.SerializeValue(ref flag);
        serializer.SerializeValue(ref consecutiveWaits);
        serializer.SerializeValue(ref isPaused);

        if (serializer.IsReader)
        {
            int count = 0;
            serializer.SerializeValue(ref count);
            readyUnitIds = new List<int>(count);
            for (int i = 0; i < count; i++)
            {
                int id = 0;
                serializer.SerializeValue(ref id);
                readyUnitIds.Add(id);
            }
        }
        else
        {
            int count = readyUnitIds?.Count ?? 0;
            serializer.SerializeValue(ref count);
            if (readyUnitIds != null)
                foreach (var id in readyUnitIds)
                {
                    int idCopy = id;
                    serializer.SerializeValue(ref idCopy);
                }
        }
    }
}