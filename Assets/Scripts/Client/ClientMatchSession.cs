using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class ClientMatchSession : MonoBehaviour
{
    [Header("Data")]
    [SerializeField] public MapRegistry mapRegistry;
    [SerializeField] private ClientScene scene;
    public int SyncState = -1;

    // -------------------------------------------------------
    // Token System
    // -------------------------------------------------------

    public int PendingToken { get; private set; } = -1;
    public int CurrentToken { get; private set; } = -1;

    public bool ResistInteractionState() => PendingToken != CurrentToken;
    public bool MaintainInteractionState() => PendingToken == CurrentToken;

    // -------------------------------------------------------
    // Game Data
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
    public bool isTargetLocked { get; set; } = false;
    // -------------------------------------------------------
    // Pattern Preview Data — written by interaction states, read by visual controller
    // -------------------------------------------------------
    public List<Vector3Int> CurrentTargetPatternCells { get; set; } = new List<Vector3Int>();
    public List<Vector3Int> CurrentAoECells { get; set; } = new List<Vector3Int>();

    public void ClearPatternData()
    {
        CurrentTargetPatternCells.Clear();
        CurrentAoECells.Clear();
        isTargetLocked = false;
    }
    public int currentSkillId = -1;
    public bool isOnCell { get; set; } = false;

    // -------------------------------------------------------
    // Snapshot Apply
    // -------------------------------------------------------

    public void ApplySnapshot(
        SessionSnapshotData before,
        ResolveData resolve,
        SecretData secret,
        DecisionRequestData decision,
        int token)
    {
        SyncState = 1;

        // Step 1 — Load map first, everything else may depend on it
        if (before.HasMapId)
            LoadMapData(before.MapId);

        // Step 2 — Apply Before snapshot
        ApplyFullSnapshot(before);

        // Step 3 — Store resolve + secret
        LastResolve = resolve;
        LastSecret = secret;

        // Step 4 — Store decision always
        ApplyDecisionRequest(decision);

        // Step 5 — Set pending token
        PendingToken = token;

        if (resolve.HasResolve)
        {
            SyncState = 3;
            StartCoroutine(ResolveReplay());
        }
        else
        {
            SyncState = 0;
            CurrentToken = PendingToken;
        }

        Debug.Log($"[ClientMatchSession] Snapshot applied, PendingToken={PendingToken}");
    }

    void ApplyFullSnapshot(SessionSnapshotData snap)
    {
        if (snap.HasCurrentTeamTurn) CurrentTeamTurn = snap.CurrentTeamTurn;
        if (snap.HasTeams) teams = snap.Teams ?? new List<TeamData>();
        if (snap.HasUnits && snap.Units != null)
            foreach (var unit in snap.Units)
            {
                int index = units.FindIndex(u => u.Id == unit.Id);
                if (index == -1) units.Add(unit);
                else units[index] = unit;
            }
        if (snap.HasTimeline) Timeline = snap.Timeline;
    }

    // -------------------------------------------------------
    // Resolve Replay
    // -------------------------------------------------------

    IEnumerator ResolveReplay()
    {
        if (!LastResolve.HasResolve) yield break;
        if (LastResolve.ResolveResults == null) yield break;

        foreach (var result in LastResolve.ResolveResults)
        {
            // Look up visual effect
            var effectVisual = scene.effectRegistry.Get(result.EffectId);

            if (effectVisual != null && effectVisual.resolveDuration > 0f)
            {
                // Find source unit in scene
                var sceneUnit = scene.GetSceneUnitById(result.SourceUnitId);

                if (sceneUnit != null)
                    yield return StartCoroutine(effectVisual.Replay(sceneUnit, result, scene, this));
                else
                    yield return new WaitForSeconds(effectVisual.resolveDuration);
            }

            // Apply partial snapshot after each result
            ApplyPartialSnapshot(result.Partial);
        }

        LastResolve = default;
        SyncState = 0;
        CurrentToken = PendingToken;
    }

    void ApplyPartialSnapshot(SessionSnapshotData partial)
    {
        if (partial.HasCurrentTeamTurn) CurrentTeamTurn = partial.CurrentTeamTurn;
        if (partial.HasTeams && partial.Teams != null) teams = partial.Teams;
        if (partial.HasUnits && partial.Units != null)
            foreach (var unit in partial.Units)
            {
                int index = units.FindIndex(u => u.Id == unit.Id);
                if (index == -1) units.Add(unit);
                else units[index] = unit;
            }
        if (partial.HasTimeline) Timeline = partial.Timeline;
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
        if (MapId == mapId) return;

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
        int instantDelta = data.currentInstant - Timeline.currentInstant;

        if (instantDelta > 0)
            foreach (UnitData unit in units)
                unit.CurrentStep = Mathf.Max(0, unit.CurrentStep - instantDelta);

        Timeline = data;
    }

    // -------------------------------------------------------
    // Unit Helpers
    // -------------------------------------------------------

    public UnitData GetUnitDataById(int unitId) => units.Find(u => u.Id == unitId);
    public List<int> GetAllUnitIds()
    {
        var res = new List<int>();
        foreach (var u in units) res.Add(u.Id);
        return res;
    }
    public UnitData GetUnitDataAt(Vector3Int cell) => units.Find(u => u.CurrentCell == cell);

    // -------------------------------------------------------
    // Team Helpers
    // -------------------------------------------------------
    public int GetTeamIdByUnitId(int unitId)
    {
        foreach (var team in teams)
            if (team.unitIds.Contains(unitId)) return team.teamId;
        return -1;
    }

    public TeamData GetOwnedTeamData() => teams.Find(t => t.clientId == NetworkManager.Singleton.LocalClientId);
    public int GetMyTeam()
    {
        return teams[0].clientId == NetworkManager.Singleton.LocalClientId ? 0 :
               teams[1].clientId == NetworkManager.Singleton.LocalClientId ? 1 : -1;
    }
    public bool IsMyTurn() => LastDecisionRequest.DecisionTeam == GetMyTeam();
    public bool IsMyUnit(int unitId) => GetOwnedTeamData()?.unitIds.Contains(unitId) ?? false;
    public bool IsUnitDataExisting(int unitId) => units.Exists(u => u.Id == unitId);
    public List<int> GetOwnedReadyUnitIds()
    {
        var owned = new List<int>();
        var team = GetOwnedTeamData();
        if (team == null) return owned;
        foreach (var unit in units)
            if (unit.CurrentStep == 0 && team.unitIds.Contains(unit.Id))
                owned.Add(unit.Id);
        return owned;
    }

    // -------------------------------------------------------
    // Data Loop
    // -------------------------------------------------------

    private float loopFrequency = 1f;

    public void Init()
    {
        StartCoroutine(DataLoop());
    }

    IEnumerator DataLoop()
    {
        while (true)
        {
            if (ResistInteractionState())
                Resisting();
            else
                Maintaining();
            yield return new WaitForSeconds(loopFrequency);
        }
    }

    private void Resisting() { }

    private void Maintaining()
    {
        if (Timeline.isPaused)
            CountDownDurations();
    }

    public void CountDownDurations()
    {
        if (waitDur > 0) waitDur--;
        else overtimeDur--;
    }
}