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

    [Header("UI Elements")]
    [SerializeField] private TextMeshProUGUI instantStatusText;
    [SerializeField] private TextMeshProUGUI instantCounterText;
    [SerializeField] private TextMeshProUGUI waitCounterText;
    [SerializeField] public GameObject waitButtonLayer;
    [SerializeField] public GameObject cancelButtonLayer;
    [SerializeField] public GameObject confirmButton;

    // -------------------------------------------------------
    // Range Display
    // -------------------------------------------------------
    public void UpdateVisualOnStateChange()
    {
        if (scene?.clientInteractionSystem?.stateMachine?.currentState is MovePreviewState)
        {
            bool isOwned = session.IsMyUnit(session.selectedUnitId);
            ShowMoveRange(isOwned);
            ShowWaitButton(false);
            ShowCancelButton(true);
            ShowConfirmButton(true);
            ClearShadowBrute();
            scene.visualController.HoverShadow();
        }


        if (scene?.clientInteractionSystem?.stateMachine?.currentState is SkillPreviewState)
        {
            // bool isOwned = session.IsMyUnit(session.selectedUnitId);
            ShowSkillRange();
            ShowWaitButton(false);
            ShowCancelButton(true);
            ShowConfirmButton(true);
            ClearShadowBrute();
            //hoverSkillEffect
        }
        if (scene?.clientInteractionSystem?.stateMachine?.currentState is UnitSelectedState)
        {
            bool isOwned = session.IsMyUnit(session.selectedUnitId);
            ShowMoveRange(isOwned);
            ShowWaitButton(false);
            ShowCancelButton(true);
            ShowConfirmButton(true);
        }
        if (scene?.clientInteractionSystem?.stateMachine?.currentState is NoneState)
        {
            ClearRange();
            ClearShadowBrute();
            ShowWaitButton(true);
            ShowCancelButton(false);
            ShowConfirmButton(false);
        }
        if (scene?.clientInteractionSystem?.stateMachine?.currentState is LockedInputState)
        {
            ClearRange();
            ClearShadowBrute();
            ShowWaitButton(false);
            ShowCancelButton(false);
            ShowConfirmButton(false);
        }
    }
    private void ShowSkillRange()
    {
        Vector3Int startCell = session.GetUnitDataById(session.selectedUnitId).CurrentCell;
        ActionDefinition skill = scene.actionLibrary.Get(session.currentSkillId);
        if (skill == null) return;
        if (scene.rangeTilemap == null || session.Map == null) return;

        ClearRange();

        var alliedCells = new HashSet<Vector3Int>();
        var enemyCells = new HashSet<Vector3Int>();

        foreach (UnitData u in session.units)
        {
            if (session.IsMyUnit(u.Id))
                alliedCells.Add(u.CurrentCell);
            else
                enemyCells.Add(u.CurrentCell);
        }

        var range = GridPathfinder.FloodFill(
            session.Map,
            startCell,
            skill.range, // <- + unit skill range buff/debuff (in the future scales)
            skill.targeting,
            !skill.isUnblockable,
            alliedCells,
            enemyCells);

        scene.SetRangeData(range); // set range data to unit, Interaction StateMachine will fetch from there

        foreach (var cell in scene.rangeTilesData)
            scene.rangeTilemap.SetTile(cell, rangeTileBase); // for now only show range of owned units skills.
    }

    private void ShowMoveRange(bool isOwned)
    {
        if (scene.rangeTilemap == null) return;

        TileBase tile = isOwned ? rangeTileBase : enemyRangeTileBase;
        ClearRange();
        UnitData unit = session.GetUnitDataById(session.selectedUnitId);
        if (unit == null || session.Map == null) return;

        var alliedCells = new HashSet<Vector3Int>();
        var enemyCells = new HashSet<Vector3Int>();

        foreach (UnitData u in session.units)
        {
            if (session.IsMyUnit(u.Id))
                alliedCells.Add(u.CurrentCell);
            else
                enemyCells.Add(u.CurrentCell);
        }


        var range = GridPathfinder.FloodFill(
            session.Map,
            unit.CurrentCell,
            unit.MoveRange,
            GridPathfinder.FloodFillTarget.EmptyCell,
            true,
            alliedCells,
            enemyCells);
        scene.SetRangeData(range);

        foreach (var cell in scene.rangeTilesData)
            scene.rangeTilemap.SetTile(cell, tile);
    }

    public void ClearRange()
    {
        if (scene.rangeTilemap != null)
            scene.rangeTilemap.ClearAllTiles();
        scene.ClearRangeData();
    }

    // -------------------------------------------------------
    // Hover Shadow
    // -------------------------------------------------------
    private List<GameObject> activeShadows = new List<GameObject>();

    //special complex case: hover shadow depends heavily on InteractionState
    public void HoverShadow()
    {
        Vector3Int cell = session.currentCellMouseOn;
        UnitData unit = session.GetUnitDataById(session.selectedUnitId);
        if (unit == null || scene.movableTilemap == null) return;
        if (cell == unit.CurrentCell) { ClearShadowBrute(); return; }

        GameObject shadow = scene.GetSceneUnitById(session.selectedUnitId).hoverUnit;
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

    // UI element display
    public void ShowWaitButton(bool isShown)
    {
        bool isMyTurn = session.IsMyTurn();
        waitButtonLayer.SetActive(isMyTurn && isShown);
    }
    public void ShowCancelButton(bool isShown)
    {
        bool isMyTurn = session.IsMyTurn();
        cancelButtonLayer.SetActive(isMyTurn && isShown);
    }
    public void ShowConfirmButton(bool isShown)
    {
        bool isMyTurn = session.IsMyTurn();
        confirmButton.SetActive(isMyTurn && isShown);
    }


    // -------------------------------------------------------
    // UI Loop : Keep this rule so future scale will clean: These UI loops only updates - pass data from session to target UI elements, it does not control the isActive or anyfunction inside that may affect the timing of visibility. 
    // ONLY DATA to the ONLY TARGET (or the data defines the target) In all case, the DATA PASSING MUST BE SIMPLE enough to be observed and has expected behaviour.
    // -------------------------------------------------------

    public void StartUILoop() => StartCoroutine(UILoop());

    bool turnFlip = false;

    IEnumerator UILoop()
    {
        while (true)
        {
            if (waitCounterText != null)
                waitCounterText.text = $"Time: {session.waitDur} | OT: {session.overtimeDur}";

            bool isMyTurn = session.IsMyTurn();

            if (instantCounterText != null)
                instantCounterText.text = $"{session.Timeline.currentInstant}/{session.Timeline.maxInstant}";

            if (!session.Timeline.isPaused)
            {
                if (instantStatusText != null)
                    instantStatusText.text = "Time is flowing...";
            }
            else
            {
                if (instantStatusText != null)
                    instantStatusText.text = (session.LastResolve.HasResolve && session.SyncState == 0) ? "Resolving...." : (isMyTurn ? "Your turn to make decision..." : "Enemy deciding...");
            }

            if (turnFlip != isMyTurn)
            {
                UpdateVisualOnStateChange();
                turnFlip = isMyTurn;
            }
            yield return new WaitForSeconds(0.1f);
        }
    }

}