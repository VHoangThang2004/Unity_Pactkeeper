using Unity.Netcode;

/// <summary>
/// Sent to all clients on DecisionWaiting enter and every 2s resend.
/// Contains durations only — client counts down locally with their own delta time.
/// No server timestamps, no elite knowledge.
/// </summary>
public struct DecisionRequestData : INetworkSerializable
{
    public int Instant;          // which instant this decision belongs to
    public int DecisionTeam;     // which team is currently being asked (0 or 1)
    public int WaitDuration;  // seconds remaining for act/wait decision
    public int Overtime;    // remaining overtime seconds for current team

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref Instant);
        serializer.SerializeValue(ref DecisionTeam);
        serializer.SerializeValue(ref WaitDuration);
        serializer.SerializeValue(ref Overtime);
    }
}