using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Defines both teams' loadouts for a specific story battle scene.
/// Asset name convention: matches the scene it serves, e.g. "Encounter_C0_S1".
/// One asset per (chapterId, sceneId) battle scene.
///
/// playerUnits: only used for scenes where the real player has no configured
/// loadout yet (e.g. Chapter 0 Scene 1 — brand new player, no owned units).
/// Leave empty for later story scenes that should use the player's real,
/// backend-fetched loadout instead.
/// </summary>
[CreateAssetMenu(fileName = "Encounter_C0_S0", menuName = "SRPG/Story Encounter Config")]
public class StoryEncounterConfig : ScriptableObject
{
    [Header("Identity")]
    public int chapterId;
    public int sceneId;

    [Header("Player Team (optional)")]
    [Tooltip("If non-empty, the server uses this preset loadout for the player " +
             "instead of fetching from the backend. Used for Chapter 0 Scene 1 " +
             "where the player has no configured units yet. Leave empty for " +
             "scenes that should use the player's real loadout.")]
    public List<StoryUnitEntry> playerUnits = new();

    [Header("AI Team")]
    [Tooltip("Units the AI team spawns with. Same shape as a real player's loadout.")]
    public List<StoryUnitEntry> aiUnits = new();

    [Header("Behaviour")]
    [Tooltip("If true, the AI always chooses Wait regardless of board state. " +
             "Used for early mechanics tutorials where the player should not be threatened.")]
    public bool alwaysWait = true;

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
        {
            result.Add(new PlayerUnitLoadout
            {
                UId = u.uId,
                PassiveSkillId = u.passiveSkillId,
                MovementSkillId = u.movementSkillId,
                WeaponSkillId = u.weaponSkillId,
                ClassSkillId = u.classSkillId,
                TrinketSkillId = u.trinketSkillId,
                MaxHP = u.maxHP,
                MaxSkillPoint = u.maxSkillPoint,
                Speed = u.speed,
                DamageMultiplier = u.damageMultiplier,
                DamageReduction = u.damageReduction,
            });
        }

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
    public int maxSkillPoint;
    public int speed;
    public int damageMultiplier;
    public int damageReduction;
}