using UnityEngine;
using Unity.VisualScripting;
using System.Collections.Generic;

public class ServerSpawnManager : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private SyncedBridge bridge;
    [SerializeField] private ServerMatchSession session;
    [SerializeField] private SpeedConfig speedConfig;

    [Header("Config")]
    //testing period only, future will have data from server backend to load team
    // [SerializeField] private List<int> spawningUnitIds = new List<int>();

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
        }
        Debug.Log($"[SpawnManager] All units recalculated.");
    }

    public bool SpawnTeam(int teamId)
    {
        // Spawn point comes from GridMapAsset — no separate SpawnConfig needed

        int spawnPointer = 0;
        foreach (int id in session.GetTeamLoadoutDataByTeamId(teamId).unitUIds)
        {
            Vector2Int spawnPoint = session.MapAsset.GetSpawn(teamId, spawnPointer);

            UnitData unit = SpawnUnit(teamId, id, spawnPoint.x, spawnPoint.y);
            if (unit.IsUnityNull())
            {
                Debug.LogError($"[SpawnManager] Failed to spawn unit for team {teamId} at ({spawnPoint.x},{spawnPoint.y})!");
                return false;
            }
            spawnPointer++;
        }

        return true;
    }

    public UnitData SpawnUnit(int teamNumber, int uId, int worldX, int worldY)
    {
        TeamData team = session.GetTeamDataByTeamId(teamNumber);
        if (team == null)
        {
            Debug.LogError($"[SpawnManager] SpawnUnit: Team {teamNumber} not found!");
            return null;
        }

        var def = session.unitLibrary.Get(uId);
        if (def == null)
        {
            Debug.LogError($"[SpawnManager] SpawnUnit: No definition for uId {uId}!");
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
            UId = uId,
            CurrentCell = new Vector3Int(worldX, worldY, 0),
            // Live values
            CurrentHP = def.maxHp,
            CurrentSkillPoint = def.maxSkillPoint,
            CurrentStep = stepRange.x,
            stepAlt = false,
            // Recalculated stats
            Speed = def.speed,
            MaxHP = def.maxHp,
            MaxSkillPoint = def.maxSkillPoint,
            DamageMultiplier = 1f,
            DamageReduction = 1f,
            // Skill slots
            MovementSkillId = def.movementSkill != null ? def.movementSkill.skillId : -1,
        };
        InitPassives(unit, def);
        InitPatternOverrides(unit, def);
        team.unitIds.Add(unit.Id);
        session.units.Add(unit);

        if (!suppressSpawnRpcs)
            bridge.SpawnUnitClientRpc(unit);

        Debug.Log($"[SpawnManager] Spawned unit Id={unit.Id} uId={uId} team={teamNumber} speed={def.speed} step={unit.CurrentStep} at ({worldX},{worldY})");
        return unit;
    }
    void InitPassives(UnitData unit, UnitDefinition def)
    {
        if (def.passiveSkills == null)
        {
            unit.PassiveSkillIds = new int[0];
            return;
        }

        var ids = new List<int>();
        foreach (var skill in def.passiveSkills)
        {
            if (skill == null) continue;
            ids.Add(skill.skillId);
        }

        unit.PassiveSkillIds = ids.ToArray();

        Debug.Log($"[SpawnManager] Unit {unit.Id} — movementSkillId={unit.MovementSkillId} passives={unit.PassiveSkillIds.Length}");
    }
    void InitPatternOverrides(UnitData unit, UnitDefinition def)
    {
        var overrides = new List<SkillPatternOverride>();

        // Movement skill pattern
        if (def.movementSkill != null && def.movementSkill.targetPattern != null)
        {
            overrides.Add(new SkillPatternOverride
            {
                SkillId = def.movementSkill.skillId,
                TargetPatternCells = def.movementSkill.targetPattern.cells,
                AoEPatternCells = null
            });
        }

        // Passive skills have no target/aoe patterns — skip

        unit.PatternOverrides = overrides.ToArray();

        Debug.Log($"[SpawnManager] Unit {unit.Id} — {overrides.Count} pattern overrides initialized.");
    }
}