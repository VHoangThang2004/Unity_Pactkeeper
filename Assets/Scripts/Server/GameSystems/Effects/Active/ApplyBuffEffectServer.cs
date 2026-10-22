using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[CreateAssetMenu(fileName = "ApplyBuffEffectServer", menuName = "SRPG/Effects/Server/ApplyBuffEffect")]
public class ApplyBuffEffectServer : ServerActiveEffectBase
{
    public override InstantType InstantType => InstantType.Instant;
    public override EffectType EffectType => EffectType.ApplyBuff;

    public int[] buffEffectIds;

    public override ResolveResult Apply(
        ServerMatchSession session,
        int sourceUnitId,
        Vector3Int sourceCell,
        Vector3Int targetCell)
    {
        UnitData sourceUnit = session.GetUnitByUnitId(sourceUnitId);
        UnitData targetUnit = session.GetUnitAt(targetCell);
        if (sourceUnit == null || targetUnit == null)
        {
            Debug.LogError($"[ApplyBuffEffect] Any unit missing!");
            return new ResolveResult { EffectId = -1 };
        }
        foreach (var buffId in buffEffectIds)
            if (session.effectRegistry.Get(buffId) is ServerPassiveEffectBase passive)
                session.AddEffect(targetUnit.Id, passive); // refreshes duration if already present

        UnitRecalculator.RecalculateAll(session); // also refreshes targetUnit.ActiveEffects

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