using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Scene object registry. Single source of truth for all runtime scene references.
/// Parallel to ClientMatchSession (data) — this holds the live GameObjects and Unity refs.
/// Wire static refs in Inspector. Dynamic refs (units, map) wired at runtime.
/// </summary>
public class ClientScene : MonoBehaviour
{
    // -------------------------------------------------------
    // Static Scene Refs (Inspector)
    // -------------------------------------------------------

    public Camera cam;
    public ClientVisualController visualController;
    public ClientMatchSession session;
    public ClientInteractionSystem clientInteractionSystem;
    public SyncedBridge bridge;
    public Vector3 offset;
    public InputReader input;
    public ClientCameraController cameraController;
    public ClientSyncMachine syncMachine;

    [SerializeField] public SkillLibrary skillLibrary;
    [SerializeField] public UnitPrefabRegistry unitPrefabRegistry;
    [SerializeField] public UnitLibrary unitLibrary;
    [SerializeField] public ClientEffectRegistry effectRegistry;

    // -------------------------------------------------------
    // Map Refs (wired by ClientMapLoader at runtime)
    // -------------------------------------------------------

    public Tilemap movableTilemap { get; private set; }
    public Tilemap rangeTilemap { get; private set; }
    public GameObject hoverHighlight { get; private set; }

    public void SetMapRefs(Tilemap movable, Tilemap range, GameObject highlight)
    {
        movableTilemap = movable;
        rangeTilemap = range;
        hoverHighlight = highlight;
    }

    public void ClearMapRefs()
    {
        movableTilemap = null;
        rangeTilemap = null;
        hoverHighlight = null;
    }

    // -------------------------------------------------------
    // Unit Registry (wired by ClientSpawner at runtime)
    // -------------------------------------------------------

    public List<ClientUnit> spawnedUnits = new List<ClientUnit>();

    public void RegisterUnit(ClientUnit unit)
    {
        if (spawnedUnits.Contains(unit)) return;
        spawnedUnits.Add(unit);
    }

    public void UnregisterUnit(ClientUnit unit)
    {
        spawnedUnits.Remove(unit);
    }

    public void ClearUnits()
    {
        foreach (var unit in spawnedUnits)
            if (unit != null) Destroy(unit.gameObject);
        spawnedUnits.Clear();
    }

    // public bool AllUnitsComepletedResolve()
    // {
    //     foreach(ClientUnit unit in spawnedUnits)
    //     {
    //         if(unit.isResolvingAnimation) return false;
    //     }
    //     return true;
    // }

    public ClientUnit GetSceneUnitById(int unitId) => spawnedUnits.Find(u => u.unitId == unitId);
    public List<ClientUnit> GetAllSceneUnits() => spawnedUnits;

}