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
    public DecisionRequestData LastDecisionRequest { get; private set; }

    public void ApplyTimelineTick(TimelineData data)
    {
        Timeline = data;

        if (data.readyUnitIds != null)
        {
            foreach (int id in data.readyUnitIds)
            {
                var unit = GetUnit(id);
                if (unit != null)
                    unit.CurrentStep = 0;
            }
        }
    }

    public void ApplyDecisionRequest(DecisionRequestData data)
    {
        LastDecisionRequest = data;
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

    public TeamData GetOwnedTeamData()
    {
        return teams.Find(t => t.clientId == NetworkManager.Singleton.LocalClientId);
    }

    public bool IsUnitUnderPermission(int unitId)
    {
        TeamData team = GetOwnedTeamData();
        return team != null && team.units.Exists(u => u.Id == unitId);
    }

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