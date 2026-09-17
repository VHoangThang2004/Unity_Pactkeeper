using System.Collections.Generic;
using UnityEngine;

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
    public int PassiveSkillId;
    public int MovementSkillId;
    public int WeaponSkillId;
    public int ClassSkillId;
    public int TrinketSkillId;
    public int MaxHP;
    public int MaxSkillPoint;
    public int Speed;
    public float DamageMultiplier;
    public float DamageReduction;
}   