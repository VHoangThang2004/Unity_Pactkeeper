using UnityEngine;
using Unity.VisualScripting;

public class ServerSpawnManager : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private SyncedBridge bridge;
    [SerializeField] private ServerMatchSession session;
    [SerializeField] private SpawnConfig spawnConfig;

    [Header("Config")]
    [SerializeField] private int defaultUId = 1;

    // -------------------------------------------------------
    // Spawn Logic
    private bool suppressSpawnRpcs = true; // Don't send during initial setup


    private int nextUnitId = 1;

    public bool spawnAll()
    {
        //spawn only 1 unit for now (testing and developing phase)
        if (!SpawnTeam(0))
        {
            return false;
        }
        if (!SpawnTeam(1))
        {
            return false;
        }

        return true;
    }

    public bool SpawnTeam(int teamNumber)
    {
        Vector2Int spawnPoint = spawnConfig.GetSpawn(teamNumber);
        if (spawnPoint == null)
        {
            Debug.LogError($"[SpawnManager] No spawn point for team {teamNumber}!");
            return false;
        }

        UnitData unit = SpawnUnit(teamNumber, defaultUId, spawnPoint.x, spawnPoint.y);
        if (unit.IsUnityNull())
        {
            Debug.LogError($"[SpawnManager] Failed to spawn unit for team {teamNumber} at ({spawnPoint.x},{spawnPoint.y})!");
            return false;
        }


        suppressSpawnRpcs = false; // Now allow individual RPCs for late joins
        return true;
    }

    public UnitData SpawnUnit(int teamNumber, int uId, int worldX, int worldY)
    {
        TeamData team = session.GetTeamData(teamNumber);
        if (team == null)
        {
            Debug.LogError($"[Session] SpawnUnit: Team {teamNumber} not found!");
            return null;
        }

        var def = session.unitLibrary.Get(uId);
        if (def == null)
        {
            Debug.LogError($"[Session] SpawnUnit: No definition for uId {uId}!");
            return null;
        }

        if (!session.Map.IsWalkable(worldX, worldY))
        {
            Debug.LogError($"[Session] SpawnUnit: Cell ({worldX},{worldY}) is not walkable!");
            return null;
        }

        var unit = new UnitData
        {
            Id = nextUnitId++,
            UId = uId,
            team = teamNumber,
            CurrentCell = new Vector3Int(worldX, worldY, 0),
            MoveRange = def.moveRange,
        };

        team.units.Add(unit);
        session.units.Add(unit);

        //rpc to clients to spawn the unit on their end
        if (!suppressSpawnRpcs)
        {
            bridge.SpawnUnitClientRpc(unit);
        }
        Debug.Log($"[Session] Spawned unit Id={unit.Id} uId={uId} team={teamNumber} at ({worldX},{worldY})");
        return unit;
    }

}