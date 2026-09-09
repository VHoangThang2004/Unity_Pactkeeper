using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class ServerMatchSession : MonoBehaviour
{
    public int Turn;
    public int CurrentPlayerTurn;
    public GridMap Map { get; private set; }
    public string MapId { get; private set; }
    public GridMapAsset MapAsset { get; private set; }

    [Header("Config")]
    [SerializeField] private string mapId = "defaultPvpMap";

    [Header("Data")]
    [SerializeField] private MapRegistry mapRegistry;
    [SerializeField] public UnitLibrary unitLibrary;

    public List<TeamData> teams;
    public List<UnitData> units;

    // -------------------------------------------------------
    // Init
    // -------------------------------------------------------

    public bool Init()
    {
        mapRegistry.Init();

        MapAsset = mapRegistry.Get(mapId);
        if (MapAsset == null)
        {
            Debug.LogError($"[MatchSession] MapRegistry has no entry for mapId '{mapId}'!");
            return false;
        }

        MapId = mapId;
        Map = new GridMap();
        Map.Init(MapAsset);

        unitLibrary.Init();
        teams = new List<TeamData>();
        units = new List<UnitData>();
        SetTeamExcludeServer();
        return true;
    }

    private void SetTeamExcludeServer()
    {
        var ids = NetworkManager.Singleton.ConnectedClientsIds;
        int teamNum = 0;
        foreach (var id in ids)
        {
            Debug.Log($"[MatchSession] Connected client: {id}");
            if (id != NetworkManager.Singleton.LocalClientId)
            {
                teams.Add(new TeamData { teamId = teamNum, clientId = id, units = new List<UnitData>() });
                teamNum++;
            }
            if (teamNum >= 2) return;
        }
    }

    // -------------------------------------------------------
    // Team & Player Management
    // -------------------------------------------------------

    public int GetTeamNumber(ulong clientId)
    {
        return teams[0].clientId == clientId ? 0 :
               teams[1].clientId == clientId ? 1 : -1;
    }

    public TeamData GetTeamData(int team)
    {
        return teams[team];
    }

    // -------------------------------------------------------
    // Lookup
    // -------------------------------------------------------

    public UnitData GetUnit(int instanceId)
    {
        return units.Find(u => u.Id == instanceId);
    }

    public List<UnitData> GetAllUnits()
    {
        return new List<UnitData>(units);
    }

    // -------------------------------------------------------
    // Occupied Cells
    // -------------------------------------------------------

    public HashSet<Vector3Int> GetOccupiedCells(int excludeUnitId)
    {
        var occupied = new HashSet<Vector3Int>();
        foreach (var u in units)
        {
            if (u.Id != excludeUnitId)
                occupied.Add(u.CurrentCell);
        }
        return occupied;
    }

    // -------------------------------------------------------
    // Movement Authorization
    // -------------------------------------------------------

    public enum MoveResult { Ok, NotYourUnit, OutOfRange, NotWalkable, UnitNotFound, PlayerNotFound, CellOccupied, NoPath }

    public MoveResult AuthorizeMove(ulong clientId, int unitId, Vector3Int target)
    {
        var unit = GetUnit(unitId);
        if (unit == null)
            return MoveResult.UnitNotFound;

        int team = GetTeamNumber(clientId);
        if (team == -1)
            return MoveResult.PlayerNotFound;

        if (unit.team != team)
            return MoveResult.NotYourUnit;

        if (!Map.IsWalkable(target.x, target.y))
            return MoveResult.NotWalkable;

        var occupiedCells = GetOccupiedCells(unitId);

        if (occupiedCells.Contains(target))
            return MoveResult.CellOccupied;

        // Pathfind using GridMap directly — no prefab, no Tilemap needed
        var path = GridPathfinder.FindPath(Map, unit.CurrentCell, target, occupiedCells);

        if (path == null)
            return MoveResult.NoPath;

        if (path.Count > unit.MoveRange)
            return MoveResult.OutOfRange;

        return MoveResult.Ok;
    }

    public void ApplyMove(int unitId, Vector3Int target)
    {
        var unit = GetUnit(unitId);
        if (unit == null) return;
        unit.CurrentCell = target;
    }
}