using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public enum SyncStateValue
{
    Uninitialized = -1,
    Idle = 0,
    Syncing = 2,
    Resolving = 3
}

public class ClientMatchSession : MonoBehaviour
{
    [Header("Data")]
    [SerializeField] public MapRegistry mapRegistry;
    [SerializeField] private ClientScene scene;

    // -------------------------------------------------------
    // Sync State Machine
    // -------------------------------------------------------
    public SyncStateValue SyncState { get; private set; } = SyncStateValue.Uninitialized;
    public bool IsSceneSyncing => scene.syncMachine.IsSyncing;

    private const float SCENE_SYNC_TIMEOUT = 60f;
    private const float RESOLVE_SYNC_TIMEOUT = 30f;

    // -------------------------------------------------------
    // Token System
    // -------------------------------------------------------
    public int PendingToken = -1;
    public int CurrentToken = -1;

    public bool ResistInteractionState() => SyncState != SyncStateValue.Idle;
    public bool MaintainInteractionState() => SyncState == SyncStateValue.Idle;

    // -------------------------------------------------------
    // Game Data
    // -------------------------------------------------------
    public int CurrentTeamTurn { get; private set; } = -1;
    public List<UnitData> units = new List<UnitData>();
    public List<TeamData> teams = new List<TeamData>();
    public List<int> ownedReadyUnitIds = new List<int>();
    public List<int> enemyReadyUnitIds = new List<int>();

    // -------------------------------------------------------
    // Map
    // -------------------------------------------------------

    [Header("Config")]
    public MatchConfig matchConfig;
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
    public int maxWaitDur = 0;
    public int overtimeDur = 0;
    public int maxOvertimeDur = 0;

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
    // Pattern Preview Data
    // -------------------------------------------------------
    public List<Vector3Int> CurrentTargetableCells { get; set; } = new List<Vector3Int>();
    public List<Vector3Int> CurrentAoECells { get; set; } = new List<Vector3Int>();

    public void ClearPatternData()
    {
        CurrentTargetableCells.Clear();
        CurrentAoECells.Clear();
        isTargetLocked = false;
    }

    public int currentSkillId = -1;
    public bool isOnCell { get; set; } = false;

    // -------------------------------------------------------
    // Snapshot Apply
    // -------------------------------------------------------

    public IEnumerator ApplySnapshot(
        SessionSnapshotData before,
        ResolveData resolve,
        SecretData secret,
        DecisionRequestData decision,
        int token)
    {
        if (SyncState != SyncStateValue.Idle && SyncState != SyncStateValue.Uninitialized)
        {
            Debug.LogWarning($"[ClientMatchSession] ApplySnapshot called during {SyncState}, ignoring");
            yield break;
        }

        SyncState = SyncStateValue.Syncing;
        PendingToken = token;

        if (before.HasMapId) LoadMapData(before.MapId);
        ApplyFullSnapshot(before);
        scene.visualController.UpdateReadyUnitBar();


        LastResolve = resolve;
        LastSecret = secret;
        ApplyDecisionRequest(decision);

        StartCoroutine(scene.syncMachine.RequestSync());

        yield return scene.visualController.ApplyLogs(before.Timeline);
        Debug.Log($"[ClientMatchSession] Logs applied for snapshot token {token}");



        yield return WaitForSceneSyncWithTimeout(SCENE_SYNC_TIMEOUT);
        Debug.Log($"[ClientMatchSession] Initial sync completed for snapshot token {token}");

        if (resolve.HasResolve)
        {
            Debug.Log($"[ClientMatchSession] Starting resolve replay for snapshot token {token}");
            yield return ResolveReplay();
        }

        LastResolve = default;
        CurrentToken = PendingToken;
        SyncState = SyncStateValue.Idle;
        scene.loadingScreen.Hide();
    }

    private IEnumerator WaitForSceneSyncWithTimeout(float timeout)
    {
        float elapsed = 0f;
        while (IsSceneSyncing && elapsed < timeout)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (elapsed >= timeout)
        {
            Debug.LogError($"[ClientMatchSession] Scene sync timeout ({timeout}s)! Force-resetting to idle.");
            scene.syncMachine.ForceReset();
            SyncState = SyncStateValue.Idle;
        }
    }


    void ApplyFullSnapshot(SessionSnapshotData snap)
    {
        if (snap.HasCurrentTeamTurn) CurrentTeamTurn = snap.CurrentTeamTurn;
        if (snap.HasTeams) teams = snap.Teams ?? new List<TeamData>();
        if (snap.HasUnits && snap.Units != null)
        {
            units.RemoveAll(u => !snap.Units.Exists(s => s.Id == u.Id));
            // scene.spawnedUnits.RemoveAll(s => !snap.Units.Exists(u => u.Id == s.unitId));
            foreach (var unit in snap.Units)
            {
                int index = units.FindIndex(u => u.Id == unit.Id);
                if (index == -1) units.Add(unit);
                else units[index] = unit;
            }
        }
        if (snap.HasTimeline)
            Timeline = snap.Timeline;

        Debug.Log($"[ClientMatchSession] Full snapshot applied. TeamTurn:{CurrentTeamTurn}, Units:{units.Count}, Timeline Instant:{Timeline.currentInstant}");
    }


    // -------------------------------------------------------
    // Resolve Replay
    // -------------------------------------------------------

    IEnumerator ResolveReplay()
    {
        if (!LastResolve.HasResolve) yield break;
        if (LastResolve.ResolveResults == null) yield break;

        SyncState = SyncStateValue.Resolving;

        foreach (var result in LastResolve.ResolveResults)
        {
            // Play visual
            var effectVisual = scene.effectRegistry.Get(result.EffectId);
            if (effectVisual != null && effectVisual.resolveDuration > 0f)
            {
                Debug.Log($"[Session] Started resolving effect {effectVisual.effectId}");
                yield return effectVisual.Replay(result, scene, this);
                Debug.Log($"[Session] Ended resolving effect {effectVisual.effectId}");
            }

            // Apply data
            if (result.Partial.HasMapId && result.Partial.HasTeams &&
                result.Partial.HasUnits && result.Partial.HasTimeline)
                ApplyFullSnapshot(result.Partial);
            else
                ApplyPartialSnapshot(result.Partial);

            // Sync scene with timeout
            SyncState = SyncStateValue.Syncing;
            yield return scene.syncMachine.RequestSync();
            SyncState = SyncStateValue.Resolving;
        }
        yield return new WaitForSeconds(0.5f); // small buffer to ensure all visuals are done before allowing interaction/syncing new packs
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
                Debug.Log($"[Session] ResolveData unit: ID{unit.Id}-UID{unit.UId}-HP{unit.CurrentHP}");
            }
        if (partial.HasTimeline)
            Timeline = partial.Timeline;
    }

    void ApplyDecisionRequest(DecisionRequestData data)
    {
        LastDecisionRequest = data;
        waitDur = data.RemainingWaitDuration;
        maxWaitDur = data.RemainingWaitDuration;
        // maxWaitDur = data.MaxWaitDuration;
        overtimeDur = data.RemainingOvertime;
        maxOvertimeDur = data.MaxOvertime;
        // update ready units

        ownedReadyUnitIds.Clear();
        enemyReadyUnitIds.Clear();
        if (data.ReadyUnitIds == null) { return; }
        var team = GetOwnedTeamData();
        if (team == null) return;
        foreach (int unitId in data.ReadyUnitIds)
        {
            if (team.unitIds.Contains(unitId))
            {
                ownedReadyUnitIds.Add(unitId);
            }
            else
            {
                enemyReadyUnitIds.Add(unitId);
            }
        }

        // if (ownedReadyUnitIds.Count == 0) GetOwnedTeamData().isInstantEnded = true;
        // if (enemyReadyUnitIds.Count == 0) GetOwnedTeamData().isInstantEnded = true;
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

    public bool isUnitReady(int unitId)
    {
        if (LastDecisionRequest.ReadyUnitIds == null) return false;
        foreach (var id in LastDecisionRequest.ReadyUnitIds)
            if (id == unitId) return true;
        return false;
    }

    public List<int> GetAllUnitIds()
    {
        var res = new List<int>();
        foreach (var u in units) res.Add(u.Id);
        return res;
    }

    public UnitData GetUnitDataAt(Vector3Int cell) =>
        units.Find(u => u.CurrentCell.x == cell.x && u.CurrentCell.y == cell.y);

    // -------------------------------------------------------
    // Team Helpers
    // -------------------------------------------------------

    //true => eye open = (isInstantEnded = false)
    public int GetTeamIdByUnitId(int unitId)
    {
        foreach (var team in teams)
            if (team.unitIds.Contains(unitId)) return team.teamId;
        return -1;
    }

    public TeamData GetTeamDataById(int teamId) =>
        teams.Find(t => t.teamId == teamId);
    public TeamData GetOwnedTeamData() =>
        teams.Find(t => t.clientId == NetworkManager.Singleton.LocalClientId);


    public TeamData GetEnemyTeamData() =>
        teams.Find(t => t.clientId != NetworkManager.Singleton.LocalClientId);

    public int GetMyTeam()
    {
        if (teams == null || teams.Count < 2) return -1;
        return teams[0].clientId == NetworkManager.Singleton.LocalClientId ? 0 :
               teams[1].clientId == NetworkManager.Singleton.LocalClientId ? 1 : -1;
    }

    public bool IsMyTurn()
    {
        int myTeam = GetMyTeam();
        if (myTeam == -1) return false;
        return LastDecisionRequest.DecisionTeam == myTeam;
    }

    public bool IsMyUnit(int unitId) => GetOwnedTeamData()?.unitIds.Contains(unitId) ?? false;
    public bool IsUnitDataExisting(int unitId) => units.Exists(u => u.Id == unitId);

    // public List<int> GetOwnedReadyUnitIds()
    // {
    //     var owned = new List<int>();
    //     var team = GetOwnedTeamData();
    //     if (team == null) return owned;
    //     foreach (var unit in units)
    //         if (isUnitReady(unit.Id) && team.unitIds.Contains(unit.Id))
    //             owned.Add(unit.Id);
    //     return owned;
    // }

    // -------------------------------------------------------
    // Data Loop
    // -------------------------------------------------------

    private float loopFrequency = 1f;

    public void Init()
    {
        SyncState = SyncStateValue.Idle;
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