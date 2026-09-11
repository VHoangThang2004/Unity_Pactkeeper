using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Tilemaps;

public class ClientVisualController : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private ClientMatchSession session;
    [SerializeField] private ClientScene scene;

    [Header("Camera")]
    public ClientCameraController cameraController;

    [Header("Tile Assets")]
    [SerializeField] private TileBase rangeTileBase;
    [SerializeField] private TileBase enemyRangeTileBase;
    [SerializeField] private TileBase aoePreviewTileBase;

    [Header("UI Elements")]
    [SerializeField] private TextMeshProUGUI instantStatusText;
    [SerializeField] private TextMeshProUGUI instantCounterText;
    [SerializeField] private TextMeshProUGUI waitCounterText;
    [SerializeField] public GameObject waitButtonLayer;
    [SerializeField] public GameObject cancelButtonLayer;
    [SerializeField] public GameObject confirmButton;

    // -------------------------------------------------------
    // State Change — controls visibility of UI elements
    // -------------------------------------------------------

    public void UpdateVisualOnStateChange()
    {
        var state = scene?.clientInteractionSystem?.stateMachine?.currentState;

        if (state is UnitSelectedState)
        {
            ShowWaitButton(false);
            ShowCancelButton(true);
            // confirm only if target locked
            ShowConfirmButton(session.isTargetLocked);
        }
        else if (state is NoneState)
        {
            ClearRange();
            ClearShadowBrute();
            ShowWaitButton(true);
            ShowCancelButton(false);
            ShowConfirmButton(false);
        }
        else if (state is LockedInputState)
        {
            ClearRange();
            ClearShadowBrute();
            ShowWaitButton(false);
            ShowCancelButton(false);
            ShowConfirmButton(false);
        }
    }

    // -------------------------------------------------------
    // Range Display — dumb redraw from session data
    // -------------------------------------------------------

    private HashSet<Vector3Int> lastDrawnTarget = new HashSet<Vector3Int>();
    private HashSet<Vector3Int> lastDrawnAoE = new HashSet<Vector3Int>();

    private void RedrawRange(bool isOwned)
    {
        if (scene.rangeTilemap == null) return;

        var newTarget = new HashSet<Vector3Int>(session.CurrentTargetableCells);
        var newAoE = new HashSet<Vector3Int>(session.CurrentAoECells);

        if (newTarget.SetEquals(lastDrawnTarget) && newAoE.SetEquals(lastDrawnAoE)) return;

        lastDrawnTarget = newTarget;
        lastDrawnAoE = newAoE;

        scene.rangeTilemap.ClearAllTiles();

        TileBase tile = (isOwned && session.isUnitReady(session.selectedUnitId)) ? rangeTileBase : enemyRangeTileBase;
        foreach (var cell in newTarget)
            scene.rangeTilemap.SetTile(cell, tile);

        TileBase aoeTile = aoePreviewTileBase != null ? aoePreviewTileBase : enemyRangeTileBase;
        foreach (var cell in newAoE)
            scene.rangeTilemap.SetTile(cell, aoeTile);

        scene.SetRangeData(newTarget);
    }
    public void ClearRange()
    {
        if (scene.rangeTilemap != null)
            scene.rangeTilemap.ClearAllTiles();
        scene.ClearRangeData();
        lastDrawnTarget.Clear();
        lastDrawnAoE.Clear();
    }

    // -------------------------------------------------------
    // Hover Shadow
    // -------------------------------------------------------

    private List<GameObject> activeShadows = new List<GameObject>();

    public void HoverShadow()
    {
        UnitData unit = session.GetUnitDataById(session.selectedUnitId);
        if (unit == null || scene.movableTilemap == null) return;

        var sceneUnit = scene.GetSceneUnitById(session.selectedUnitId);
        if (sceneUnit == null || sceneUnit.hoverUnit == null) return;

        Vector3Int cell = session.isTargetLocked
            ? session.currentPreviewCell
            : session.currentCellMouseOn;

        if (cell == unit.CurrentCell) { ClearShadowBrute(); return; }

        if (!session.CurrentTargetableCells.Contains(cell))
        {
            ClearShadowBrute();
            return;
        }

        GameObject shadow = sceneUnit.hoverUnit;
        shadow.SetActive(true);
        if (!activeShadows.Contains(shadow))
            activeShadows.Add(shadow);
        shadow.transform.position = scene.movableTilemap.GetCellCenterWorld(cell);
    }
    public void ClearShadowBrute()
    {
        foreach (var shadow in activeShadows)
            if (shadow != null) shadow.SetActive(false);
        activeShadows.Clear();
    }

    // -------------------------------------------------------
    // Button Visibility
    // -------------------------------------------------------

    public void ShowWaitButton(bool isShown)
    {
        waitButtonLayer.SetActive(session.IsMyTurn() && isShown);
    }

    public void ShowCancelButton(bool isShown)
    {
        cancelButtonLayer.SetActive(session.IsMyTurn() && isShown);
    }

    public void ShowConfirmButton(bool isShown)
    {
        confirmButton.SetActive(session.IsMyTurn() && isShown);
    }

    // -------------------------------------------------------
    // UI Loop — data only, dumb updates every tick
    // -------------------------------------------------------

    public void StartUILoop() => StartCoroutine(UILoop());

    private bool turnFlip = false;

    IEnumerator UILoop()
    {
        while (true)
        {
            // Timeline counters
            if (waitCounterText != null)
                waitCounterText.text = $"Time: {session.waitDur} | OT: {session.overtimeDur}";

            if (instantCounterText != null)
                instantCounterText.text = $"{session.Timeline.currentInstant}/{session.Timeline.maxInstant}";

            // Status text
            if (instantStatusText != null)
            {
                if (!session.Timeline.isPaused)
                    instantStatusText.text = "Time is flowing...";
                else if (session.LastResolve.HasResolve && session.SyncState != 0)
                    instantStatusText.text = "Resolving....";
                else
                    instantStatusText.text = session.IsMyTurn() ? "Your turn to decide..." : "Enemy deciding...";
            }

            // Turn flip — update button visibility on turn change
            bool isMyTurn = session.IsMyTurn();
            if (turnFlip != isMyTurn)
            {
                UpdateVisualOnStateChange();
                turnFlip = isMyTurn;
            }

            // Range + shadow — redraw every tick if unit selected
            if (scene?.clientInteractionSystem?.stateMachine?.currentState is UnitSelectedState)
            {
                bool isOwned = session.IsMyUnit(session.selectedUnitId);
                RedrawRange(isOwned);
                HoverShadow();

                // Update confirm button based on target lock
                UnitData unit = session.GetUnitDataById(session.selectedUnitId);
                bool targetLocked = session.isTargetLocked;
                ShowConfirmButton(targetLocked);
            }

            yield return new WaitForSeconds(0.1f);
        }
    }
}