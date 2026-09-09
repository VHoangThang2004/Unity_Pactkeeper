using UnityEngine;

public class InteractionStateMachine
{
    private IInteractionState currentState;

    private readonly NoneState noneState;
    private readonly UnitSelectedState unitSelectedState;
    private readonly MovePreviewState movePreviewState;

    public InteractionStateMachine(ClientController controller)
    {
        noneState = new NoneState(controller, this);
        unitSelectedState = new UnitSelectedState(controller, this);
        movePreviewState = new MovePreviewState(controller, this);
    }

    public void Start()
    {
        TransitionTo(noneState);
    }

    public void OnTileClick(Vector3Int cell)
    {
        currentState?.OnTileClick(cell);
    }

    public void OnTileHover(Vector3Int cell)
    {
        currentState?.OnTileHover(cell);
    }

    // -------------------------------------------------------
    // Transitions
    // -------------------------------------------------------

    public void GoToNone()
    {
        TransitionTo(noneState);
    }

    public void GoToUnitSelected(ClientUnit unit)
    {
        TransitionTo(unitSelectedState, unit: unit);
    }

    public void GoToMovePreview(Vector3Int targetTile)
    {
        TransitionTo(movePreviewState, targetTile: targetTile);
    }

    private void TransitionTo(IInteractionState next, ClientUnit unit = null, Vector3Int? targetTile = null)
    {
        currentState?.OnExit();
        currentState = next;
        currentState.OnEnter(unit, targetTile);
    }

    //-------------------------------------------------------
    //exposure
    //-------------------------------------------------------
    public void OnActionStandBy() => (currentState as MovePreviewState)?.StandBy();
    // public void OnActionWait() => (currentState as ??)?.Wait();
    public void OnActionSkill(int skillId) => (currentState as MovePreviewState)?.UseSkill(skillId);
    public void OnActionCancel() => (currentState as MovePreviewState)?.Cancel();
}