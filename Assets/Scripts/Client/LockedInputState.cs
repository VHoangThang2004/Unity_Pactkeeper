using System.Collections;
using UnityEngine;

using System.Collections.Generic;
using Unity.Netcode;


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

    public void OnEnter(int? unitId = null, Vector3Int? targetTile = null, int? skillId = null)
    {
        //clears all UI related to input elements
        session.selectedUnitId = -1;
        session.currentSkillId = -1;

        scene.visualController.UpdateVisualOnStateChange();
        scene.clientInteractionSystem.PromiseUnlock();
    }


    public void OnExit() { }

    public void OnTileClick(Vector3Int cell)
    { }

    public void OnTileHover(Vector3Int cell) { }
    public void OnDecision()
    {
        return;
    }
    public void Cancel()
    {
        Debug.LogError("WHY? We're in locked state already, why, how can you still press cancel?");
    }

}