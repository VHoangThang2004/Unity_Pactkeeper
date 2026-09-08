using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class ClientController : MonoBehaviour
{
    [Header("Bridge")]
    [SerializeField] private SyncedBridge bridge;

    [Header("Data")]
    [SerializeField] public UnitLibrary unitLibrary;
    [SerializeField] public UnitPrefabRegistry unitPrefabRegistry;
    

    [Header("Refs")]
    [SerializeField] public InputReader input;
    [SerializeField] public Tilemap tilemap;
    [SerializeField] public Tilemap rangeTilemap;
    [SerializeField] public TileBase rangeTileBase;
    [SerializeField] public TileBase enemyRangeTileBase;
    [SerializeField] public ClientCameraController cameraController;
    [SerializeField] public Camera cam;
    [SerializeField] public GameObject hoverHighlight;
    [SerializeField] public Vector3 offset;
    

    [Header("Sub Systems")]
    [SerializeField] private ClientInteractionSystem interactionSystem;
    [SerializeField] private ClientSpawner spawner;

    
    public Vector3Int currentCell;
    public bool isOnCell = false;


    // -------------------------------------------------------
    // Data — other scripts access through here
    // -------------------------------------------------------

    public ClientUnit selectedUnit;
    public bool IsOwnedUnit;

    private List<ClientUnit> spawnedUnits = new();
    public List<ClientUnit> GetAllUnits()
    {
        return spawnedUnits;
    }
    public ClientUnit GetClientUnit(int instanceId)
    {
        return spawnedUnits.Find(u => u.data.Id == instanceId);
    }

    public ClientUnit GetClientUnitAt(Vector3Int cell)
    {
        return spawnedUnits.Find(u => u.data.CurrentCell == cell);
    }

    public void RegisterUnit(int instanceId, ClientUnit unit)
    {
        if (spawnedUnits.Contains(unit))
        {
            Debug.LogWarning($"[ClientController] Unit {instanceId} already registered!");
            return;
        }
        spawnedUnits.Add(unit);
    }

    public void SetSelectedUnit(ClientUnit unit)
    {
        selectedUnit = unit;
    }

    public void ClearSelectedUnit()
    {
        selectedUnit = null;
    }

    // -------------------------------------------------------
    // Unity
    // -------------------------------------------------------

    //this starts works even under reconnection scenario, because the server will resend the initial state to client upon reconnection, allowing client to re-initialize its view and data
    void Start()
    {
        unitLibrary.Init();
        unitPrefabRegistry.Init(unitLibrary);
        input.OnLeftClick += HandleClick;
        //rpc sends request snapshot of current game state, including map and units, so that client can initialize its view
        bridge.RequestInitialStateServerRpc();
    }

    void OnDestroy()
    {
        input.OnLeftClick -= HandleClick;
    }

    void Update()
    {
        HandleCamera();
        HandleHover();
    }

    // -------------------------------------------------------
    // Input → Intent
    // -------------------------------------------------------

    void HandleCamera()
    {
        cameraController.Move(input.MoveInput, input.IsFastMove);
        cameraController.Zoom(input.ZoomInput);
    }

    void HandleHover()
    {
        interactionSystem.HandleTileHover();
    }

    void HandleClick()
    {
        if (!isOnCell) return;
        interactionSystem.HandleTileClick(currentCell);
    }


    // -------------------------------------------------------
    // Bridge → Server (Client sends)
    // -------------------------------------------------------

    public void TryMoveUnit(int unitId, Vector3Int target)
    {
        bridge.SendMoveServerRpc(unitId, target);
    }

    public void TryTestCell(Vector3Int target)
    {
        bridge.TestCellServerRpc(target.x, target.y);
    }

    // -------------------------------------------------------
    // Bridge → Client (Server responds)
    // -------------------------------------------------------


    public void OnMoveConfirmed(int instanceId, Vector3Int target)
    {
        var unit = GetClientUnit(instanceId);
        if (unit == null)
        {
            Debug.LogError($"[ClientController] OnMoveConfirmed: unit {instanceId} not found!");
            return;
        }
        unit.MoveTo(tilemap, target);
    }

    public void OnServerResult(int x, int y, bool walkable)
    {
        Debug.Log($"[ClientController] Server says ({x},{y}) = {walkable}");
    }



    [Header("State")]
    private bool receivedInitialSnapshot = false;
    [SerializeField] public ClientMatchSession clientSession;

    public void OnUnitSpawned(UnitData unitData)
    {
        // Queue spawns until snapshot establishes baseline
        if (!receivedInitialSnapshot)
        {
            Debug.Log($"[Client] Deferring spawn {unitData.Id} until snapshot");
            return;
        }

        spawner.SpawnUnit(unitData);
    }

    public void OnInitialStateReceived(SessionSnapshotData snapshot)
    {
        Debug.Log($"[Client] Received snapshot with {snapshot.Units?.Count ?? 0} units");

        receivedInitialSnapshot = true;
        clientSession.ApplySnapshot(snapshot);

        // Spawn all units from snapshot
        if (snapshot.Units != null)
        {
            foreach (var unitData in snapshot.Units)
            {
                spawner.SpawnUnit(unitData);
            }
        }
    }
}