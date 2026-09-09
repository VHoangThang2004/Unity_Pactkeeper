using UnityEngine;
using UnityEngine.Tilemaps;

public class ClientVisualController : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private ClientController controller;

    // Wired at runtime by ClientMapLoader
    private Tilemap tilemap;
    private Tilemap rangeTilemap;

    public void SetMapRefs(Tilemap movable, Tilemap range)
    {
        tilemap      = movable;
        rangeTilemap = range;
    }

    // -------------------------------------------------------
    // Range Display
    // -------------------------------------------------------

    /// <summary>
    /// Computes rangeTilesData from GridMap, then paints rangeTilemap from it.
    /// rangeTilemap is display only — never read for logic.
    /// </summary>
    public void ShowRange(bool isOwned)
    {
        TileBase tileToUse = isOwned ? controller.rangeTileBase : controller.enemyRangeTileBase;
        ClearRange();

        // Compute pure data range
        controller.ComputeRangeData(controller.selectedUnit);

        // Paint tilemap from data
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
}