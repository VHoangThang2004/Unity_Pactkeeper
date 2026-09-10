using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class ServerMatchSession : MonoBehaviour
{
    public int CurrentTeamTurnId = -1; // -1 : not a turn, time flowing, if a team's turn, this value equals to TeamData.teamId
    public GridMap Map { get; private set; }
    public string MapId { get; private set; }
    public GridMapAsset MapAsset { get; private set; }

    [Header("Config")]
    [SerializeField] private string mapId = "defaultPvpMap";

    [Header("Data")]
    [SerializeField] private MapRegistry mapRegistry;
    [SerializeField] public UnitLibrary unitLibrary;

    private List<TeamData> teams;
    public List<UnitData> units;
    // Testing phase — hardcoded loadouts
    // Backend phase: replace this with data received from backend
    private List<TeamLoadout> loadouts = new List<TeamLoadout>
    {
        new TeamLoadout { teamId = 0, clientId = 0, unitUIds = new List<int> { 1, 2 } },
        new TeamLoadout { teamId = 1, clientId = 0, unitUIds = new List<int> { 1, 2 } }
    };

    // Last sent state — always up to date, used for targeted sends and resync
    public SessionSnapshotData LastSnapshot;
    public ResolveData LastResolve;

    // Secret data per team — never inside TeamData, never accidentally broadcast
    private SecretData[] secretData = new SecretData[]
{
    new SecretData { Data = string.Empty },
    new SecretData { Data = string.Empty }
};

    public SecretData GetSecretData(int team)
    {
        if (team < 0 || team >= secretData.Length) return default;
        return secretData[team];
    }

    public void SetSecretData(int team, SecretData data)
    {
        if (team < 0 || team >= secretData.Length) return;
        secretData[team] = data;
    }

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
        SetTeamExcludeServer();
        return true;
    }

    private void SetTeamExcludeServer()
    {
        teams = new List<TeamData>();
        units = new List<UnitData>();

        // Build TeamData from loadouts — clientId assigned from connected clients for now
        var connectedIds = new List<ulong>(NetworkManager.Singleton.ConnectedClientsIds);
        connectedIds.Remove(NetworkManager.Singleton.LocalClientId); // exclude server

        for (int i = 0; i < loadouts.Count && i < connectedIds.Count; i++)
        {
            loadouts[i].clientId = connectedIds[i]; // testing: assign real clientId here
            teams.Add(new TeamData
            {
                teamId = loadouts[i].teamId,
                clientId = loadouts[i].clientId,
                unitIds = new List<int>()
            });
        }
    }

    // -------------------------------------------------------
    // Team & Player Management
    // -------------------------------------------------------

    public int GetTeamNumberByClientId(ulong clientId)
    {
        return teams[0].clientId == clientId ? 0 :
               teams[1].clientId == clientId ? 1 : -1;
    }

    public TeamData GetCurrentTurnTeamData()
    {
        return teams[0].teamId == CurrentTeamTurnId ? teams[0] :
                teams[1].teamId == CurrentTeamTurnId ? teams[1] : null;
    }

    public TeamData GetTeamDataByTeamId(int teamId)
    {
        return teams[0].teamId == teamId ? teams[0] :
               teams[1].teamId == teamId ? teams[1] : null;
    }
    public TeamLoadout GetTeamLoadoutDataByTeamId(int teamId)
    {
        return loadouts[0].teamId == teamId ? loadouts[0] :
               loadouts[1].teamId == teamId ? loadouts[1] : null;
    }
    public TeamData GetTeamDataByClientId(ulong clientId)
    {
        return teams[0].clientId == clientId ? teams[0] :
               teams[1].clientId == clientId ? teams[1] : null;
    }
    public TeamLoadout GetTeamLoadoutDataByClientId(ulong clientId)
    {
        return loadouts[0].clientId == clientId ? loadouts[0] :
               loadouts[1].clientId == clientId ? loadouts[1] : null;
    }
    public List<TeamData> GetAllTeamData()
    {
        return teams;
    }
    public int GetOtherTeamId(int currentTeamId)
    {
        return teams[0].teamId == currentTeamId ? teams[0].teamId : teams[1].teamId;
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

        int team = GetTeamNumberByClientId(clientId);
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