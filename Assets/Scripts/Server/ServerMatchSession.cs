using System.Collections.Generic;
using Unity.Netcode;
using Unity.Services.Matchmaker.Models;
using UnityEngine;

public class ServerMatchSession : MonoBehaviour
{
    public int Turn;
    public int CurrentPlayerTurn;
    public GridMap Map { get; private set; }

    [Header("Config")]
    [SerializeField] private string mapId = "defaultPvpMap";

    [Header("Data")]
    [SerializeField] public UnitLibrary unitLibrary;

    public List<TeamData> teams; // index = team number

    public List<UnitData> units;

    // -------------------------------------------------------
    // Init
    // -------------------------------------------------------

    public bool Init()
    {
        Map = MapLoader.Load(mapId + ".json");
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
                //set team for players that arent server 
                teams.Add(new TeamData { teamId = teamNum, clientId = id, units = new List<UnitData>() });
                teamNum++;
            }
            if (teamNum >= 2) return; // Only support 2 teams, for now dont have player steamId from backend (no backend yet) so this method is used, will be removed when steamId is implemented and we can get player team data from backend instead of setting it here on server
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
    // Movement Authorization
    // -------------------------------------------------------

    public enum MoveResult { Ok, NotYourUnit, OutOfRange, NotWalkable, UnitNotFound, PlayerNotFound, CellOccupied }

    public MoveResult AuthorizeMove(ulong clientId, int unitId, Vector3Int target)
    {
        var unit = GetUnit(unitId);
        if (unit == null)
            return MoveResult.UnitNotFound;

        // Check team ownership — not clientId
        int team = GetTeamNumber(clientId);
        if (team == -1)
            return MoveResult.PlayerNotFound;

        if (unit.team != team)
            return MoveResult.NotYourUnit;

        if (!Map.IsWalkable(target.x, target.y))
            return MoveResult.NotWalkable;

        int dist = Mathf.Abs(target.x - unit.CurrentCell.x)
                 + Mathf.Abs(target.y - unit.CurrentCell.y);

        if (dist > unit.MoveRange)
            return MoveResult.OutOfRange;

        // Add unit collision check
        foreach (var otherUnit in units)
        {
            Debug.Log($"[AuthorizeMove] Checking collision with Unit {otherUnit.Id} at {otherUnit.CurrentCell}");
            if (otherUnit.Id != unitId &&
                otherUnit.CurrentCell.x == target.x &&
                otherUnit.CurrentCell.y == target.y)
            {
                return MoveResult.CellOccupied;  // Add this to enum
            }
        }

        return MoveResult.Ok;
    }

    public void ApplyMove(int unitId, Vector3Int target)
    {
        var unit = GetUnit(unitId);
        if (unit == null) return;
        unit.CurrentCell = target;
    }
}