// Server/GameSystems/Effects/Concrete/NormalAttackEffectServer.cs
using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NormalProjectileAttackEffectServer", menuName = "SRPG/Effects/Server/NormalProjectileAttack")]
public class NormalProjectileAttackEffectServer : ServerActiveEffectBase
{
    public override InstantType InstantType => InstantType.NonInstant;
    public override EffectType EffectType => EffectType.Damage;

    public override ResolveResult Apply(
        ServerMatchSession session,
        int sourceUnitId,
        Vector3Int sourceCell,
        Vector3Int targetCell) //1 target only for this effect (dont need to check aoe pattern)
    {
        UnitData unit = session.GetUnitByUnitId(sourceUnitId);
        if (unit == null)
        {
            Debug.LogError($"[MoveEffect] Unit {sourceUnitId} not found!");
            return new ResolveResult { EffectId = -1 };
        }

        Vector3Int fromCell = unit.CurrentCell;
        Vector3Int newTargetCell = OffsetRecalculator.RecalculateNewTargetCell(sourceCell,targetCell,fromCell);
        // Validate target
        UnitData targetUnit = session.GetUnitAt(newTargetCell);
        if (targetUnit == null)
        {
            Debug.LogWarning("Missing target!");
            return new ResolveResult
            {
                EffectId = effectId,
                SourceUnitId = sourceUnitId,
                SourceCell = fromCell,
                TargetCells = new List<Vector3Int> { newTargetCell },
                Partial = SessionSnapshotData.Partial(new List<UnitData> { unit })
            };
        }

        if (session.GetTeamIdByUnitId(sourceUnitId) == session.GetTeamIdByUnitId(targetUnit.Id))
        {
            Debug.LogWarning("Cannot damage ally !");
            return new ResolveResult { EffectId = -1 };
        }

        targetUnit.CurrentHP = targetUnit.CurrentHP - Mathf.RoundToInt(unit.DamageMultiplier * baseValue / 100);

        if (targetUnit.CurrentHP <= 0)
        {
            session.Kill(targetUnit.Id);
            return new ResolveResult
            {
                EffectId = effectId,
                SourceUnitId = sourceUnitId,
                SourceCell = fromCell,
                TargetCells = new List<Vector3Int> { newTargetCell },
                Partial = SessionSnapshotData.Full(
                    session.MapId,
                    session.CurrentTeamTurnId,
                    session.GetAllCopyTeamData(),
                    session.GetAllCopyUnits(),
                    new TimelineData
                    {
                        currentInstant = session.CurrentInstant,
                        isPaused = session.TimelineState != ServerSessionState.Flowing,
                        flag = session.FlaggedTeamId
                    })
            };
        }
        return new ResolveResult
        {
            EffectId = effectId,
            SourceUnitId = sourceUnitId,
            SourceCell = fromCell,
            TargetCells = new List<Vector3Int> { newTargetCell },
            Partial = SessionSnapshotData.Partial(new List<UnitData> { unit, targetUnit })
        };
    }
}