using System.Collections.Generic;

/// <summary>
/// Static registry — passes clientId→playerId map from LobbyManager to ServerMatchSession.
/// </summary>
public static class MatchIdentityRegistry
{
    public static Dictionary<ulong, string> clientPlayerMap = new Dictionary<ulong, string>();

    public static string GetPlayerId(ulong clientId)
    {
        clientPlayerMap.TryGetValue(clientId, out string playerId);
        return playerId ?? string.Empty;
    }
}