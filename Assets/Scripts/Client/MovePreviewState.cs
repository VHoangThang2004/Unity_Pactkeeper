using UnityEngine;

public class MovePreviewState : IInteractionState
{
    private readonly ClientMatchSession session;
    private readonly ClientScene scene;
    private readonly InteractionStateMachine sm;

    public MovePreviewState(ClientMatchSession session, ClientScene scene, InteractionStateMachine sm)
    {
        this.session = session;
        this.scene = scene;
        this.sm = sm;
    }

    public void OnEnter(int? unitId = null, Vector3Int? targetTile = null, int? skillId = null)
    {
        if (targetTile.HasValue)
            session.currentPreviewCell = targetTile.Value;

        scene.visualController.UpdateVisualOnStateChange();
        Debug.Log($"[State] -> MovePreview (unit {session.selectedUnitId} at preview {session.currentPreviewCell})");
    }

    public void OnExit()
    {
        scene.visualController.ClearShadowBrute();
        if (session.selectedUnitId != -1)
            session.currentPreviewCell = session.GetUnitDataById(session.selectedUnitId).CurrentCell;
    }

    public void OnTileClick(Vector3Int targetCell)
    {
        UnitData selectedUnit = session.GetUnitDataById(session.selectedUnitId);
        if (selectedUnit == null) { sm.GoToNone(); return; }

        if (targetCell == selectedUnit.CurrentCell) { sm.GoToUnitSelected(session.selectedUnitId); return; }

        var unitAtCell = scene.GetSceneUnitAt(targetCell);
        if (unitAtCell != null) { sm.GoToUnitSelected(session.GetUnitDataAt(targetCell).Id); return; }

        if (scene.IsInRange(targetCell))
        {
            sm.GoToMovePreview(targetCell);
            return;
        } //keep state, doesnt change state, keep it simple

        sm.GoToNone();
    }

    public void OnTileHover(Vector3Int cell) { }

    // -------------------------------------------------------
    // Actions (called by UI buttons through interactInteractionStateMachine)
    // -------------------------------------------------------
    public void OnDecision()
    {
        UnitData unit = session.GetUnitDataById(session.selectedUnitId);
        if (unit == null) return;


        scene.bridge.SendDecisionServerRpc(
            unit.Id,
            session.currentPreviewCell,
            DecisionType.ActivateAction,
            1,
            session.CurrentToken); // move to cell -> skill id = 1
    }

    public void Cancel()
    {
        sm.GoToUnitSelected(session.selectedUnitId);
    }
}