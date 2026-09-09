using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Tilemaps;

public class ClientVisualController : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private ClientController controller;

    [Header("UI Elements")]
    [SerializeField] public TextMeshProUGUI instantStatusText;
    [SerializeField] public TextMeshProUGUI instantCounterText;
    [SerializeField] public TextMeshProUGUI waitCounterText;

    // Wired at runtime by ClientMapLoader
    private Tilemap tilemap;
    private Tilemap rangeTilemap;

    public void SetMapRefs(Tilemap movable, Tilemap range)
    {
        tilemap = movable;
        rangeTilemap = range;
    }

    // -------------------------------------------------------
    // Range Display
    // -------------------------------------------------------

    public void ShowRange(bool isOwned)
    {
        TileBase tileToUse = isOwned ? controller.rangeTileBase : controller.enemyRangeTileBase;
        ClearRange();
        controller.ComputeRangeData(controller.selectedUnit);
        foreach (var cell in controller.rangeTilesData)
            rangeTilemap.SetTile(cell, tileToUse);
    }

    public void ClearRange()
    {
        rangeTilemap.ClearAllTiles();
        controller.ClearRangeData();
    }

    // -------------------------------------------------------
    // Hover Shadow
    // -------------------------------------------------------

    public void HoverShadow(Tilemap tilemap, Vector3Int cell)
    {
        UnitData data = controller.selectedUnit.data;
        ClientUnit clientUnit = controller.selectedUnit;
        if (cell == data.CurrentCell)
        {
            ClearShadow();
            return;
        }
        clientUnit.hoverUnit.SetActive(true);
        clientUnit.hoverUnit.transform.position = tilemap.GetCellCenterWorld(cell);
    }

    public void ClearShadow()
    {
        if (controller.selectedUnit != null)
            controller.selectedUnit.hoverUnit.SetActive(false);
    }

    public void ClearShadow(ClientUnit unit)
    {
        if (unit != null)
            unit.hoverUnit.SetActive(false);
    }

    public void ClearShadowBrute()
    {
        foreach (var unit in controller.GetAllUnits())
            unit.hoverUnit.SetActive(false);
    }

    // -------------------------------------------------------
    // UI
    // -------------------------------------------------------
    public void ClearInstantStatus()
    {
        if (instantStatusText == null) return;
        instantStatusText.text = "Time is flowing...";
    }



    bool uiLoopEnded = false;
    public void StartUILoop() => StartCoroutine(UILoop());
    public void EndUILoop() => uiLoopEnded = true;
    IEnumerator UILoop()
    {
        int c = 0;
        uiLoopEnded = false;
        controller.ActWaitDecisionCanvas.SetActive(false);
        while (!uiLoopEnded)
        {
            if (controller.clientSession.Timeline.isPaused)
            {
                waitCounterText.gameObject.SetActive(true);
                if (c >= 10)
                {
                    controller.clientSession.CountDownDurations();
                    c = 0;
                }
                DecisionRequestData currentDecision = controller.clientSession.LastDecisionRequest;
                waitCounterText.text = $"Timeleft : {controller.clientSession.waitDur} | Overtime : {controller.clientSession.overtimeDur}";
                if (controller.clientSession.IsMyTurn())
                {
                    controller.ActWaitDecisionCanvas.SetActive(currentDecision.ActWaitDuration > 0);
                    instantStatusText.text = currentDecision.ActionDuration > 0 ? "Act or Wait?" : "Waiting for action...";
                }
                else
                {
                    controller.ActWaitDecisionCanvas.SetActive(false);
                    instantStatusText.text = currentDecision.ActionDuration > 0 ? "Enemy deciding..." : "Enemy deciding on action....";
                }

            }
            else
            {
                instantStatusText.text = "Time is flowing...";
                waitCounterText.gameObject.SetActive(false);
                controller.ActWaitDecisionCanvas.SetActive(false);
            }
            instantCounterText.text = $"{controller.clientSession.Timeline.currentInstant}/150";
            c++;
            yield return new WaitForSeconds(0.1f);
        }
    }

    public void DecidedActWait()
    {
        controller.ActWaitDecisionCanvas.SetActive(false);
    }

}