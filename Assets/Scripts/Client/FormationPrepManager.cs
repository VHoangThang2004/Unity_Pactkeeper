using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Pre-battle formation step (PvP / free-loadout story). Server drives it via PrepInfo/PrepEnded RPCs.
/// Player drags owned units from the list onto their team's spawn points, clicks a placed unit to edit it
/// in the 8_UnitConfig overlay, then presses Ready. Formation is saved with the same API as 7_UnitList;
/// spawn positions go to the server via SubmitPrepServerRpc.
/// </summary>
public class FormationPrepManager : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private ClientScene scene;
    [SerializeField] private ClientMatchSession session;
    [SerializeField] private ClientMapLoader mapLoader;
    [SerializeField] private BackendConfig backendConfig;
    [SerializeField] private SceneConfig sceneConfig;
    [SerializeField] private ClassDefinitionRegistry classLibrary;

    [Header("UI")]
    [SerializeField] private GameObject panel;
    [SerializeField] private Transform unitListContainer;
    [SerializeField] private GameObject unitContainerPrefab; // same prefab as 7_UnitList
    [SerializeField] private TextMeshProUGUI countdownText;
    [SerializeField] private Button readyButton;
    [SerializeField] private Color placedTint = new Color(1f, 1f, 1f, 0.4f);

    [Header("Rules")]
    [SerializeField] private int maxUnits = 5;

    public bool IsActive { get; private set; }

    private float deadline;
    private bool ready;
    private Vector2Int[] spawns = Array.Empty<Vector2Int>();
    private List<OwnedUnitDto> ownedUnits = new();
    private readonly Dictionary<int, OwnedUnitDto> placed = new();   // spawn index -> unit
    private readonly Dictionary<int, GameObject> ghosts = new();     // spawn index -> preview
    private readonly Dictionary<string, UnitContainer> listItems = new();
    private bool saving, dirty;

    int MaxUnits => Mathf.Min(maxUnits, spawns.Length);

    void Awake()
    {
        panel.SetActive(false);
        readyButton.onClick.AddListener(OnClickReady);
    }

    void Update()
    {
        if (IsActive && countdownText != null)
            countdownText.text = Mathf.CeilToInt(Mathf.Max(0f, deadline - Time.time)).ToString();
    }

    // -------------------------------------------------------
    // Server-driven lifecycle
    // -------------------------------------------------------

    public void Begin(string mapId, int teamId, float secondsLeft)
    {
        IsActive = true;
        SyncTimer(secondsLeft);
        StartCoroutine(BeginSequence(mapId, teamId));
    }

    public void SyncTimer(float secondsLeft) => deadline = Time.time + secondsLeft;

    IEnumerator BeginSequence(string mapId, int teamId)
    {
        classLibrary.Init();

        // Show the real battlefield early; the first snapshot later sees the same mapId and skips reloading
        session.LoadMapData(mapId);
        yield return mapLoader.Load(_ => { });
        if (!IsActive) yield break;

        spawns = session.MapAsset.teamSpawns[teamId].positions;
        // Highlight own spawn points with the map's range tile (same look as move range in battle)
        foreach (var s in spawns)
            scene.rangeTilemap.SetTile((Vector3Int)s, scene.mapPrefab.rangeTileBase);
        if (spawns.Length > 0) scene.cameraController.CenterOn(CellWorld(spawns[0]));

        List<int> formation = new();
        yield return FormationApi.GetOwnedUnits(backendConfig, u => ownedUnits = u ?? new List<OwnedUnitDto>());
        yield return FormationApi.GetFormation(backendConfig, f => formation = f);
        if (!IsActive) yield break;

        // Saved formation starts on spawn points in order
        for (int i = 0; i < formation.Count && i < MaxUnits; i++)
        {
            var unit = ownedUnits.Find(u => u.unitDefinitionUId == formation[i]);
            if (unit != null) placed[i] = unit;
        }

        BuildUnitList();
        Refresh();
        scene.input.OnLeftClick += OnFieldClick;
        scene.loadingScreen.Hide();
        panel.SetActive(true);
        RequestSave();
    }

    public void End()
    {
        if (!IsActive) return;
        IsActive = false;
        scene.input.OnLeftClick -= OnFieldClick;

        var overlay = SceneManager.GetSceneByName(sceneConfig.unitConfigScene);
        if (overlay.isLoaded) SceneManager.UnloadSceneAsync(overlay);
        UnitConfigManager.IsOverlay = false;

        foreach (var g in ghosts.Values) Destroy(g);
        if (scene.rangeTilemap != null) scene.rangeTilemap.ClearAllTiles();
        foreach (var item in listItems.Values) Destroy(item.gameObject);
        ghosts.Clear(); listItems.Clear(); placed.Clear();
        panel.SetActive(false);
    }

    // -------------------------------------------------------
    // Unit list + drag/drop
    // -------------------------------------------------------

    void BuildUnitList()
    {
        ownedUnits.Sort((a, b) => a.unitDefinitionUId.CompareTo(b.unitDefinitionUId));
        foreach (var unit in ownedUnits)
        {
            var container = Instantiate(unitContainerPrefab, unitListContainer).GetComponent<UnitContainer>();
            var prefab = scene.unitPrefabRegistry.Get(unit.unitDefinitionUId);
            container.Setup(
                unit.ownedUnitId,
                unit.unitDefinitionUId,
                prefab != null ? prefab.GetComponent<ClientUnit>()?.unitImg : null,
                scene.unitPrefabRegistry.GetName(unit.unitDefinitionUId),
                unit.unlockedClassIds.Length > 0 ? classLibrary.GetFullFrame(unit.unlockedClassIds[0]) : null,
                unit.unlockedClassIds.Length > 1 ? classLibrary.GetHalfFrame(unit.unlockedClassIds[1]) : null,
                false);
            container.gameObject.AddComponent<PrepDragItem>().Init(this, unit);
            listItems[unit.ownedUnitId] = container;
        }
    }

    /// <summary>
    /// Called by PrepDragItem. Drop on own spawn point = place (swap if occupied);
    /// drop anywhere else = take the unit out of the formation.
    /// </summary>
    public void DropUnit(OwnedUnitDto unit, Vector2 screenPos)
    {
        if (!IsActive || ready) return;

        int target = SpawnIndexAt(screenPos);
        int current = -1;
        foreach (var (idx, u) in placed)
            if (u.ownedUnitId == unit.ownedUnitId) current = idx;

        if (target < 0)
        {
            if (current < 0 || placed.Count <= 1) return; // never leave the formation empty
            placed.Remove(current);
        }
        else
        {
            placed.TryGetValue(target, out var occupant);
            if (current >= 0)
            {
                placed.Remove(current);
                if (occupant != null) placed[current] = occupant; // swap
            }
            else if (occupant == null && placed.Count >= MaxUnits) return;
            placed[target] = unit;
        }

        Refresh();
        RequestSave();
    }

    void OnFieldClick()
    {
        if (ready || EventSystem.current.IsPointerOverGameObject()) return;
        if (SceneManager.GetSceneByName(sceneConfig.unitConfigScene).isLoaded) return;

        int idx = SpawnIndexAt(scene.input.MouseScreenPosition);
        if (idx < 0 || !placed.TryGetValue(idx, out var unit)) return;

        // Same flow as 7_UnitList "Configure", but additive so the match stays alive
        PlayerSession.SelectedOwnedUnitId = unit.ownedUnitId;
        UnitConfigManager.IsOverlay = true;
        SceneManager.LoadSceneAsync(sceneConfig.unitConfigScene, LoadSceneMode.Additive);
    }

    void OnClickReady()
    {
        if (ready || placed.Count == 0) return;
        ready = true;
        readyButton.interactable = false;
        RequestSave();
    }

    // -------------------------------------------------------
    // Visuals
    // -------------------------------------------------------

    void Refresh()
    {
        foreach (var g in ghosts.Values) Destroy(g);
        ghosts.Clear();

        foreach (var (idx, unit) in placed)
        {
            // Unit prefab without ClientUnit.Init() is a plain visual — nothing reads session data
            var prefab = scene.unitPrefabRegistry.Get(unit.unitDefinitionUId);
            if (prefab != null) ghosts[idx] = Instantiate(prefab, CellWorld(spawns[idx]), Quaternion.identity);
        }

        foreach (var (id, item) in listItems)
            item.SetTint(placed.Values.Any(u => u.ownedUnitId == id) ? placedTint : Color.white);

        readyButton.interactable = !ready && placed.Count > 0;
    }

    // -------------------------------------------------------
    // Save (serialized so PUTs never land out of order)
    // -------------------------------------------------------

    void RequestSave()
    {
        dirty = true;
        if (!saving) StartCoroutine(SaveLoop());
    }

    IEnumerator SaveLoop()
    {
        saving = true;
        while (dirty && IsActive)
        {
            dirty = false;
            var ordered = placed.OrderBy(p => p.Key).ToList();
            int[] uIds = ordered.Select(p => p.Value.unitDefinitionUId).ToArray();
            int[] indices = ordered.Select(p => p.Key).ToArray();
            bool readyNow = ready;

            yield return FormationApi.SaveFormation(backendConfig, uIds.ToList());
            if (IsActive) scene.controller.bridge.SubmitPrepServerRpc(uIds, indices, readyNow);
        }
        saving = false;
    }

    // -------------------------------------------------------
    // Helpers
    // -------------------------------------------------------

    Vector3 CellWorld(Vector2Int cell) => scene.movableTilemap.GetCellCenterWorld((Vector3Int)cell);

    int SpawnIndexAt(Vector2 screenPos)
    {
        Vector3 world = scene.cam.ScreenToWorldPoint(screenPos);
        world.z = 0;
        Vector3Int cell = scene.movableTilemap.WorldToCell(world);
        return Array.FindIndex(spawns, s => s.x == cell.x && s.y == cell.y);
    }
}
