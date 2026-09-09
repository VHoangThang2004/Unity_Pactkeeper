using UnityEngine;
using Unity.VisualScripting;

public class ServerSpawnManager : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private SyncedBridge bridge;
    [SerializeField] private ServerMatchSession session;
    [SerializeField] private SpeedConfig speedConfig;

    [Header("Config")]
    [SerializeField] private int defaultUId = 1;

    private bool suppressSpawnRpcs = true;
    private int nextUnitId = 1;

    public bool spawnAll()
    {
        if (!SpawnTeam(0)) return false;
        if (!SpawnTeam(1)) return false;
        return true;
    }

    public bool SpawnTeam(int teamNumber)
    {
        // Spawn point comes from GridMapAsset — no separate SpawnConfig needed
        Vector2Int spawnPoint = session.MapAsset.GetSpawn(teamNumber);

        UnitData unit = SpawnUnit(teamNumber, defaultUId, spawnPoint.x, spawnPoint.y);
        if (unit.IsUnityNull())
        {
            Debug.LogError($"[SpawnManager] Failed to spawn unit for team {teamNumber} at ({spawnPoint.x},{spawnPoint.y})!");
            return false;
        }

        suppressSpawnRpcs = false;
        return true;
    }

    public UnitData SpawnUnit(int teamNumber, int uId, int worldX, int worldY)
    {
        TeamData team = session.GetTeamData(teamNumber);
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
            Id          = nextUnitId++,
            UId         = uId,
            team        = teamNumber,
            CurrentCell = new Vector3Int(worldX, worldY, 0),
            MoveRange   = def.moveRange,
            Speed       = def.speed,
            CurrentStep = stepRange.x,
            stepAlt     = false,
        };

        team.units.Add(unit);
        session.units.Add(unit);

        if (!suppressSpawnRpcs)
            bridge.SpawnUnitClientRpc(unit);

        Debug.Log($"[SpawnManager] Spawned unit Id={unit.Id} uId={uId} team={teamNumber} speed={def.speed} step={unit.CurrentStep} at ({worldX},{worldY})");
        return unit;
    }
}