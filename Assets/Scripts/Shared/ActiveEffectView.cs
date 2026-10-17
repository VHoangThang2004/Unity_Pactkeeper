using Unity.Netcode;

/// <summary>
/// What the client may know about one active effect on a unit — state only, never formulas.
/// Built server-side from ActiveEffectInstance (ServerMatchSession.SyncEffectViews).
/// </summary>
public struct ActiveEffectView : INetworkSerializable
{
    public int EffectId;
    // Instant at whose start the effect is removed, -1 = permanent.
    // Absolute (not "remaining") so it stays correct between full snapshots — only cheap ticks arrive then.
    public int ExpiresAtInstant;

    public bool IsPermanent => ExpiresAtInstant < 0;
    public int RemainingAt(int currentInstant) => IsPermanent ? -1 : System.Math.Max(0, ExpiresAtInstant - currentInstant);

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref EffectId);
        serializer.SerializeValue(ref ExpiresAtInstant);
    }
}
