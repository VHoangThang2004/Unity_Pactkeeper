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
    public int RemainingWaitDuration;  // seconds remaining for act/wait decision
    public int MaxWaitDuration;  // max wait duration given per instant
    public int RemainingOvertime;    // remaining overtime seconds for current team
    public int MaxOvertime;  // max overtime given for whole match session

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref Instant);
        serializer.SerializeValue(ref DecisionTeam);
        serializer.SerializeValue(ref RemainingWaitDuration);
        serializer.SerializeValue(ref RemainingOvertime);
        serializer.SerializeValue(ref MaxWaitDuration);
        serializer.SerializeValue(ref MaxOvertime);
    }
}