using Unity.Netcode;
public struct SkillUsageData : INetworkSerializable
{
    public int SkillId;
    public int UsageThisInstant;
    public int UsageTotal;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref SkillId);
        serializer.SerializeValue(ref UsageThisInstant);
        serializer.SerializeValue(ref UsageTotal);
    }
}