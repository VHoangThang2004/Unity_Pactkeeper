// Server/GameSystems/Effects/Concrete/NormalAttackEffectServer.cs
using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "GuardEffectServer", menuName = "SRPG/Effects/Server/Guard")]
public class GuardEffectServer : ServerActiveEffectBase
{
    public override InstantType InstantType => InstantType.Instant;
    public override EffectType EffectType => EffectType.Move;

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

        // Validate target
        UnitData targetUnit = session.GetUnitAt(targetCell);
        if (targetUnit == null)
        {
            Debug.LogWarning("Missing target!");
            return new ResolveResult { EffectId = -1 };
        }

        if (session.GetTeamIdByUnitId(sourceUnitId) != session.GetTeamIdByUnitId(targetUnit.Id))
        {
            Debug.LogWarning("Cannot guard enemy !");
            return new ResolveResult { EffectId = -1 };
        }

        Vector3Int fromCell = sourceUnit.CurrentCell;
        sourceUnit.CurrentCell = targetUnit.CurrentCell;
        targetUnit.CurrentCell = fromCell;

        return new ResolveResult
        {
            EffectId = effectId,
            SourceUnitId = sourceUnitId,
            SourceCell = fromCell,
            TargetCells = new List<Vector3Int> { targetCell },
            Partial = SessionSnapshotData.Partial(new List<UnitData> { sourceUnit, targetUnit })
        };
    }
}