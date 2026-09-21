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
    public int gems;
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

// -------------------------------------------------------
// Gacha Summons
// -------------------------------------------------------

[System.Serializable]
public class RewardDto
{
    public string type; // "Unit" | "Weapon" | "Trinket" | "Gems"
    public int definitionId;
    public int classId;
    public int amount;
}

[System.Serializable]
public class BannerItemDto
{
    public RewardDto reward;
    public int weight;
    public bool isFeatured;
    public int rewardTier;
}

[System.Serializable]
public class PullOptionDto
{
    public string pullType; // "Single" | "Five" | "Ten"
    public int price;
}

[System.Serializable]
public class GachaBannerDto
{
    public string id;
    public string name;
    public string description;
    public BannerItemDto[] items;
    public PullOptionDto[] pullOptions;
    public bool isActive;
    public string startDate;
    public string expiryDate;
    public int pityThreshold;
    public string createdAt;
}

[System.Serializable]
public class GachaBannersWrapper
{
    public GachaBannerDto[] items;
}

[System.Serializable]
public class GachaPullRequestDto
{
    public string bannerId;
    public string pullType;
}

[System.Serializable]
public class GachaPullResultItemDto
{
    public RewardDto reward;
    public bool isDuplicate;
    public int compensationGems;
}

[System.Serializable]
public class GachaPullResponseDto
{
    public GachaPullResultItemDto[] results;
    public int gemsSpent;
    public int gemsRemaining;
}

[System.Serializable]
public class DropRateItemDto
{
    public RewardDto reward;
    public bool isFeatured;
    public int rewardTier;
    public double dropRate;
}

[System.Serializable]
public class BannerDropRatesDto
{
    public string bannerId;
    public string bannerName;
    public DropRateItemDto[] items;
}