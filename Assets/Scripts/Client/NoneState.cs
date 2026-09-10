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

    public void OnEnter(int? unitId = null, Vector3Int? targetTile = null, int? skillId = null)
    {
        session.selectedUnitId = -1;
        session.currentSkillId = -1;

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

    public void OnTileHover(Vector3Int cell)
    {
    }
    public void OnDecision()
    {
        Debug.Log("What are you trying to decide at none state? You're not selecting a unit, not selecting a tile, nothing here for you to take an action");
        return;
    }
    public void Cancel()
    {
        Debug.LogError("WHY? We're in none state already, why, how can you still press cancel?");
    }

    public void ApplyWait()
    {
        scene.bridge.SendDecisionServerRpc(-1, default, DecisionType.Wait, -1, session.CurrentToken);
    }
}