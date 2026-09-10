using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Pre-match team configuration. Server-only — never sent to clients.
/// Testing phase: hardcoded in ServerMatchSession.Init().
/// Backend phase: received from server backend, replaces hardcoded data entirely.
/// Defines who is on the team and what units they bring to the match.
/// </summary>
[System.Serializable]
public class TeamLoadout
{
    public int teamId;
    public ulong clientId;          // player's network id — known from backend in real case
    public List<int> unitUIds;      // unit definition ids to spawn (uId references UnitDefinition)
    //expected to have more data (about unit configurations, skins data, effect skins data, etc. all related to each player preference setting from lobby)
}