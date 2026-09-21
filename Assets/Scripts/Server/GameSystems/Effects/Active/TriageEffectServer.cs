// Server/GameSystems/Effects/Concrete/NormalAttackEffectServer.cs
using System;
using System.Collections.Generic;
// using Unity.Mathematics;
using UnityEngine;

[CreateAssetMenu(fileName = "TriageEffectServer", menuName = "SRPG/Effects/Server/Triage")]
public class TriageEffectServer : ServerActiveEffectBase
{
    public override InstantType InstantType => InstantType.HalfInstant;
    public override EffectType EffectType => EffectType.ApplyBuff;

    public override ResolveResult Apply(
        ServerMatchSession session,
        int sourceUnitId,
        Vector3Int sourceCell,
        Vector3Int targetCell) //1 target only for this effect (dont need to check aoe pattern). If an effect with complicated aoe pattern then must check affected cells from this target
    {
        UnitData sourceUnit = session.GetUnitByUnitId(sourceUnitId);
        if (sourceUnit == null)
        {
            Debug.LogError($"[MoveEffect] Unit {sourceUnitId} not found!");
            return new ResolveResult { EffectId = -1 };
        }

        Vector3Int fromCell = sourceUnit.CurrentCell;
        Vector3Int newTargetCell = OffsetRecalculator.RecalculateNewTargetCell(sourceCell, targetCell, fromCell);
        // Validate target
        UnitData targetUnit = session.GetUnitAt(newTargetCell);
        if (targetUnit == null)
        {
            Debug.LogWarning("Missing target!");
            return new ResolveResult
            {
                EffectId = -1
            };
        }

        if (session.GetTeamIdByUnitId(sourceUnitId) != session.GetTeamIdByUnitId(targetUnit.Id) || targetUnit.CurrentStep <= 0)
        {
            Debug.LogWarning("Cannot apply Triage! to the target !");
            return new ResolveResult
            {
                EffectId = effectId,
                SourceUnitId = sourceUnitId,
                SourceCell = fromCell,
                TargetCells = new List<Vector3Int> { newTargetCell },
                Partial = SessionSnapshotData.Partial(new List<UnitData> { sourceUnit })
            };
        }
        sourceUnit.CurrentStep += targetUnit.CurrentStep;
        targetUnit.CurrentStep = 0;
        session.ReadyUnitIds.Add(targetUnit.Id);




        return new ResolveResult
        {
            EffectId = effectId,
            SourceUnitId = sourceUnitId,
            SourceCell = fromCell,
            TargetCells = new List<Vector3Int> { newTargetCell },
            Partial = SessionSnapshotData.Partial(new List<UnitData> { sourceUnit, targetUnit })
        };
    }
}