using UnityEngine;
using Unity.VisualScripting;
using System.Collections.Generic;

public class ServerSpawnManager : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private SyncedBridge bridge;
    [SerializeField] private ServerMatchSession session;
    [SerializeField] private SpeedConfig speedConfig;

    private bool suppressSpawnRpcs = true;
    private int nextUnitId = 1;

    public bool spawnAll()
    {
        if (!SpawnTeam(0)) return false;
        if (!SpawnTeam(1)) return false;
        RecalculateAll();
        suppressSpawnRpcs = false;
        return true;
    }

    public void RecalculateAll()
    {
        foreach (var unit in session.units)
        {
            var def = session.unitLibrary.Get(unit.UId);
            if (def == null) continue;
            var activeEffects = session.GetUnitActiveEffects(unit.Id);
            UnitRecalculator.Recalculate(unit, def, activeEffects);
            RecalculateSkillPatterns(unit);
        }
        Debug.Log($"[SpawnManager] All units recalculated.");
    }

    public bool SpawnTeam(int teamId)
    {
        var loadout = session.GetTeamLoadoutDataByTeamId(teamId);
        if (loadout == null)
        {
            Debug.LogError($"[SpawnManager] No loadout for team {teamId}!");
            return false;
        }

        int spawnPointer = 0;
        foreach (var unitLoadout in loadout.units)
        {
            Vector2Int spawnPoint = session.MapAsset.GetSpawn(teamId, spawnPointer);
            UnitData unit = SpawnUnit(teamId, unitLoadout, spawnPoint.x, spawnPoint.y);
            if (unit.IsUnityNull())
            {
                Debug.LogError($"[SpawnManager] Failed to spawn unit for team {teamId} at ({spawnPoint.x},{spawnPoint.y})!");
                return false;
            }
            spawnPointer++;
        }

        return true;
    }

    public UnitData SpawnUnit(int teamNumber, PlayerUnitLoadout loadout, int worldX, int worldY)
    {
        TeamData team = session.GetTeamDataByTeamId(teamNumber);
        if (team == null)
        {
            Debug.LogError($"[SpawnManager] SpawnUnit: Team {teamNumber} not found!");
            return null;
        }

        var def = session.unitLibrary.Get(loadout.uId);
        if (def == null)
        {
            Debug.LogError($"[SpawnManager] SpawnUnit: No definition for uId {loadout.uId}!");
            return null;
        }

        if (!session.Map.IsWalkable(worldX, worldY))
        {
            Debug.LogError($"[SpawnManager] SpawnUnit: Cell ({worldX},{worldY}) is not walkable!");
            return null;
        }

        Vector2Int stepRange = speedConfig.GetStepRange(def.speed);

        var unit = new UnitData
        {
            Id = nextUnitId++,
            UId = loadout.uId,
            CurrentCell = new Vector3Int(worldX, worldY, 0),
            CurrentHP = def.maxHp,
            CurrentSkillPoint = def.maxSkillPoint,
            CurrentStep = stepRange.x,
            stepAlt = false,
            Speed = def.speed,
            MaxHP = def.maxHp,
            MaxSkillPoint = def.maxSkillPoint,
            DamageMultiplier = 1f,
            DamageReduction = 1f,
            MovementSkillId = loadout.movementSkillId,
            WeaponSkillId = loadout.weaponSkillId,
            ClassSkillId = loadout.classSkillId,
            EquipmentSkillId = loadout.equipmentSkillId,
            PassiveSkillId = def.passiveSkill != null ? def.passiveSkill.skillId : -1,
        };

        InitSkillPatterns(unit, loadout);

        team.unitIds.Add(unit.Id);
        session.units.Add(unit);

        if (!suppressSpawnRpcs)
            bridge.SpawnUnitClientRpc(unit);

        Debug.Log($"[SpawnManager] Spawned unit Id={unit.Id} uId={loadout.uId} team={teamNumber} speed={def.speed} step={unit.CurrentStep} at ({worldX},{worldY})");
        return unit;
    }

    // -------------------------------------------------------
    // Skill Pattern Init + Recalculate
    // -------------------------------------------------------

    void InitSkillPatterns(UnitData unit, PlayerUnitLoadout loadout)
    {
        var skillIds = new List<int>
    {
        loadout.movementSkillId,
        loadout.weaponSkillId,
        loadout.classSkillId,
        loadout.equipmentSkillId
    };

        var patterns = new List<CurrentPatterns>();
        foreach (var skillId in skillIds)
        {
            if (skillId == -1) continue;
            var skill = session.skillLibrary.Get(skillId);
            if (skill == null) continue;
            patterns.Add(new CurrentPatterns { SkillId = skillId });
        }

        unit.SkillPatterns = patterns.ToArray();
        RecalculateSkillPatterns(unit);

        Debug.Log($"[SpawnManager] Unit {unit.Id} — {patterns.Count} skill patterns initialized.");
    }

    public void RecalculateSkillPatterns(UnitData unit)
    {
        if (unit.SkillPatterns == null) return;

        for (int i = 0; i < unit.SkillPatterns.Length; i++)
        {
            var skill = session.skillLibrary.Get(unit.SkillPatterns[i].SkillId);
            if (skill == null) continue;
            unit.SkillPatterns[i] = BuildSkillPattern(unit, skill);
        }
    }
    CurrentPatterns BuildSkillPattern(UnitData unit, SkillDefinition skill)
    {
        var targetCells = new List<Vector3Int>();

        if (session.CanUseSkill(unit.Id, skill.skillId, skill) && skill.targetPattern != null)
        {
            var translated = PatternResolver.TranslateTargetPattern(skill, unit);
            targetCells = ServerPatternResolver.FilterTargetPattern(translated, unit, skill, session);
        }

        Vector2Int[] aoePattern = null;
        if (skill.effectIds != null)
            foreach (var effectId in skill.effectIds)
            {
                var effect = session.effectRegistry.Get(effectId);
                if (effect?.aoePattern?.cells != null)
                {
                    aoePattern = effect.aoePattern.cells;
                    break;
                }
            }

        return new CurrentPatterns
        {
            SkillId = skill.skillId,
            TargetCells = targetCells.ToArray(),
            AoePattern = aoePattern
        };
    }
}