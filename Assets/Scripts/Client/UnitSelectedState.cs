using UnityEngine;
using System.Collections.Generic;

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

    public void OnEnter(int? unitId = null, int? skillId = null)
    {
        session.selectedUnitId = unitId ?? -1;
        session.currentPreviewCell = session.GetUnitDataById(session.selectedUnitId)?.CurrentCell ?? default;

        // Default to movement skill
        UnitData unit = session.GetUnitDataById(session.selectedUnitId);
        session.currentSkillId = unit?.MovementSkillId ?? -1;

        CalculateTargetPattern();
        session.CurrentAoECells.Clear();

        Debug.Log($"[State] → UnitSelected (unit {unitId} owned={session.IsMyUnit(session.selectedUnitId)})");
    }

    public void OnExit()
    {
        session.currentSkillId = -1;
        session.ClearPatternData();
    }

    public void OnTileClick(Vector3Int cell)
    {
        UnitData unit = session.GetUnitDataById(session.selectedUnitId);
        if (unit == null) { sm.GoToNone(); return; }

        // Click own unit — deselect
        if (cell == unit.CurrentCell) { sm.GoToNone(); return; }

        // Click another unit — switch selection
        UnitData unitAtCell = session.GetUnitDataAt(cell);
        if (unitAtCell != null) { sm.GoToUnitSelected(unitAtCell.Id); return; }

        // Click valid target cell — lock preview cell
        if (session.IsMyUnit(session.selectedUnitId) && session.CurrentTargetPatternCells.Contains(cell))
        {
            session.currentPreviewCell = cell;
            session.isTargetLocked = true;
            CalculateAoECells(cell);
            scene.visualController.UpdateVisualOnStateChange();
            return;
        }

        sm.GoToNone();
    }

    public void OnTileHover(Vector3Int cell)
    {
        if (!session.IsMyUnit(session.selectedUnitId)) return;

        UnitData unit = session.GetUnitDataById(session.selectedUnitId);
        if (unit == null) return;

        // Only preview AoE if not locked to a cell
        if (session.currentPreviewCell == unit.CurrentCell)
        {
            if (session.CurrentTargetPatternCells.Contains(cell))
                CalculateAoECells(cell);
            else
                session.CurrentAoECells.Clear();
        }

        scene.visualController.HoverShadow();
    }

    public void OnDecision()
    {
        // Confirm — send decision with current preview cell
        int unitId = session.selectedUnitId;
        int skillId = session.currentSkillId;
        if (unitId == -1 || skillId == -1) return;
        if (session.currentPreviewCell == session.GetUnitDataById(unitId)?.CurrentCell) return; // no target selected

        scene.bridge.SendDecisionServerRpc(
            unitId,
            session.currentPreviewCell,
            DecisionType.ActivateAction,
            skillId,
            session.CurrentToken);
    }

    public void OnDecision(int skillId)
    {
        // Switch skill
        session.currentSkillId = skillId;
        session.currentPreviewCell = session.GetUnitDataById(session.selectedUnitId)?.CurrentCell ?? default;
        CalculateTargetPattern();
        session.CurrentAoECells.Clear();
        scene.visualController.UpdateVisualOnStateChange();
    }

    public void Cancel()
    {
        sm.GoToNone();
    }

    // -------------------------------------------------------
    // Pattern Calculation
    // -------------------------------------------------------

    void CalculateTargetPattern()
    {
        session.CurrentTargetPatternCells.Clear();

        UnitData unit = session.GetUnitDataById(session.selectedUnitId);
        if (unit == null || unit.PatternOverrides == null) return;

        foreach (var o in unit.PatternOverrides)
        {
            if (o.SkillId != session.currentSkillId) continue;
            if (o.TargetPatternCells == null) return;

            foreach (var offset in o.TargetPatternCells)
                session.CurrentTargetPatternCells.Add(new Vector3Int(
                    unit.CurrentCell.x + offset.x,
                    unit.CurrentCell.y + offset.y,
                    0));
            return;
        }
    }

    void CalculateAoECells(Vector3Int targetCell)
    {
        session.CurrentAoECells.Clear();

        UnitData unit = session.GetUnitDataById(session.selectedUnitId);
        if (unit == null || unit.PatternOverrides == null) return;

        foreach (var o in unit.PatternOverrides)
        {
            if (o.SkillId != session.currentSkillId) continue;
            if (o.AoEPatternCells == null) return;

            var tempPattern = ScriptableObject.CreateInstance<SkillPattern>();
            tempPattern.cells = o.AoEPatternCells;

            session.CurrentAoECells.AddRange(
                    ClientPatternResolver.GetAoECells(
                    tempPattern,
                    unit.CurrentCell,
                    targetCell,
                    session)
                );
            return;
        }
    }
}