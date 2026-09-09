using UnityEngine;

public class NoneState : IInteractionState
{
    private readonly ClientController controller;
    private readonly InteractionStateMachine sm;

    public NoneState(ClientController controller, InteractionStateMachine sm)
    {
        this.controller = controller;
        this.sm = sm;
    }

    public void OnEnter(ClientUnit unit = null, Vector3Int? targetTile = null)
    {
        // Full reset
        controller.visualController.ClearRange();
        controller.ClearSelectedUnit();
        controller.visualController.ClearShadowBrute();
        controller.ActionMenu.Hide();

        Debug.Log("[State] → None");
    }

    public void OnExit() { }

    public void OnTileClick(Vector3Int cell)
    {
        var unit = controller.GetClientUnitAt(cell);
        if (unit != null)
            sm.GoToUnitSelected(unit);
    }

    public void OnTileHover(Vector3Int cell) { }
}