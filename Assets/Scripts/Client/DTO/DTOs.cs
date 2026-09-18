using System.Collections.Generic;
using UnityEngine;

// -------------------------------------------------------
// Profile & Inventory
// -------------------------------------------------------

[System.Serializable]
public class PlayerProfileResponse
{
    public string id;
    public string playerId;
    public string username;
    public int level;
    public int experience;
    public OwnedUnitDto[] ownedUnits;
    public OwnedWeaponDto[] ownedWeapons;
    public OwnedTrinketDto[] ownedTrinkets;
}

[System.Serializable]
public class OwnedUnitDto
{
    public string ownedUnitId;
    public int unitDefinitionUId;
    public int grade;
    public int[] unlockedClassIds;
    public int equippedMovementSkillId;
    public string equippedOwnedWeaponId;
    public string equippedOwnedTrinketId;
    public int equippedClassSkillId;
}

[System.Serializable]
public class OwnedWeaponDto
{
    public string ownedWeaponId;
    public int weaponDefinitionId;
}

[System.Serializable]
public class OwnedTrinketDto
{
    public string ownedTrinketId;
    public int trinketDefinitionId;
}

// -------------------------------------------------------
// Unit Config
// -------------------------------------------------------

[System.Serializable]
public class UnitConfigDto
{
    public string ownedUnitId;
    public int uId;
    public int grade;
    public int passiveSkillId;
    public int equippedMovementSkillId;
    public int equippedClassSkillId;
    public UnitGradeStatsDto gradeStats;
    public EquippedEquipmentDataDto equippedWeapon;
    public EquippedEquipmentDataDto equippedTrinket;
}

[System.Serializable]
public class UnitGradeStatsDto
{
    public int maxHP;
    public int maxSkillPoint;
    public int speed;
    public int damageMultiplier;
    public int damageReduction;
}

[System.Serializable]
public class EquippedEquipmentDataDto
{
    public string ownedItemId;  //stupid backend, doesnt pass this, so we're gonna use elsewhere to get this data
    public int definitionId;
    public int skillId;
    public int maxHP;
    public int maxSkillPoint;
    public int speed;
    public int damageMultiplier;
    public int damageReduction;
}

// -------------------------------------------------------
// Equipment Definitions
// -------------------------------------------------------

[System.Serializable]
public class WeaponDefinitionDto
{
    public int weaponId;
    public string name;
    public int classId;
    public int skillId;
    public EquipmentStatModifiersDto statModifiers;
}

[System.Serializable]
public class TrinketDefinitionDto
{
    public int trinketId;
    public string name;
    public int skillId;
    public EquipmentStatModifiersDto statModifiers;
}

[System.Serializable]
public class EquipmentStatModifiersDto
{
    public int maxHP;
    public int speed;
    public int maxSkillPoint;
    public int damageMultiplier;
    public int damageReduction;
}

// -------------------------------------------------------
// Skill & Class
// -------------------------------------------------------

[System.Serializable]
public class ClassSkillResultDto
{
    public int classId;
    public int skillId;
}

// -------------------------------------------------------
// Owned Equipment Results
// -------------------------------------------------------

[System.Serializable]
public class OwnedWeaponResultDto
{
    public string ownedWeaponId;
    public int weaponDefinitionId;
    public bool isEquipped;
}

[System.Serializable]
public class OwnedTrinketResultDto
{
    public string ownedTrinketId;
    public int trinketDefinitionId;
    public bool isEquipped;
}