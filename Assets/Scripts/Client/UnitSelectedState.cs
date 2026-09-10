using UnityEngine;

public class UnitSelectedState : IInteractionState
{
    private readonly ClientMatchSession session;
    private readonly ClientScene scene;
    private readonly InteractionStateMachine sm;

    public UnitSelectedState(ClientMatchSession session, ClientScene scene, InteractionStateMachine sm)
    {
        this.session = session;
        this.scene = scene;
        this.sm = sm;

    }

    private int translator(int? num)
    {
        if (num == null) return -1;
        return (int)num;
    }

    public void OnEnter(int? unitId = null, Vector3Int? targetTile = null, int? skillId = null)
    {
        session.selectedUnitId = translator(unitId);

        scene.visualController.UpdateVisualOnStateChange();
        Debug.Log($"[State] -> UnitSelected (unit {unitId}, owned={session.IsMyUnit(session.selectedUnitId)})");
    }

    public void OnExit() {}

    public void OnTileClick(Vector3Int cell)
    {
        if (cell == session.GetUnitDataById(session.selectedUnitId).CurrentCell) { sm.GoToNone(); return; }

        UnitData unitAtCell = session.GetUnitDataAt(cell);
        if (unitAtCell != null) { sm.GoToUnitSelected(unitAtCell.Id); return; }

        if (session.IsMyUnit(session.selectedUnitId) && scene.IsInRange(cell)) { sm.GoToMovePreview(cell); return; }

        sm.GoToNone();
    }

    public void OnTileHover(Vector3Int cell)
    {
        if (!session.IsMyUnit(session.selectedUnitId)) return;

        if (scene.IsInRange(cell))
            scene.visualController.HoverShadow();
        else
            scene.visualController.ClearShadowBrute();
    }

    // -------------------------------------------------------
    // Actions (called by UI buttons through interactInteractionStateMachine)
    // -------------------------------------------------------
    public void OnDecision(int skillCardId)
    {
        session.currentSkillId = skillCardId;
        sm.GoToSkillPreview(skillCardId);
    }
    public void OnDecision()
    {
        // not used, just be here so doesnt appears as syntax error.
        Debug.LogError("[SM- UnitSelectedState] Place should not be reached is reached.");
    }

    public void Cancel()
    {
        sm.GoToNone();
    }
}