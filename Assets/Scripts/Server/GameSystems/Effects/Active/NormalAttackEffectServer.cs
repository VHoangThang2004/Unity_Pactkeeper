// Server/GameSystems/Effects/Concrete/NormalAttackEffectServer.cs
using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NormalAttackEffectServer", menuName = "SRPG/Effects/Server/NormalAttack")]
public class NormalAttackEffectServer : ServerActiveEffectBase
{
    public override InstantType InstantType => InstantType.NonInstant;
    public override EffectType EffectType => EffectType.Damage;

    public override ResolveResult Apply(
        ServerMatchSession session,
        int sourceUnitId,
        Vector3Int target) //1 target only for this effect
    {
        UnitData unit = session.GetUnit(sourceUnitId);
        if (unit == null)
        {
            Debug.LogError($"[MoveEffect] Unit {sourceUnitId} not found!");
            return new ResolveResult { EffectId = -1 };
        }

        // Validate target
        UnitData targetUnit = session.GetUnitAt(target);
        if (targetUnit == null)
        {
            Debug.LogWarning("Missing target!");
            return new ResolveResult { EffectId = effectId };
        }

        if (session.GetTeamIdByUnitId(sourceUnitId) == session.GetTeamIdByUnitId(targetUnit.Id))
        {
            Debug.LogWarning("Cannot damage ally !");
            return new ResolveResult { EffectId = -1 };
        }

        Vector3Int fromCell = unit.CurrentCell;
        targetUnit.CurrentHP = targetUnit.CurrentHP - Mathf.RoundToInt(unit.DamageMultiplier * 10);
        
        if (targetUnit.CurrentHP <= 0)
        {
            session.Kill(targetUnit.Id);
            return new ResolveResult
            {
                EffectId = effectId,
                SourceUnitId = sourceUnitId,
                SourceCell = fromCell,
                TargetCells = new List<Vector3Int> { target },
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
            TargetCells = new List<Vector3Int> { target },
            Partial = SessionSnapshotData.Partial(new List<UnitData> { unit, targetUnit })
        };
    }
}