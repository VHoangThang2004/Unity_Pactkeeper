using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class ClientMatchSession : MonoBehaviour
{
    public int Turn;
    public int CurrentPlayerTurn;
    public List<UnitData> units = new List<UnitData>();
    public List<TeamData> teams = new List<TeamData>();

    [Header("Data")]
    [SerializeField] private MapRegistry mapRegistry;

    // -------------------------------------------------------
    // Map
    // -------------------------------------------------------

    public string MapId { get; private set; }
    public GridMapAsset MapAsset { get; private set; }
    public GridMap Map { get; private set; }

    // -------------------------------------------------------
    // Timeline State (server-authoritative, display only)
    // -------------------------------------------------------

    public TimelineData Timeline { get; private set; }
    public DecisionRequestData LastDecisionRequest;
    public int waitDur = 0;
    public int overtimeDur = 0;

    public void CountDownDurations()
    {
        if (waitDur > 0)
            waitDur--;
        else
            overtimeDur = Mathf.Max(0, overtimeDur - 1);
    }



    public void ApplyTimelineTick(TimelineData data)
    {
        Timeline = data;

        // Decrement all unit steps locally — mirrors server tick
        // Mid snapshot corrects any drift before decisions are made
        foreach (var unit in units)
            if (unit.CurrentStep > 0)
                unit.CurrentStep--;
    }

    public void ApplyDecisionRequest(DecisionRequestData data)
    {
        LastDecisionRequest = data;
        waitDur = Mathf.Max(data.ActWaitDuration, data.ActionDuration);
        if (data.DecisionTeam == 0)
        {
            overtimeDur = data.OvertimeTeam0;
        }
        else
        {
            overtimeDur = data.OvertimeTeam1;
        }
    }

    public void ApplyUnitStepReset(int unitId, int newStep)
    {
        var unit = GetUnit(unitId);
        if (unit != null)
            unit.CurrentStep = newStep;
    }

    // -------------------------------------------------------
    // Snapshot
    // -------------------------------------------------------

    public void ApplySnapshot(SessionSnapshotData snapshot)
    {
        Turn = snapshot.Turn;
        CurrentPlayerTurn = snapshot.CurrentPlayerTurn;
        teams = snapshot.Teams ?? new List<TeamData>();
        units.Clear();

        if (snapshot.Units != null)
            foreach (var unit in snapshot.Units)
                units.Add(unit);

        Timeline = snapshot.Timeline;

        // Load map from registry using mapId from snapshot
        LoadMap(snapshot.MapId);
    }

    private void LoadMap(string mapId)
    {
        if (string.IsNullOrEmpty(mapId))
        {
            Debug.LogError("[ClientMatchSession] Snapshot has no MapId!");
            return;
        }

        mapRegistry.Init();

        MapAsset = mapRegistry.Get(mapId);
        if (MapAsset == null)
        {
            Debug.LogError($"[ClientMatchSession] MapRegistry has no entry for mapId '{mapId}'!");
            return;
        }

        MapId = mapId;
        Map = new GridMap();
        Map.Init(MapAsset);

        Debug.Log($"[ClientMatchSession] Map loaded: '{mapId}'");
    }

    // -------------------------------------------------------
    // Unit Helpers
    // -------------------------------------------------------

    public void AddUnit(UnitData unit) => units.Add(unit);

    public UnitData GetUnit(int unitId) => units.Find(u => u.Id == unitId);

    public UnitData GetUnitInTeam(int unitId, TeamData team) => team.units.Find(u => u.Id == unitId);

    public UnitData GetUnitDataAt(Vector3Int cell) => units.Find(u => u.CurrentCell == cell);
    public int GetCurrentTeamDecisionOvertime()
    {
        if (LastDecisionRequest.DecisionTeam == 0)
        {
            return LastDecisionRequest.OvertimeTeam0;
        }
        else
        {
            return LastDecisionRequest.OvertimeTeam1;
        }
    }

    public TeamData GetOwnedTeamData()
    {
        return teams.Find(t => t.clientId == NetworkManager.Singleton.LocalClientId);
    }

    public bool IsMyTurn() => LastDecisionRequest.DecisionTeam == GetMyTeam();

    public int GetMyTeam() => teams.IndexOf(GetOwnedTeamData());
    public bool IsMyUnit(int unitId) => GetOwnedTeamData() != null && GetOwnedTeamData().units.Exists(u => u.Id == unitId);


    public List<int> GetOwnedReadyUnitIds()
    {
        var owned = new List<int>();
        TeamData team = GetOwnedTeamData();
        if (team == null || Timeline.readyUnitIds == null) return owned;

        foreach (int id in Timeline.readyUnitIds)
            if (team.units.Exists(u => u.Id == id))
                owned.Add(id);

        return owned;
    }
}