using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[CreateAssetMenu(fileName = "PushEffectServer", menuName = "SRPG/Effects/Server/PushEffect")]
public class PushEffectServer : ServerActiveEffectBase
{
    public override InstantType InstantType => InstantType.Instant;
    public override EffectType EffectType => EffectType.Move;

    public override ResolveResult Apply(
        ServerMatchSession session,
        int sourceUnitId,
        Vector3Int sourceCell,
        Vector3Int targetCell)
    //fixed target pattern
    //fixed aoe pattern

    {
        UnitData sourceUnit = session.GetUnitByUnitId(sourceUnitId);
        UnitData targetUnit = session.GetUnitAt(targetCell);
        if (sourceUnit == null || targetUnit == null)
        {
            Debug.LogError($"[PushEffect] Any unit missing!");
            return new ResolveResult { EffectId = -1 };
        }
        Vector3Int targetNewCell = OffsetRecalculator.RecalculateNewTargetCell(sourceCell, targetCell, targetCell);
        if (session.GetUnitAt(targetNewCell) != null || !session.Map.IsWalkable(targetNewCell.x, targetNewCell.y))
        {
            Debug.LogError($"[PushEffect] Not valid cell!");
            return new ResolveResult { EffectId = -1 };
        }
        targetUnit.CurrentCell = targetNewCell;

        return new ResolveResult
        {
            EffectId = effectId,
            SourceUnitId = sourceUnitId,
            SourceCell = sourceUnit.CurrentCell,
            TargetCells = new List<Vector3Int> { targetCell },
            Partial = SessionSnapshotData.Partial(new List<UnitData> { sourceUnit, targetUnit })
        };
    }
}