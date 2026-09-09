using Unity.Netcode;

/// <summary>
/// Sent to ALL clients every 2 seconds during a paused instant.
/// Used purely for timer sync — no flag, no elite knowledge.
/// Clients derive whose turn it is from their own ClientMatchSession.
/// </summary>
public struct DecisionRequestData : INetworkSerializable
{
    public int Instant;             // which instant this decision belongs to
    public int DecisionTeam;        // which team is currently being asked (0 or 1)
    public float ActWaitDeadline;   // server time when act/wait window expires (5s)
    public float ActionDeadline;    // server time when action window expires (30s), -1 if not in action phase
    public float OvertimeTeam0;     // remaining overtime seconds for team 0
    public float OvertimeTeam1;     // remaining overtime seconds for team 1

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref Instant);
        serializer.SerializeValue(ref DecisionTeam);
        serializer.SerializeValue(ref ActWaitDeadline);
        serializer.SerializeValue(ref ActionDeadline);
        serializer.SerializeValue(ref OvertimeTeam0);
        serializer.SerializeValue(ref OvertimeTeam1);
    }
}