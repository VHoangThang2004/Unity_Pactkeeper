using UnityEngine;

public class InteractionStateMachine
{
    public IInteractionState currentState { get; private set; }
    private ClientScene scene;
    private ClientMatchSession session;

    private readonly LockedInputState lockedInputState;
    private readonly NoneState noneState;
    private readonly UnitSelectedState unitSelectedState;

    public InteractionStateMachine(ClientMatchSession session, ClientScene scene)
    {
        this.scene = scene;
        this.session = session;
        lockedInputState = new LockedInputState(session, scene, this);
        noneState = new NoneState(session, scene, this);
        unitSelectedState = new UnitSelectedState(session, scene, this);
    }

    public void Init()
    {
        TransitionTo(lockedInputState);
    }

    public void OnTileClick(Vector3Int cell) => currentState?.OnTileClick(cell);
    public void OnTileHover(Vector3Int cell) => currentState?.OnTileHover(cell);

    // -------------------------------------------------------
    // Transitions
    // -------------------------------------------------------

    public void GoToLocked() => TransitionTo(lockedInputState);
    public void GoToNone() => TransitionTo(noneState);
    public void GoToUnitSelected(int unitId) => TransitionTo(unitSelectedState, unitId: unitId);

    private void TransitionTo(IInteractionState next, int? unitId = null, int? skillId = null)
    {
        if (session.SyncState != 0)
        {
            if (currentState is not LockedInputState)
            {
                currentState?.OnExit();
                currentState = lockedInputState;
                currentState.OnEnter();
                scene.visualController.UpdateVisualOnStateChange();
            }
            return;
        }
        currentState?.OnExit();
        currentState = next;
        currentState.OnEnter(unitId, skillId: skillId);
        scene.visualController.UpdateVisualOnStateChange();
    }

    // -------------------------------------------------------
    // Exposure
    // -------------------------------------------------------

    public void OnDecisionSelectSkill(int skillId) => (currentState as UnitSelectedState)?.OnDecision(skillId);
    public void OnDecision() => currentState?.OnDecision();
    public void OnWait() => (currentState as NoneState)?.ApplyWait();
    public void OnCancel() => currentState?.Cancel();
}