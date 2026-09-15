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
    public int TeamId;
    public ulong ClientId;
    public string PlayerId = string.Empty;
    public List<PlayerUnitLoadout> Units;
}

public class PlayerUnitLoadout
{
    public int UId;
    public int MovementSkillId;
    public int WeaponSkillId;
    public int ClassSkillId;
    public int EquipmentSkillId; // shoes etc.
}   