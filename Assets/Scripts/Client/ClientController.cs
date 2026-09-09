using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Tilemaps;

public class ClientController : MonoBehaviour
{
    [Header("Bridge")]
    [SerializeField] private SyncedBridge bridge;

    [Header("Data")]
    [SerializeField] public UnitLibrary unitLibrary;
    [SerializeField] public UnitPrefabRegistry unitPrefabRegistry;

    [Header("Refs — Scene")]
    [SerializeField] public InputReader input;
    [SerializeField] public TileBase rangeTileBase;
    [SerializeField] public TileBase enemyRangeTileBase;
    [SerializeField] public ClientCameraController cameraController;
    [SerializeField] public Camera cam;
    [SerializeField] public Vector3 offset;
    [SerializeField] public EventSystem eventSystem;

    [Header("Sub Systems")]
    [SerializeField] private ClientInteractionSystem interactionSystem;
    [SerializeField] private ClientSpawner spawner;
    [SerializeField] public ClientMatchSession clientSession;
    [SerializeField] public ClientVisualController visualController;
    [SerializeField] public ActionMenuUI ActionMenu;

    [SerializeField] public GameObject ActWaitDecisionCanvas;
    [SerializeField] private ClientMapLoader mapLoader;

    // -------------------------------------------------------
    // Map Refs (wired at runtime by ClientMapLoader)
    // -------------------------------------------------------

    public Tilemap tilemap { get; private set; }
    public Tilemap rangeTilemap { get; private set; }
    public GameObject hoverHighlight { get; private set; }

    public void SetMapRefs(Tilemap movable, Tilemap range, GameObject highlight)
    {
        tilemap = movable;
        rangeTilemap = range;
        hoverHighlight = highlight;
    }

    // -------------------------------------------------------
    // Range Data
    // -------------------------------------------------------

    public HashSet<Vector3Int> rangeTilesData { get; private set; } = new HashSet<Vector3Int>();

    public void ComputeRangeData(ClientUnit unit)
    {
        if (clientSession.Map == null)
        {
            Debug.LogError("[ClientController] Map not loaded — cannot compute range.");
            rangeTilesData = new HashSet<Vector3Int>();
            return;
        }
        var occupiedCells = GetOccupiedCells(unit);
        rangeTilesData = GridPathfinder.FloodFill(clientSession.Map, unit.data.CurrentCell, unit.data.MoveRange, occupiedCells);
    }

    public void ClearRangeData() => rangeTilesData = new HashSet<Vector3Int>();
    public bool IsInRange(Vector3Int cell) => rangeTilesData.Contains(cell);

    // -------------------------------------------------------
    // Occupied Cells
    // -------------------------------------------------------

    public HashSet<Vector3Int> GetOccupiedCells(ClientUnit excludeUnit)
    {
        var occupied = new HashSet<Vector3Int>();
        foreach (var u in spawnedUnits)
            if (u != excludeUnit)
                occupied.Add(u.data.CurrentCell);
        return occupied;
    }

    // -------------------------------------------------------
    // State Data
    // -------------------------------------------------------

    public Vector3Int currentCell;
    public bool isOnCell = false;
    public ClientUnit selectedUnit { get; private set; }
    public bool IsOwnedUnit { get; private set; }

    private List<ClientUnit> spawnedUnits = new();
    private bool receivedInitialSnapshot = false;
    private bool mapLoaded = false;
    private bool initDone = false;

    private int trialErrorCount = 0;
    private int totalErrorCount = 0;

    public void OnActionStandBy()
    {
        Debug.Log("[ClientController] Requesting stand by...");
        interactionSystem.stateMachine.OnActionStandBy();
    }

    public void OnDecisionActWait(bool wantsToAct)
    {
        Debug.Log($"[ClientController] Requesting action:...{wantsToAct}");
        SendActWaitDecision(wantsToAct);
    }


    // -------------------------------------------------------
    // Unit Registry
    // -------------------------------------------------------

    public List<ClientUnit> GetAllUnits() => spawnedUnits;
    public ClientUnit GetClientUnit(int instanceId) => spawnedUnits.Find(u => u.data.Id == instanceId);
    public ClientUnit GetClientUnitAt(Vector3Int cell) => spawnedUnits.Find(u => u.data.CurrentCell == cell);

    public void RegisterUnit(int instanceId, ClientUnit unit)
    {
        if (spawnedUnits.Exists(u => u.data.Id == instanceId))
        {
            Debug.LogWarning($"[ClientController] Unit {instanceId} already registered!");
            return;
        }
        spawnedUnits.Add(unit);
    }

    // -------------------------------------------------------
    // Selection
    // -------------------------------------------------------

    public void SetSelectedUnit(ClientUnit unit) => selectedUnit = unit;
    public void ClearSelectedUnit() => selectedUnit = null;
    public void SetIsOwnedUnit(bool owned) => IsOwnedUnit = owned;

    // -------------------------------------------------------
    // Unity
    // -------------------------------------------------------

    void Start()
    {
        StartCoroutine(ClientGuard());
        StartCoroutine(InitSequence());
    }

    void OnDestroy()
    {
        input.OnLeftClick -= HandleClick;
    }

    void Update()
    {
        cameraController.Move(input.MoveInput, input.IsFastMove);
        cameraController.Zoom(input.ZoomInput);
        if (mapLoaded)
            interactionSystem.HandleTileHover();
    }

    void HandleClick()
    {
        if (!mapLoaded) return;
        if (!isOnCell) return;
        if (EventSystem.current.IsPointerOverGameObject()) return;
        interactionSystem.HandleTileClick(currentCell);
    }

    // -------------------------------------------------------
    // Guard
    // -------------------------------------------------------

    IEnumerator ClientGuard()
    {
        while (true)
        {
            if (trialErrorCount > 10 || totalErrorCount > 50)
            {
                Debug.LogError("[ClientController] Too many errors — something is critically wrong!");
                // TODO: disconnect and return to main menu
                yield break;
            }
            yield return new WaitForSeconds(60f);
            trialErrorCount = 0;
        }
    }

    void IncrementErrors()
    {
        trialErrorCount++;
        totalErrorCount++;
    }

    // -------------------------------------------------------
    // Init Sequence
    // -------------------------------------------------------

    IEnumerator InitSequence()
    {
        while (!TryInitRegistries())
        {
            Debug.LogError("[ClientController] Registry init failed, retrying...");
            IncrementErrors();
            yield return new WaitForSeconds(1f);
        }

        input.OnLeftClick += HandleClick;
        bridge.RequestInitialStateServerRpc();

        // Wait for map — loaded by ApplyFull when Full snapshot arrives
        yield return new WaitUntil(() => mapLoaded);

        initDone = true;
        Debug.Log("[ClientController] Client initialized successfully.");
        visualController.StartUILoop();
    }

    bool TryInitRegistries()
    {
        try
        {
            unitLibrary.Init();
            unitPrefabRegistry.Init(unitLibrary);
            return true;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[ClientController] Registry init exception: {e.Message}");
            return false;
        }
    }

    // -------------------------------------------------------
    // Bridge -> Server
    // -------------------------------------------------------

    public void TryMoveAndSkill(int unitId, Vector3Int target, int skillId) => Debug.Log($"[ClientController] TryMoveAndSkill: unit {unitId} -> {target} skill {skillId}");
    public void TryTestCell(Vector3Int target) => bridge.TestCellServerRpc(target.x, target.y);
    public void SendActWaitDecision(bool wantsToAct) => bridge.SendActWaitDecisionServerRpc(wantsToAct);
    public void SendActionDecision(int unitId, Vector3Int target) => bridge.SendActionDecisionServerRpc(unitId, target);

    // -------------------------------------------------------
    // Bridge -> Client
    // -------------------------------------------------------

    public void OnMoveConfirmed(int instanceId, Vector3Int target)
    {
        interactionSystem.ForceNoneState();
        var unit = GetClientUnit(instanceId);
        if (unit == null)
        {
            Debug.LogError($"[ClientController] OnMoveConfirmed: unit {instanceId} not found!");
            return;
        }
        unit.MoveTo(tilemap, target, GetOccupiedCells(unit));
    }

    public void OnServerResult(int x, int y, bool walkable)
    {
        Debug.Log($"[ClientController] Server says ({x},{y}) = {walkable}");
        interactionSystem.ForceNoneState();
    }

    public void OnUnitSpawned(UnitData unitData)
    {
        if (!receivedInitialSnapshot)
        {
            Debug.Log($"[Client] Deferring spawn {unitData.Id} until snapshot");
            return;
        }
        spawner.SpawnUnit(unitData);
        interactionSystem.ForceNoneState();
    }

    /// <summary>
    /// Full snapshot — rebuild everything: map + all units.
    /// Called on connect, reconnect, or match end.
    /// </summary>
    public void OnInitialStateReceived(SessionSnapshotData snapshot)
    {
        Debug.Log($"[Client] Full snapshot received with {snapshot.Units?.Count ?? 0} units");
        receivedInitialSnapshot = true;
        clientSession.ApplySnapshot(snapshot);
        StartCoroutine(ApplyFull(snapshot));
    }

    /// <summary>
    /// Mid snapshot bundled with decision request.
    /// Data layer only — reposition existing units, spawn missing ones.
    /// No despawn/respawn — no visual interruption.
    /// </summary>
    public void OnMidSnapshotWithDecision(SessionSnapshotData snapshot, DecisionRequestData decision)
    {
        Debug.Log($"[Client] Mid snapshot + decision (team {decision.DecisionTeam})");

        // Update data layer
        clientSession.ApplySnapshot(snapshot);

        // Update visual layer — reposition only, no despawn/respawn
        if (snapshot.Units != null && mapLoaded)
        {
            foreach (var unitData in snapshot.Units)
            {
                var existing = GetClientUnit(unitData.Id);
                if (existing == null)
                    spawner.SpawnUnit(unitData);
                else
                    existing.SetPosition(tilemap, unitData.CurrentCell);
            }
        }

        // Decision UI
        clientSession.ApplyDecisionRequest(decision);
    }

    // -------------------------------------------------------
    // Full Snapshot Apply (map + units)
    // -------------------------------------------------------

    IEnumerator ApplyFull(SessionSnapshotData snapshot)
    {
        // Reload map
        mapLoaded = false;
        DespawnAllUnits();

        while (!mapLoaded)
        {
            bool ok = mapLoader.LoadMap(clientSession.MapAsset);
            if (ok)
            {
                mapLoaded = true;
                interactionSystem.Init();
            }
            else
            {
                Debug.LogError("[ClientController] Full snapshot: map load failed, retrying...");
                IncrementErrors();
                yield return new WaitForSeconds(1f);
            }
        }

        // Spawn all units fresh
        if (snapshot.Units != null)
            foreach (var unitData in snapshot.Units)
                spawner.SpawnUnit(unitData);

        interactionSystem.ForceNoneState();
    }

    // -------------------------------------------------------
    // Spawn Helpers
    // -------------------------------------------------------

    void DespawnAllUnits()
    {
        foreach (var unit in spawnedUnits)
            if (unit != null)
                Destroy(unit.gameObject);
        spawnedUnits.Clear();
    }

    // -------------------------------------------------------
    // Timeline / Decision Handlers
    // -------------------------------------------------------

    public void OnTimelineTick(TimelineData data)
    {
        clientSession.ApplyTimelineTick(data);
        // Update step UI on all units
        foreach (var unit in spawnedUnits)
            unit.UpdateStepUI(unit.data.CurrentStep);
        Debug.Log($"[Client] Timeline tick instant {data.currentInstant}/{data.maxInstant}, ready: {data.readyUnitIds?.Count ?? 0}");
    }

    public void OnDecisionRequest(DecisionRequestData data)
    {
        clientSession.ApplyDecisionRequest(data);
    }

    public void OnUnitStepReset(int unitId, int newStep)
    {
        clientSession.ApplyUnitStepReset(unitId, newStep);
        var unit = GetClientUnit(unitId);
        if (unit != null)
            unit.UpdateStepUI(newStep);
        Debug.Log($"[Client] Unit {unitId} step reset to {newStep}");
    }
}