using Unity.Netcode;

// Struct this use for sending data through network (must use struct + INetworkSerializable)
public struct NetUnitLoadout : INetworkSerializable
{
    public int uId;
    public int movementSkillId;
    public int weaponSkillId;
    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref uId);
        serializer.SerializeValue(ref movementSkillId);
        serializer.SerializeValue(ref weaponSkillId);
    }
}
