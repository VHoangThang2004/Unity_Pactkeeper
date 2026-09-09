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
    public int ActWaitDuration;  // seconds remaining for act/wait decision (-1 if not in act/wait phase)
    public int ActionDuration;   // seconds remaining for action decision (-1 if not in action phase)
    public int OvertimeTeam0;    // remaining overtime seconds for team 0
    public int OvertimeTeam1;    // remaining overtime seconds for team 1

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref Instant);
        serializer.SerializeValue(ref DecisionTeam);
        serializer.SerializeValue(ref ActWaitDuration);
        serializer.SerializeValue(ref ActionDuration);
        serializer.SerializeValue(ref OvertimeTeam0);
        serializer.SerializeValue(ref OvertimeTeam1);
    }
}