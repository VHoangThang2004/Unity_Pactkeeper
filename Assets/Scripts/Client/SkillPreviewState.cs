using UnityEngine;

public class SkillPreviewState : IInteractionState
{
    private readonly ClientMatchSession session;
    private readonly ClientScene scene;
    private readonly InteractionStateMachine sm;
    public SkillPreviewState(ClientMatchSession session, ClientScene scene, InteractionStateMachine sm)
    {
        this.session = session;
        this.scene = scene;
        this.sm = sm;
    }

    public void OnEnter(int? unitId = null, Vector3Int? targetTile = null, int? skillId = null)
    {
        // skillCardId set via GoToSkillPreview(skillCardId)
        session.currentPreviewCell = session.GetUnitDataById(session.selectedUnitId).CurrentCell; // mark as not decided a preview-targeted cell to cast Skill 

        scene.visualController.UpdateVisualOnStateChange();
    }

    public void OnTileClick(Vector3Int cell)
    {
        // Clicked cell is the FinalTargetCell — send decision


        ActionDefinition skill = scene.actionLibrary.Get(session.currentSkillId);
        if (skill == null) { sm.GoToNone(); return; }

        if (session.IsMyUnit(session.selectedUnitId) && scene.IsInRange(cell))
        {
            // if Is in range means valid target (range is calculated in grid path finder, if any bug happens related to skill targetting then go there)
            session.currentPreviewCell = cell;
            scene.visualController.UpdateVisualOnStateChange();
            return;
        }

        //if no correct case, count as cancel
        sm.GoToUnitSelected(session.selectedUnitId);
    }
    public void OnTileHover(Vector3Int cell) { }
    public void OnExit() { session.currentSkillId = -1; }



    public void OnDecision()
    {
        int unitId = session.selectedUnitId;
        int skillCardId = session.currentSkillId;
        if (unitId == -1) return;

        var actionDef = scene.actionLibrary.Get(skillCardId);
        if (actionDef == null)
        {
            Debug.LogError($"[ActionMenuUI] No ActionDefinition for skillCardId {skillCardId}!");
            return;
        }

        scene.bridge.SendDecisionServerRpc(
            unitId,
            session.currentPreviewCell,
            DecisionType.ActivateAction,
            skillCardId,
            session.CurrentToken);

        //states ends here, waits till new snapshot arrive and gets to none state (forceNoneState) from other independant processes.
    }


    public void Cancel()
    {
        sm.GoToUnitSelected(session.selectedUnitId);
    }
}