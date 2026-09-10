using UnityEngine;

public class LockedInputState : IInteractionState
{
    private readonly ClientMatchSession session;
    private readonly ClientScene scene;
    private readonly InteractionStateMachine sm;

    public LockedInputState(ClientMatchSession session, ClientScene scene, InteractionStateMachine sm)
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
        scene.clientInteractionSystem.PromiseUnlock();
    }

    public void OnExit() { }
    public void OnTileClick(Vector3Int cell) { }
    public void OnTileHover(Vector3Int cell) { }
    public void OnDecision() { }
    public void Cancel()
    {
        Debug.LogWarning("[LockedInputState] Cancel called — input is locked.");
    }
}