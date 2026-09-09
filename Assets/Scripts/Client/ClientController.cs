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
    [SerializeField] private ClientMapLoader mapLoader;

    // -------------------------------------------------------
    // Map Refs (wired at runtime by ClientMapLoader)
    // -------------------------------------------------------

    public Tilemap tilemap { get; private set; }
    public Tilemap rangeTilemap { get; private set; }
    public GameObject hoverHighlight { get; private set; }

    public void SetMapRefs(Tilemap movable, Tilemap range, GameObject highlight)
    {
        tilemap        = movable;
        rangeTilemap   = range;
        hoverHighlight = highlight;
    }

    // -------------------------------------------------------
    // Range Data (pure data — source of truth for reachability)
    // -------------------------------------------------------

    /// <summary>
    /// Reachable cells for the currently selected unit.
    /// Computed from GridMap — not from rangeTilemap.
    /// rangeTilemap is painted FROM this, never read for logic.
    /// </summary>
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
        rangeTilesData = GridPathfinder.FloodFill(
            clientSession.Map,
            unit.data.CurrentCell,
            unit.data.MoveRange,
            occupiedCells
        );
    }

    public void ClearRangeData()
    {
        rangeTilesData = new HashSet<Vector3Int>();
    }

    public bool IsInRange(Vector3Int cell) => rangeTilesData.Contains(cell);

    // -------------------------------------------------------
    // Occupied Cells Helper
    // -------------------------------------------------------

    public HashSet<Vector3Int> GetOccupiedCells(ClientUnit excludeUnit)
    {
        var occupied = new HashSet<Vector3Int>();
        foreach (var u in spawnedUnits)
        {
            if (u != excludeUnit)
                occupied.Add(u.data.CurrentCell);
        }
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

    public void OnActionStandBy()
    {
        Debug.Log("[ClientController] Requesting stand by...");
        interactionSystem.stateMachine.OnActionStandBy();
    }

    // -------------------------------------------------------
    // Unit Registry
    // -------------------------------------------------------

    public List<ClientUnit> GetAllUnits() => spawnedUnits;

    public ClientUnit GetClientUnit(int instanceId)
        => spawnedUnits.Find(u => u.data.Id == instanceId);

    public ClientUnit GetClientUnitAt(Vector3Int cell)
        => spawnedUnits.Find(u => u.data.CurrentCell == cell);

    public void RegisterUnit(int instanceId, ClientUnit unit)
    {
        if (spawnedUnits.Contains(unit))
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
        unitLibrary.Init();
        unitPrefabRegistry.Init(unitLibrary);
        input.OnLeftClick += HandleClick;
        bridge.RequestInitialStateServerRpc();
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
    // Bridge -> Server
    // -------------------------------------------------------

    public void TryMoveAndStandBy(int unitId, Vector3Int target)
    {
        bridge.SendMoveServerRpc(unitId, target);
    }

    public void TryMoveAndSkill(int unitId, Vector3Int target, int skillId)
    {
        Debug.Log($"[ClientController] TryMoveAndSkill: unit {unitId} -> {target} with skill {skillId}");
    }

    public void TryTestCell(Vector3Int target)
    {
        bridge.TestCellServerRpc(target.x, target.y);
    }

    public void SendActWaitDecision(bool wantsToAct)
    {
        bridge.SendActWaitDecisionServerRpc(wantsToAct);
    }

    public void SendActionDecision(int unitId, Vector3Int target)
    {
        bridge.SendActionDecisionServerRpc(unitId, target);
    }

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
        var occupied = GetOccupiedCells(unit);
        unit.MoveTo(tilemap, target, occupied);
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

    public void OnInitialStateReceived(SessionSnapshotData snapshot)
    {
        Debug.Log($"[Client] Received snapshot with {snapshot.Units?.Count ?? 0} units");
        receivedInitialSnapshot = true;
        clientSession.ApplySnapshot(snapshot);

        if (!mapLoaded)
        {
            bool ok = mapLoader.LoadMap(clientSession.MapAsset);
            if (!ok)
            {
                Debug.LogError("[ClientController] Map load failed.");
                return;
            }
            mapLoaded = true;
            interactionSystem.Init();
        }

        if (snapshot.Units != null)
            foreach (var unitData in snapshot.Units)
                spawner.SpawnUnit(unitData);

        interactionSystem.ForceNoneState();
    }

    public void OnTimelineTick(TimelineData data)
    {
        clientSession.ApplyTimelineTick(data);
        Debug.Log($"[Client] Timeline tick instant {data.currentInstant}/{data.maxInstant}, ready: {data.readyUnitIds?.Count ?? 0}");
    }

    public void OnDecisionRequest(DecisionRequestData data)
    {
        clientSession.ApplyDecisionRequest(data);
        var ownedTeam = clientSession.GetOwnedTeamData();
        if (ownedTeam == null) return;
        int myTeam = clientSession.teams.IndexOf(ownedTeam);
        bool isMyTurn = (myTeam == data.DecisionTeam);
        Debug.Log($"[Client] Decision request team {data.DecisionTeam} deciding. My turn: {isMyTurn}");
    }

    public void OnUnitStepReset(int unitId, int newStep)
    {
        clientSession.ApplyUnitStepReset(unitId, newStep);
        Debug.Log($"[Client] Unit {unitId} step reset to {newStep}");
    }
}