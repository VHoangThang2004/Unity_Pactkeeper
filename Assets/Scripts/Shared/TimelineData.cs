using System.Collections.Generic;
using Unity.Netcode;

public struct TimelineData : INetworkSerializable
{
    public int currentInstant;
    public int maxInstant;
    public int flag;
    public int consecutiveWaits;
    public bool isPaused;
    //removed ready units (client can process the data themselves, units data already included in session snapshot data)

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref currentInstant);
        serializer.SerializeValue(ref maxInstant);
        serializer.SerializeValue(ref flag);
        serializer.SerializeValue(ref consecutiveWaits);
        serializer.SerializeValue(ref isPaused);
    }
}