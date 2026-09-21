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
        List<ActiveEffectInstance> activeEffects = session.GetUnitActiveEffects(targetUnit.Id);
        foreach (var buffId in buffEffectIds)
        {
            var buff = session.effectRegistry.Get(buffId);
            if (buff == null) continue;
            if (buff is not ServerPassiveEffectBase passive) continue;

            var existing = activeEffects.Find(e => e.effect.effectId == buffId);
            if (existing != null)
            {
                // Overwrite duration if new one is longer
                if (!existing.isPermanent && passive.durationInstants > existing.effect.durationInstants)
                    existing.effect.durationInstants = passive.durationInstants;
                continue;
            }
            activeEffects.Add(new ActiveEffectInstance
            {
                effect = passive,
                isPermanent = passive.isPermanent,
                remainingInstants = passive.durationInstants
            });
        }

        targetUnit.ActiveEffectIds = activeEffects.Select(e => e.effect.effectId).ToArray();
        UnitRecalculator.RecalculateAll(session);

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