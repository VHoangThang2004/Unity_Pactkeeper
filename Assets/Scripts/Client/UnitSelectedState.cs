using UnityEngine;

public class UnitSelectedState : IInteractionState
{
    private readonly ClientController controller;
    private readonly InteractionStateMachine sm;

    private ClientUnit selectedUnit;
    private bool isOwned;

    public UnitSelectedState(ClientController controller, InteractionStateMachine sm)
    {
        this.controller = controller;
        this.sm = sm;
    }

    public void OnEnter(ClientUnit unit = null, Vector3Int? targetTile = null)
    {
        controller.visualController.ClearShadowBrute();

        selectedUnit = unit;
        isOwned = controller.clientSession.IsUnitUnderPermission(unit.data.Id);

        controller.SetSelectedUnit(selectedUnit);
        controller.SetIsOwnedUnit(isOwned);

        // ShowRange computes rangeTilesData and paints rangeTilemap
        controller.visualController.ShowRange(isOwned);

        controller.ActionMenu.ShowForUnit(selectedUnit, isOwned);

        Debug.Log($"[State] -> UnitSelected (unit {unit.data.Id}, owned={isOwned})");
    }

    public void OnExit() { }

    public void OnTileClick(Vector3Int cell)
    {
        if (cell == selectedUnit.data.CurrentCell)
        {
            sm.GoToNone();
            return;
        }

        var unitAtCell = controller.GetClientUnitAt(cell);
        if (unitAtCell != null)
        {
            sm.GoToUnitSelected(unitAtCell);
            return;
        }

        // Use rangeTilesData — not rangeTilemap
        if (isOwned && controller.IsInRange(cell))
        {
            sm.GoToMovePreview(cell);
            return;
        }

        sm.GoToNone();
    }

    public void OnTileHover(Vector3Int cell)
    {
        if (!isOwned) return;

        // Use rangeTilesData — not rangeTilemap
        if (controller.IsInRange(cell))
            controller.visualController.HoverShadow(controller.tilemap, cell);
        else
            controller.visualController.ClearShadow();
    }

    public void UseSkill(int skillId)
    {
        controller.TryMoveAndSkill(selectedUnit.data.Id, selectedUnit.data.CurrentCell, skillId);
        sm.GoToNone();
    }

    public void Cancel()
    {
        sm.GoToUnitSelected(selectedUnit);
    }
}