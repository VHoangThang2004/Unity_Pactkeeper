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
    public ulong clientId;
    public List<PlayerUnitLoadout> units;
}

public class PlayerUnitLoadout
{
    public int uId;
    public int movementSkillId;
    public int weaponSkillId;
    public int classSkillId;
    public int equipmentSkillId; // shoes etc.
}   