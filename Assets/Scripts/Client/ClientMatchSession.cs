using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Data central for the client. Single source of truth for all game state.
/// Everyone reads from here, everyone writes to here.
/// Has its own state machine about Syncing 
/// </summary>
public class ClientMatchSession : MonoBehaviour
{
    [Header("Data")]
    [SerializeField] public MapRegistry mapRegistry;
    public int SyncState = -1;

    // -------------------------------------------------------
    // Token System
    // -------------------------------------------------------

    /// <summary>Latest token received from server. Set immediately on snapshot arrival.</summary>
    public int PendingToken { get; private set; } = -1; // PendingToken != CurrentToken means syncing => LockedInputState
    //PendingToken == CurrentToken means synced => SupportInteractionState

    /// <summary>Confirmed by ClientSyncMachine when all sync states complete.</summary>
    public int CurrentToken { get; private set; } = -1;

    public bool ResistInteractionState() => PendingToken != CurrentToken;
    public bool MaintainInteractionState() => PendingToken == CurrentToken;

    // -------------------------------------------------------
    // Game Data (from snapshot)
    // -------------------------------------------------------

    public int CurrentTeamTurn { get; private set; } = -1;
    public List<UnitData> units = new List<UnitData>();
    public List<TeamData> teams = new List<TeamData>();

    // -------------------------------------------------------
    // Map
    // -------------------------------------------------------

    public string MapId { get; private set; }
    public GridMapAsset MapAsset { get; private set; }
    public GridMap Map { get; private set; }

    // -------------------------------------------------------
    // Timeline
    // -------------------------------------------------------

    public TimelineData Timeline { get; private set; }

    // -------------------------------------------------------
    // Decision Data
    // -------------------------------------------------------

    public DecisionRequestData LastDecisionRequest { get; private set; }
    public int waitDur = 0;
    public int overtimeDur = 0;

    // -------------------------------------------------------
    // Resolve + Secret
    // -------------------------------------------------------

    public ResolveData LastResolve { get; private set; }
    public SecretData LastSecret { get; private set; }

    // -------------------------------------------------------
    // Interaction State
    // -------------------------------------------------------

    public int selectedUnitId { get; set; } = -1;
    public Vector3Int currentCellMouseOn { get; set; } = default;
    public Vector3Int currentPreviewCell { get; set; } = default;
    public int currentSkillId = -1;
    public bool isOnCell { get; set; } = false;

    // -------------------------------------------------------
    // Snapshot Apply
    // -------------------------------------------------------

    /// <summary>
    /// Called by ClientController on snapshot received.
    /// Updates all game data and sets PendingToken — processes react autonomously.
    /// </summary>
    public void ApplySnapshot(SessionSnapshotData snapshot, ResolveData resolve, SecretData secret, DecisionRequestData decision, int token)
    {
        // resolve data never arrive with decision data
        // Game data
        SyncState = 1; //applying sync
        CurrentTeamTurn = resolve.HasResolve ? -1 : snapshot.CurrentTeamTurn;
        teams = snapshot.Teams ?? new List<TeamData>();
        // units data overwrite();
        if (snapshot.Units != null)
            foreach (UnitData unit in snapshot.Units)
            {
                int index = units.FindIndex(u => u.Id == unit.Id);
                if (index == -1)
                    units.Add(unit);
                else
                    units[index] = unit;
            }
        Timeline = snapshot.Timeline;
        LastResolve = resolve;
        LastSecret = secret;
        if (!resolve.HasResolve) ApplyDecisionRequest(decision);
        LoadMapData(snapshot.MapId);
        SyncState = 2;
        // Set pending token  — this will signal other auto sync process
        PendingToken = token;

        // pending token => syncmachine runs until they mark their syncingToken = pending token. Then it will mark the ClientResolveReplayManager
        // Finally ResolveReplayManager set current Token = Pending Token (marked all syncing -> resolving completed, all other auto process may continue as usual) 

        //for now: skip all resolve animation ,apply the results (after sanpshot) 
        if (LastResolve.HasResolve)
            StartCoroutine(fakeResolve());
        else
        {
            SyncState = 0;
            CurrentToken = PendingToken; // no resolve, confirm immediately
        }
        Debug.Log($"[ClientMatchSession] Snapshot applied, PendingToken={PendingToken}");
    }

    IEnumerator fakeResolve()
    {
        SyncState = 3;
        yield return new WaitForSeconds(2f);
        ApplyAfterSnapshot();// move this to after the resolve replay process when completed coding that
    }


    void ApplyAfterSnapshot()
    {
        if (!LastResolve.HasResolve) return;
        if (CurrentToken > PendingToken) return;
        CurrentTeamTurn = LastResolve.AfterSnapshot.CurrentTeamTurn;
        teams = LastResolve.AfterSnapshot.Teams ?? new List<TeamData>();
        // units data overwrite();
        if (LastResolve.AfterSnapshot.Units != null)
            foreach (UnitData unit in LastResolve.AfterSnapshot.Units)
            {
                int index = units.FindIndex(u => u.Id == unit.Id);
                if (index == -1)
                    units.Add(unit);
                else
                    units[index] = unit;
            }

        LastResolve = default; // Completely wipe the last resolve to prevent re-loading the old Snapshot in this (because when get here, resolve animation is completed)
        SyncState = 0;
        CurrentToken = PendingToken;
    }

    void ApplyDecisionRequest(DecisionRequestData data)
    {
        LastDecisionRequest = data;
        waitDur = data.WaitDuration;
        overtimeDur = data.Overtime;
    }

    void LoadMapData(string mapId)
    {
        if (string.IsNullOrEmpty(mapId)) return;
        if (MapId == mapId) return; // no change

        MapAsset = mapRegistry.Get(mapId);
        if (MapAsset == null)
        {
            Debug.LogError($"[ClientMatchSession] No map asset for '{mapId}'!");
            return;
        }

        MapId = mapId;
        Map = new GridMap();
        Map.Init(MapAsset);
    }

    // -------------------------------------------------------
    // Timeline Tick
    // -------------------------------------------------------

    public void ApplyTimelineTick(TimelineData data)
    {
        Timeline = data;
        foreach (UnitData unit in units)
            if (unit.CurrentStep > 0)
                unit.CurrentStep--;
    }

    // -------------------------------------------------------
    // Unit Helpers
    // -------------------------------------------------------

    public UnitData GetUnitDataById(int unitId) => units.Find(u => u.Id == unitId);
    public List<int> GetAllUnitIds()
    {
        List<int> res = new List<int>();
        foreach (var u in units)
        {
            res.Add(u.Id);
        }
        return res;
    }
    public UnitData GetUnitDataAt(Vector3Int cell) => units.Find(u => u.CurrentCell == cell);

    // -------------------------------------------------------
    // Team Helpers
    // -------------------------------------------------------

    public TeamData GetOwnedTeamData() => teams.Find(t => t.clientId == NetworkManager.Singleton.LocalClientId);
    public int GetMyTeam()
    {
        return teams[0].clientId == NetworkManager.Singleton.LocalClientId ? 0 :
               teams[1].clientId == NetworkManager.Singleton.LocalClientId ? 1 : -1;
    }
    public bool IsMyTurn() => LastDecisionRequest.DecisionTeam == GetMyTeam();
    public bool IsMyUnit(int unitId) => GetOwnedTeamData()?.unitIds.Contains(unitId) ?? false;
    public bool IsUnitDataExisting(int unitId) => units.Exists(u => u.Id == unitId) ? true : false;
    public List<int> GetOwnedReadyUnitIds()
    {
        var owned = new List<int>();
        var team = GetOwnedTeamData();
        if (team == null) return owned;

        // Ready = CurrentStep == 0
        foreach (var unit in units)
            if (unit.CurrentStep == 0 && team.unitIds.Contains(unit.Id))
                owned.Add(unit.Id);

        return owned;
    }

    // DATA LOOP
    private float loopFrequency = 1f;
    public void Init()
    {
        //called once, start loops that manages DATA (future data loops will be initiallized here if the loop is stable and independent)
        StartCoroutine(DataLoop());
    }

    IEnumerator DataLoop()
    {
        while (true)
        {
            if (ResistInteractionState())
            {
                Resisting();
            }
            else
            {
                Maintaining();
            }
            yield return new WaitForSeconds(loopFrequency);
        }
    }

    private void Resisting()
    {

    }
    private void Maintaining()
    {
        if (Timeline.isPaused)
            CountDownDurations();
    }


    public void CountDownDurations()
    {
        if (waitDur > 0)
            waitDur--;
        else
            overtimeDur --;
    }
}