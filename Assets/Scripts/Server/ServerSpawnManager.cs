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
        // UnitRecalculator.RecalculateAll(session);
        suppressSpawnRpcs = false;
        return true;
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
        foreach (var unitLoadout in loadout.Units)
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

        if (!session.Map.IsWalkable(worldX, worldY))
        {
            Debug.LogError($"[SpawnManager] SpawnUnit: Cell ({worldX},{worldY}) is not walkable!");
            return null;
        }

        Vector2Int stepRange = speedConfig.GetStepRange(loadout.Speed);

        var unit = new UnitData
        {
            Id = nextUnitId++,
            UId = loadout.UId,
            CurrentCell = new Vector3Int(worldX, worldY, 0),
            CurrentHP = loadout.MaxHP,
            CurrentStep = stepRange.x,
            StepAlt = true,
            CurrentStepBase = stepRange.y,
            Speed = loadout.Speed,
            MaxHP = loadout.MaxHP,
            MaxSkillPoint = loadout.MaxSkillPoint,
            CurrentSkillPoint = 0,
            DamageMultiplier = loadout.DamageMultiplier,
            DamageReduction = loadout.DamageReduction,
            BaseSpeed = loadout.Speed,
            BaseMaxHP = loadout.MaxHP,
            BaseMaxSkillPoint = loadout.MaxSkillPoint,
            BaseDamageMultiplier = loadout.DamageMultiplier,
            BaseDamageReduction = loadout.DamageReduction,
            MovementSkillId = loadout.MovementSkillId,
            WeaponSkillId = loadout.WeaponSkillId,
            ClassSkillId = loadout.ClassSkillId,
            TrinketSkillId = loadout.TrinketSkillId,
            PassiveSkillId = loadout.PassiveSkillId,
        };

        team.unitIds.Add(unit.Id);
        session.units.Add(unit);

        if (!suppressSpawnRpcs)
            bridge.SpawnUnitClientRpc(unit);

        Debug.Log($"[SpawnManager] Spawned unit Id={unit.Id} uId={loadout.UId} team={teamNumber} speed={loadout.Speed} step={unit.CurrentStep} at ({worldX},{worldY})");
        return unit;
    }

    // -------------------------------------------------------
    // Skill Pattern Init + Recalculate
    // -------------------------------------------------------

}