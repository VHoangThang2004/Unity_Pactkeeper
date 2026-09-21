using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[CreateAssetMenu(fileName = "ClassSkillAssasinEffectServer", menuName = "SRPG/Effects/Server/ClassSkillAssasin")]
public class ClassSkillAssasinEffectServer : ServerActiveEffectBase
{
    public override InstantType InstantType => InstantType.Instant;
    public override EffectType EffectType => EffectType.ApplyBuff;

    public override ResolveResult Apply(
        ServerMatchSession session,
        int sourceUnitId,
        Vector3Int sourceCell,
        Vector3Int targetCell) //1 target only for this effect (dont need to check aoe pattern)
    {
        UnitData sourceUnit = session.GetUnitByUnitId(sourceUnitId);
        if (sourceUnit == null)
        {
            Debug.LogError($"[MoveEffect] Unit {sourceUnitId} not found!");
            return new ResolveResult { EffectId = -1 };
        }

        for (int i = 0; i < sourceUnit.SkillUsages.Length; i++)
        {
            if (sourceUnit.SkillUsages[i].SkillId == sourceUnit.MovementSkillId)
            {
                sourceUnit.SkillUsages[i].UsageThisInstant = 0;
                break;
            }
        }

        return new ResolveResult
        {
            EffectId = effectId,
            SourceUnitId = sourceUnitId,
            SourceCell = sourceUnit.CurrentCell,
            TargetCells = new List<Vector3Int> { targetCell },
            Partial = SessionSnapshotData.Partial(new List<UnitData> { sourceUnit })
        };
    }
}