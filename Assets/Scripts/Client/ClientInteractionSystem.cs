
using UnityEngine;
using UnityEngine.Tilemaps;
using Unity.Netcode;

public class ClientInteractionSystem : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Tilemap tilemap;
    [SerializeField] private ClientController controller;

    // -------------------------------------------------------
    // Hover
    // -------------------------------------------------------

    public void HandleTileHover()
    {
        Vector3 worldPos = controller.cam.ScreenToWorldPoint(controller.input.MouseScreenPosition);
        worldPos.z = 0;

        controller.currentCell = tilemap.WorldToCell(worldPos);

        if (!tilemap.HasTile(controller.currentCell))
        {
            if (controller.hoverHighlight.activeSelf)
                controller.hoverHighlight.SetActive(false);
            controller.isOnCell = false;
            return;
        }

        controller.isOnCell = true;
        Vector3 center = tilemap.GetCellCenterWorld(controller.currentCell);

        if (!controller.hoverHighlight.activeSelf)
            controller.hoverHighlight.SetActive(true);
        controller.hoverHighlight.transform.position = center + controller.offset;

        // Hover preview on selected unit
        if (controller.selectedUnit != null && controller.IsOwnedUnit)
        {
            if (controller.rangeTilemap.HasTile(controller.currentCell))
                controller.selectedUnit.HoverPosition(tilemap, controller.currentCell);
            else
                controller.selectedUnit.HoverCancel();
        }
    }

    // -------------------------------------------------------
    // Click
    // -------------------------------------------------------

    public void HandleTileClick(Vector3Int cell)
    {
        if (controller.selectedUnit == null)
        {
            // Try to select a unit at this cell
            ClientUnit unit = controller.GetClientUnitAt(cell);
            if (unit != null)
            {
                Debug.Log($"[Interaction] Clicked on unit {unit.data.Id} at {cell}");
                SelectUnit(unit);
            }
            else
            {
                controller.TryTestCell(cell);
                Debug.Log($"[Interaction] No unit at {cell}");
                return;
            }
            return;
        }
        else
        {
            // Unit already selected
            if (controller.rangeTilemap.HasTile(cell))
            {
                // Move to target cell
                controller.TryMoveUnit(controller.selectedUnit.data.Id, cell);
                DeselectUnit();
            }
            else
            {
                // Clicked outside range — check if clicking another unit
                ClientUnit unit = controller.GetClientUnitAt(cell);
                if (unit != null)
                {
                    SelectUnit(unit);
                }
                else
                    DeselectUnit();
            }
            return;
        }
    }

    // -------------------------------------------------------
    // Helpers
    // -------------------------------------------------------

    private bool IsOwnedByLocalPlayer(int unitId)
    {
        return controller.clientSession.IsUnitUnderPermission(unitId);
    }

    void SelectUnit(ClientUnit unit)
    {
        if (controller.selectedUnit == unit)
        {
            DeselectUnit();
            return;
        }

        controller.SetSelectedUnit(unit);
        controller.IsOwnedUnit = IsOwnedByLocalPlayer(unit.data.Id);
        controller.selectedUnit.CalculateRange(tilemap);
        ShowRange();

        Debug.Log($"[Interaction] Player {NetworkManager.Singleton.LocalClientId} selected unit {unit.data.Id}");
    }

    void DeselectUnit()
    {
        ClearRange();
        controller.selectedUnit.HoverCancel();
        controller.ClearSelectedUnit();

        Debug.Log($"[Interaction] Player {NetworkManager.Singleton.LocalClientId} deselected unit {controller.selectedUnit?.data.Id ?? -1}");
    }


    // -------------------------------------------------------
    // Range Display
    // -------------------------------------------------------

    public void ShowRange()
    {
        TileBase tileToUse = controller.IsOwnedUnit ? controller.rangeTileBase : controller.enemyRangeTileBase;
        ClearRange();
        controller.selectedUnit.CalculateRange(tilemap);
        foreach (var cell in controller.selectedUnit.reachableCells)
            controller.rangeTilemap.SetTile(cell, tileToUse);
    }

    public void ClearRange()
    {
        controller.rangeTilemap.ClearAllTiles();
    }
}