using System.Collections.Generic;
using Unity.Netcode;
using Unity.Services.Matchmaker.Models;
using UnityEngine;

public class ClientMatchSession : MonoBehaviour
{
    public int Turn;
    public int CurrentPlayerTurn;
    public List<UnitData> units = new List<UnitData>();
    public List<TeamData> teams = new List<TeamData>();

    public void ApplySnapshot(SessionSnapshotData snapshot)
    {
        Turn = snapshot.Turn;
        CurrentPlayerTurn = snapshot.CurrentPlayerTurn;
        teams = snapshot.Teams ?? new List<TeamData>();
        units.Clear();

        if (snapshot.Units != null)
        {
            foreach (var unit in snapshot.Units)
                units.Add(unit);
        }
    }

    public void AddUnit(UnitData unit)
    {
        units.Add(unit);
    }

    public UnitData GetUnit(int unitId)
    {
        return units.Find(u => u.Id == unitId);
    }
    
    public UnitData GetUnitInTeam(int unitId,TeamData team)
    {
        return team.units.Find(u => u.Id == unitId);
    }
    


    public UnitData GetUnitDataAt(Vector3Int cell)
    {
        return units.Find(u => u.CurrentCell == cell);
    }

    public TeamData GetOwnedTeamData()
    {
        return teams.Find(t => t.clientId == NetworkManager.Singleton.LocalClientId); // switch to SteamId when getting steam data from Steam
    }

    public bool IsUnitUnderPermission(int unitId)
    {
        TeamData team = GetOwnedTeamData();
        return team != null && team.units.Exists(u => u.Id == unitId);
    }

}