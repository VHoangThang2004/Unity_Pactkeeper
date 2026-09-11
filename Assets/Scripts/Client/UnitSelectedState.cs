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

        UnitData unit = session.GetUnitDataById(session.selectedUnitId);
        session.currentSkillId = unit?.MovementSkillId ?? -1;
        session.isTargetLocked = false;

        ReloadTargetPattern();

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

        // Click valid target cell first — takes priority over unit switching
        if (session.IsMyUnit(session.selectedUnitId) && session.CurrentTargetableCells.Contains(cell))
        {
            session.currentPreviewCell = cell;
            session.isTargetLocked = true;
            CalculateAoECells(cell);
            scene.visualController.UpdateVisualOnStateChange();
            return;
        }

        // Click another unit — switch selection
        UnitData unitAtCell = session.GetUnitDataAt(cell);
        if (unitAtCell != null && unitAtCell.Id != session.selectedUnitId)
        {
            sm.GoToUnitSelected(unitAtCell.Id);
            return;
        }

        // Click own unit or invalid cell — deselect
        sm.GoToNone();
    }

    public void OnTileHover(Vector3Int cell)
    {
        if (!session.IsMyUnit(session.selectedUnitId)) return;

        UnitData unit = session.GetUnitDataById(session.selectedUnitId);
        if (unit == null) return;

        // Only preview AoE if not locked
        if (!session.isTargetLocked)
        {
            if (session.CurrentTargetableCells.Contains(cell))
                CalculateAoECells(cell);
            else
                session.CurrentAoECells.Clear();
        }

        scene.visualController.HoverShadow();
    }

    public void OnDecision()
    {
        int unitId = session.selectedUnitId;
        int skillId = session.currentSkillId;
        if (unitId == -1 || skillId == -1) return;
        if (!session.isTargetLocked) return;

        scene.bridge.SendDecisionServerRpc(
            unitId,
            session.currentPreviewCell,
            DecisionType.ActivateAction,
            skillId,
            session.CurrentToken);
    }

    public void OnDecision(int skillId)
    {
        session.currentSkillId = skillId;
        session.isTargetLocked = false;
        session.currentPreviewCell = default;
        ReloadTargetPattern();
        Debug.Log($"[UnitSelected] OnDecision — switched to skillId={skillId}, targetable={session.CurrentTargetableCells.Count}");
        scene.visualController.UpdateVisualOnStateChange();
    }

    public void Cancel()
    {
        sm.GoToNone();
    }

    // -------------------------------------------------------
    // Pattern
    // -------------------------------------------------------

    public void ReloadTargetPattern()
    {
        session.CurrentTargetableCells.Clear();
        if (!session.isTargetLocked)
            session.CurrentAoECells.Clear();

        UnitData unit = session.GetUnitDataById(session.selectedUnitId);
        if (unit == null) { Debug.LogWarning("[UnitSelected] unit null"); return; }
        if (unit.SkillPatterns == null) { Debug.LogWarning("[UnitSelected] SkillPatterns null"); return; }

        Debug.Log($"[UnitSelected] Looking for skillId={session.currentSkillId}, patterns count={unit.SkillPatterns.Length}");
        foreach (var o in unit.SkillPatterns)
            Debug.Log($"[UnitSelected] Pattern entry — SkillId={o.SkillId}, TargetCells={o.TargetCells?.Length ?? 0}");

        foreach (var o in unit.SkillPatterns)
        {
            if (o.SkillId != session.currentSkillId) continue;
            if (o.TargetCells != null)
                foreach (var cell in o.TargetCells)
                    session.CurrentTargetableCells.Add(cell);
            Debug.Log($"[UnitSelected] Loaded {session.CurrentTargetableCells.Count} targetable cells");
            return;
        }

        Debug.LogWarning($"[UnitSelected] No pattern found for skillId={session.currentSkillId}");
    }
    void CalculateAoECells(Vector3Int targetCell)
    {
        session.CurrentAoECells.Clear();

        UnitData unit = session.GetUnitDataById(session.selectedUnitId);
        if (unit?.SkillPatterns == null) return;

        foreach (var o in unit.SkillPatterns)
        {
            if (o.SkillId != session.currentSkillId) continue;
            if (o.AoePattern == null) return;

            var tempPattern = ScriptableObject.CreateInstance<SkillPattern>();
            tempPattern.cells = o.AoePattern;

            session.CurrentAoECells.AddRange(
                ClientPatternResolver.GetAoECells(
                    tempPattern,
                    unit.CurrentCell,
                    targetCell,
                    session));
            return;
        }
    }
}