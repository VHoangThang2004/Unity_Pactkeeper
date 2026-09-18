// Server/GameSystems/Effects/Concrete/NormalAttackEffectServer.cs
using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

[CreateAssetMenu(fileName = "NormalHealEffectServer", menuName = "SRPG/Effects/Server/NormalHeal")]
public class NormalHealEffectServer : ServerActiveEffectBase
{
    public override InstantType InstantType => InstantType.HalfInstant;
    public override EffectType EffectType => EffectType.Heal;

    public override ResolveResult Apply(
        ServerMatchSession session,
        int sourceUnitId,
        Vector3Int target) //1 target only for this effect (dont need to check aoe pattern). If an effect with complicated aoe pattern then must check affected cells from this target
    {
        UnitData unit = session.GetUnitByUnitId(sourceUnitId);
        if (unit == null)
        {
            Debug.LogError($"[MoveEffect] Unit {sourceUnitId} not found!");
            return new ResolveResult { EffectId = -1 };
        }

        Vector3Int fromCell = unit.CurrentCell;
        // Validate target
        UnitData targetUnit = session.GetUnitAt(target);
        if (targetUnit == null)
        {
            Debug.LogWarning("Missing target!");
            return new ResolveResult
            {
                EffectId = effectId,
                SourceUnitId = sourceUnitId,
                SourceCell = fromCell,
                TargetCells = new List<Vector3Int> { target },
                Partial = SessionSnapshotData.Partial(new List<UnitData> { unit })
            };
        }

        if (session.GetTeamIdByUnitId(sourceUnitId) != session.GetTeamIdByUnitId(targetUnit.Id))
        {
            Debug.LogWarning("Cannot heal enemy !");
            return new ResolveResult
            {
                EffectId = effectId,
                SourceUnitId = sourceUnitId,
                SourceCell = fromCell,
                TargetCells = new List<Vector3Int> { target },
                Partial = SessionSnapshotData.Partial(new List<UnitData> { unit })
            };
        }

        targetUnit.CurrentHP = Mathf.Min(targetUnit.CurrentHP + Mathf.RoundToInt(unit.DamageMultiplier * baseValue/100), targetUnit.MaxHP);

        return new ResolveResult
        {
            EffectId = effectId,
            SourceUnitId = sourceUnitId,
            SourceCell = fromCell,
            TargetCells = new List<Vector3Int> { target },
            Partial = SessionSnapshotData.Partial(new List<UnitData> { unit, targetUnit })
        };
    }
}