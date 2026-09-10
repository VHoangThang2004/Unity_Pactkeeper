using UnityEngine;

public class InteractionStateMachine
{
    public IInteractionState currentState { get; private set; }
    private ClientScene scene;
    private ClientMatchSession session;

    private readonly LockedInputState lockedInputState;
    private readonly NoneState noneState;
    private readonly UnitSelectedState unitSelectedState;
    private readonly MovePreviewState movePreviewState;
    private readonly SkillPreviewState skillPreviewState;

    public InteractionStateMachine(ClientMatchSession session, ClientScene scene)
    {
        this.scene = scene;
        this.session = session;
        lockedInputState = new LockedInputState(session, scene, this);
        noneState = new NoneState(session, scene, this);
        unitSelectedState = new UnitSelectedState(session, scene, this);
        movePreviewState = new MovePreviewState(session, scene, this);
        skillPreviewState = new SkillPreviewState(session, scene, this);
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
    public void GoToMovePreview(Vector3Int tile) => TransitionTo(movePreviewState, targetTile: tile);
    public void GoToSkillPreview(int skillId) => TransitionTo(skillPreviewState, skillId: skillId);

    private void TransitionTo(IInteractionState next, int? unitId = null, Vector3Int? targetTile = null, int? skillId = null)
    {

        if (session.SyncState != 0)
        { // NEVER allow state change while syncing is not completed.
            if (currentState is not LockedInputState)
            {
                currentState?.OnExit();
                currentState = lockedInputState;
                currentState.OnEnter();
                scene.visualController.UpdateVisualOnStateChange(); // reset on state change
            }
            return;
        }
        currentState?.OnExit();
        currentState = next;
        currentState.OnEnter(unitId, targetTile, skillId);
        scene.visualController.UpdateVisualOnStateChange(); // reset on state change

    }

    // -------------------------------------------------------
    // Exposure
    // -------------------------------------------------------

    public void OnDecisionSelectSkill(int skillId) => (currentState as UnitSelectedState)?.OnDecision(skillId); // unitselected state -> skill preview state by selecting skill Id
    public void OnDecision() => currentState?.OnDecision();
    public void OnWait() => (currentState as NoneState)?.ApplyWait();
    public void OnCancel() => currentState?.Cancel();

}