using Unity.Netcode;

/// <summary>
/// Per-client secret data — sent only to the owning client.
/// Placeholder for now (skill card hand, hidden stats, etc.)
/// Lives in ServerMatchSession separately from TeamData — never accidentally broadcast.
/// </summary>
public struct SecretData : INetworkSerializable
{
    public string Data; // placeholder — replace with proper fields when designed

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref Data);
    }
}