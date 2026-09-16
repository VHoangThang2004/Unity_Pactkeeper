using System.Collections.Generic;
using Unity.Netcode;

public struct TimelineData : INetworkSerializable
{
    public int currentInstant;
    public int maxInstant;
    public int flag;
    public int consecutivePassInstants;
    public bool isPaused;
    public string[] timelinelog; // for client to know what was-is going on.
    //removed ready units (client can process the data themselves, units data already included in session snapshot data)

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref currentInstant);
        serializer.SerializeValue(ref maxInstant);
        serializer.SerializeValue(ref flag);
        serializer.SerializeValue(ref consecutivePassInstants);
        serializer.SerializeValue(ref isPaused);
        
        if (serializer.IsReader)
        {
            int count = 0;
            serializer.SerializeValue(ref count);
            timelinelog = new string[count];
            for (int i = 0; i < count; i++)
            {
                string s = string.Empty;
                serializer.SerializeValue(ref s);
                timelinelog[i] = s;
            }
        }
        else
        {
            int count = timelinelog?.Length ?? 0;
            serializer.SerializeValue(ref count);
            if (timelinelog != null)
                for (int i = 0; i < count; i++)
                {
                    string s = timelinelog[i] ?? string.Empty;
                    serializer.SerializeValue(ref s);
                }
        }
    }
}