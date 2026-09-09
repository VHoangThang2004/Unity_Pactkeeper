using UnityEngine;

public class MovePreviewState : IInteractionState
{
    private readonly ClientController controller;
    private readonly InteractionStateMachine sm;

    private ClientUnit selectedUnit;
    private Vector3Int targetTile;

    public MovePreviewState(ClientController controller, InteractionStateMachine sm)
    {
        this.controller = controller;
        this.sm = sm;
    }

    public void OnEnter(ClientUnit unit = null, Vector3Int? targetTile = null)
    {
        selectedUnit = controller.selectedUnit;
        this.targetTile = targetTile ?? selectedUnit.data.CurrentCell;

        controller.visualController.HoverShadow(controller.tilemap, this.targetTile);

        Debug.Log($"[State] -> MovePreview (unit {selectedUnit.data.Id} -> {this.targetTile})");
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
        if (controller.IsInRange(cell))
        {
            sm.GoToMovePreview(cell);
            return;
        }

        sm.GoToNone();
    }

    public void OnTileHover(Vector3Int cell) { }

    // -------------------------------------------------------
    // Actions (called by UI buttons)
    // -------------------------------------------------------

    public void StandBy()
    {
        controller.TryMoveAndStandBy(selectedUnit.data.Id, targetTile);
        sm.GoToNone();
    }

    public void UseSkill(int skillId)
    {
        controller.TryMoveAndSkill(selectedUnit.data.Id, targetTile, skillId);
        sm.GoToNone();
    }

    public void Cancel()
    {
        sm.GoToUnitSelected(selectedUnit);
    }
}