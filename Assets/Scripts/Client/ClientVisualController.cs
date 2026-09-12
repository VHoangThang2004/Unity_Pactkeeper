using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.UI;

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
    [SerializeField] private TextMeshProUGUI timerBarText;
    [SerializeField] private Image timerBar;
    [SerializeField] public GameObject waitButtonLayer;
    [SerializeField] public GameObject cancelButtonLayer;
    // [SerializeField] public GameObject confirmButton;
    [SerializeField] private CanvasGroup confirmButtonCanvasGroup;

    [SerializeField] public GameObject ActionMenuUI;
    [SerializeField] public TextMeshProUGUI CurrentUnitInfo;



    // -------------------------------------------------------
    // State Change — controls visibility of UI elements
    // -------------------------------------------------------

    public void UpdateVisualOnStateChange()
    {
        var state = scene?.clientInteractionSystem?.stateMachine?.currentState;
        ClearRange();
        ClearShadowBrute();

        if (state is UnitSelectedState)
        {
            ShowWaitButton(false);
            ShowCancelButton(true);
            ShowActionMenu(true);

            if (session.isTargetLocked)
            {
                Vector3Int x = session.CurrentTargetableCells.Find(cell => cell.x == session.currentPreviewCell.x && cell.y == session.currentPreviewCell.y);
                ShowConfirmButton(x.z == 1 && session.isUnitReady(session.selectedUnitId));
            }
        }
        else if (state is NoneState)
        {
            ShowWaitButton(true);
            ShowCancelButton(false);
            ShowConfirmButton(false);
            ShowActionMenu(false);
            ActionMenuUI.SetActive(false);
        }
        else if (state is LockedInputState)
        {
            ShowWaitButton(false);
            ShowCancelButton(false);
            ShowConfirmButton(false);
            ShowActionMenu(false);
            ActionMenuUI.SetActive(false);
        }
    }

    // -------------------------------------------------------
    // Range Display — dumb redraw from session data
    // -------------------------------------------------------

    private void RedrawRange()
    {
        bool isOwned = session.IsMyUnit(session.selectedUnitId);
        ClearRange();
        if (scene.rangeTilemap == null) return;

        var newTarget = new HashSet<Vector3Int>(session.CurrentTargetableCells);

        scene.rangeTilemap.ClearAllTiles();

        foreach (var cell in newTarget)
        {
            TileBase tile = (isOwned && cell.z == 1) ? rangeTileBase : enemyRangeTileBase;
            scene.rangeTilemap.SetTile(cell, tile);
            Debug.Log($"cell[{cell}] is set {(isOwned && cell.z == 1)}");
        }
    }
    public void ClearRange()
    {
        if (scene.rangeTilemap != null)
            scene.rangeTilemap.ClearAllTiles();
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

        cell.z = 1;

        // Debug.Log($"Current previewing cell: {cell}");

        if (!session.CurrentTargetableCells.Contains(cell))
        {
            ClearShadowBrute();
            return;
        }
        // Debug.Log($"Cell is in CurrentTagetable cells");

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
    public void ShowActionMenu(bool isShown)
    {
        if (isShown)
        {
            ActionMenuUI.SetActive(true);
            UnitData unit = session.GetUnitDataById(session.selectedUnitId);
            SkillDefinition skill = scene.skillLibrary.Get(unit.WeaponSkillId);
            if (skill != null && scene.mainWeaponSkillIcon != null)
                scene.mainWeaponSkillIcon.sprite = skill.icon;
            UnitDefinition unitDefinition = scene.unitLibrary.Get(unit.UId);
            CurrentUnitInfo.text = "Name: "+unitDefinition.name.ToString()
            + "\n HP: " + unit.CurrentHP.ToString()
            + "\n SP: " + unit.CurrentSkillPoint.ToString()
            + "\n Speed: " + unit.Speed.ToString();
        }
        else
        {
            ActionMenuUI.SetActive(false);
        }
    }

    public void ShowConfirmButton(bool isShown)
    {
        bool show = session.IsMyTurn() && isShown;
        confirmButtonCanvasGroup.alpha = show ? 1f : 0f;
        confirmButtonCanvasGroup.interactable = show;
        confirmButtonCanvasGroup.blocksRaycasts = show;
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
            if (timerBarText != null)
                timerBarText.text = session.waitDur > 0 ? "Waiting..." : "Overtime...";
            if (timerBar != null)
            {
                float waitDur = session.waitDur > 0 ? session.waitDur : session.overtimeDur;
                float maxDur = session.waitDur > 0 ? session.maxWaitDur : session.maxOvertimeDur;
                timerBar.color = session.waitDur > 0 ? Color.green : Color.red;
                timerBar.fillAmount = maxDur > 0 ? waitDur / maxDur : 0f;
            }

            if (instantCounterText != null)
                instantCounterText.text = $"{session.Timeline.currentInstant}/{session.Timeline.maxInstant}";

            // Status text
            if (instantStatusText != null)
            {
                if (!session.Timeline.isPaused)
                    instantStatusText.text = "Time is flowing";
                else if (session.LastResolve.HasResolve && session.SyncState != 0)
                    instantStatusText.text = "Resolving";
                else
                    instantStatusText.text = session.IsMyTurn() ? "Your turn" : "Enemy turn";
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
                // Update confirm button based on target lock
                RedrawRange();
                if (session.isTargetLocked)
                {
                    Vector3Int x = session.CurrentTargetableCells.Find(cell => cell.x == session.currentPreviewCell.x && cell.y == session.currentPreviewCell.y);
                    ShowConfirmButton(x.z == 1 && session.isUnitReady(session.selectedUnitId));
                }
                else
                {
                    ShowConfirmButton(false);
                }

            }

            yield return new WaitForSeconds(0.1f);
        }
    }
}