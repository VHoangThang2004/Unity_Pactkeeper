using UnityEngine;

public class NoneState : IInteractionState
{
    private readonly ClientMatchSession session;
    private readonly ClientScene scene;
    private readonly InteractionStateMachine sm;

    public NoneState(ClientMatchSession session, ClientScene scene, InteractionStateMachine sm)
    {
        this.session = session;
        this.scene = scene;
        this.sm = sm;
    }

    public void OnEnter(int? unitId = null, int? skillId = null)
    {
        session.selectedUnitId = -1;
        session.currentSkillId = -1;
        session.currentPreviewCell = default;
        session.ClearPatternData();

        scene.visualController.UpdateVisualOnStateChange();
        Debug.Log("[State] → None");
    }

    public void OnExit() { }

    public void OnTileClick(Vector3Int cell)
    {
        var unit = scene.GetSceneUnitAt(cell);
        if (unit != null)
            sm.GoToUnitSelected(unit.unitId);
    }

    public void OnTileHover(Vector3Int cell) { }

    public void OnDecision()
    {
        Debug.LogWarning("[NoneState] OnDecision called — nothing to decide.");
    }

    public void Cancel()
    {
        Debug.LogWarning("[NoneState] Cancel called — already in none state.");
    }

    public void ApplyWait()
    {
        scene.bridge.SendDecisionServerRpc(-1, default, DecisionType.Wait, -1, session.CurrentToken);
    }
}