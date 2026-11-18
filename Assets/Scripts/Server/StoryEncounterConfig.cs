using System.Collections.Generic;
using UnityEngine;

public enum StoryAIMode
{
    AlwaysWait = 0,            // Default for legacy scripted tutorials (C0_S1..C3_S2)
    ScriptedThenTactical = 1,  // Hybrid: scripted opening steps, then Tactical AI
    Tactical = 2               // Full Tactical AI from Instant 0
}

public enum AIBehaviorProfile
{
    Balanced = 0,
    Aggressive = 1,
    Defensive = 2,
    Support = 3
}

[System.Serializable]
public class ScriptedAIAction
{
    [Tooltip("Instant at which this scripted AI command triggers.")]
    public int instantIndex;

    [Tooltip("Index in aiUnits (-1 = first ready AI unit).")]
    public int unitSlotIndex = 0;

    [Tooltip("Which skill slot to execute (None = AI Wait for this turn).")]
    public SkillSlot skillSlot = SkillSlot.None;

    [Tooltip("If true, targets customTargetCell; otherwise picks best valid cell for that skill.")]
    public bool hasCustomTarget = false;
    public Vector2Int customTargetCell;
}

[System.Serializable]
public class ChallengeRules
{
    [Tooltip("Optional max instant limit for this encounter (0 = use MatchConfig.maxInstant).")]
    public int maxInstantsLimit = 0;

    [Tooltip("Max ally deaths allowed (-1 = unlimited, 0 = no allies may fall).")]
    public int maxAllyDeathsAllowed = -1;
}

/// <summary>
/// Defines both teams' loadouts and AI/challenge behavior for a story battle scene.
/// Asset name convention: matches the scene it serves, e.g. "Encounter_C0_S1".
/// </summary>
[CreateAssetMenu(fileName = "Encounter_C0_S0", menuName = "SRPG/Story Encounter Config")]
public class StoryEncounterConfig : ScriptableObject
{
    [Header("Identity")]
    public int chapterId;
    public int sceneId;

    [Header("Map")]
    [Tooltip("The mapId from MapRegistry to use for this encounter. If empty, falls back to default.")]
    public string mapId;

    [Header("Player Team (optional)")]
    [Tooltip("If non-empty, the server uses this preset loadout for the player " +
             "instead of fetching from the backend. Leave empty for " +
             "scenes that should use the player's real loadout (triggers PrepPhase).")]
    public List<StoryUnitEntry> playerUnits = new();

    [Header("AI Team")]
    [Tooltip("Units the AI team spawns with. Same shape as a real player's loadout.")]
    public List<StoryUnitEntry> aiUnits = new();

    [Header("AI Behaviour")]
    [Tooltip("Legacy toggle kept for backward compatibility with existing Encounter assets.")]
    public bool alwaysWait = true;

    [Tooltip("AlwaysWait (0) preserves existing scripted tutorials. Use ScriptedThenTactical for Hybrid, or Tactical for Challenge.")]
    public StoryAIMode aiMode = StoryAIMode.AlwaysWait;

    public AIBehaviorProfile behaviorProfile = AIBehaviorProfile.Balanced;

    [Tooltip("Scripted opening commands executed before switching to Tactical AI (used when aiMode == ScriptedThenTactical).")]
    public List<ScriptedAIAction> scriptedActions = new();

    [Header("Challenge Win/Fail Rules (Optional)")]
    public ChallengeRules challengeRules = new();

    public StoryAIMode GetEffectiveAIMode()
    {
        if (aiMode != StoryAIMode.AlwaysWait)
            return aiMode;

        return alwaysWait ? StoryAIMode.AlwaysWait : StoryAIMode.Tactical;
    }

    public bool HasPresetPlayerLoadout => playerUnits != null && playerUnits.Count > 0;

    public TeamLoadout ToPlayerTeamLoadout(string playerId)
    {
        return BuildLoadout(teamId: 0, playerId: playerId, units: playerUnits);
    }

    public TeamLoadout ToAITeamLoadout()
    {
        return BuildLoadout(teamId: 1, playerId: "AI", units: aiUnits);
    }

    private TeamLoadout BuildLoadout(int teamId, string playerId, List<StoryUnitEntry> units)
    {
        var result = new List<PlayerUnitLoadout>();
        foreach (var u in units)
            result.Add(u.ToUnitLoadout());

        return new TeamLoadout
        {
            TeamId = teamId,
            ClientId = 0, // assigned later by ServerMatchSession.SetTeamFromLoadout
            PlayerId = playerId,
            Units = result
        };
    }
}

[System.Serializable]
public class StoryUnitEntry
{
    public int uId;
    public int passiveSkillId;
    public int movementSkillId;
    public int weaponSkillId;
    public int classSkillId;
    public int trinketSkillId;
    public int maxHP;
    [Tooltip("Starting HP when this encounter begins. If <= 0, defaults to maxHP.")]
    public int currentHP;
    public int maxSkillPoint;
    [Tooltip("Starting Skill Point when this encounter begins.")]
    public int currentSkillPoint;
    public int speed;
    public int damageMultiplier;
    public int damageReduction;

    public int GetStartingHP() => currentHP > 0 ? Mathf.Min(currentHP, maxHP) : maxHP;
    public int GetStartingSP() => Mathf.Clamp(currentSkillPoint, 0, maxSkillPoint);

    // Single conversion point — used by encounter loadouts and mid-match tutorial spawns
    public PlayerUnitLoadout ToUnitLoadout() => new PlayerUnitLoadout
    {
        UId = uId,
        PassiveSkillId = passiveSkillId,
        MovementSkillId = movementSkillId,
        WeaponSkillId = weaponSkillId,
        ClassSkillId = classSkillId,
        TrinketSkillId = trinketSkillId,
        MaxHP = maxHP,
        CurrentHP = GetStartingHP(),
        MaxSkillPoint = maxSkillPoint,
        CurrentSkillPoint = GetStartingSP(),
        Speed = speed,
        DamageMultiplier = damageMultiplier,
        DamageReduction = damageReduction,
    };
}
