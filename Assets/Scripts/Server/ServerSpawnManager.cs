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
        return true;
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

        suppressSpawnRpcs = false;
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
            team = teamNumber,
            CurrentCell = new Vector3Int(worldX, worldY, 0),
            MoveRange = def.moveRange,
            Speed = def.speed,
            CurrentStep = stepRange.x,
            stepAlt = false,
        };

        team.unitIds.Add(unit.Id);
        session.units.Add(unit);

        if (!suppressSpawnRpcs)
            bridge.SpawnUnitClientRpc(unit);

        Debug.Log($"[SpawnManager] Spawned unit Id={unit.Id} uId={uId} team={teamNumber} speed={def.speed} step={unit.CurrentStep} at ({worldX},{worldY})");
        return unit;
    }
}