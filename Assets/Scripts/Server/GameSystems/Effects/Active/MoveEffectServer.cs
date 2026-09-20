// Server/GameSystems/Effects/Concrete/MoveEffect.cs
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Effect_Move", menuName = "SRPG/Effects/Server/Move")]
public class MoveEffectServer : ServerActiveEffectBase
{
    public override InstantType InstantType => InstantType.Instant;
    public override EffectType EffectType => EffectType.Move;

    public override ResolveResult Apply(
        ServerMatchSession session,
        int sourceUnitId,
        Vector3Int sourceCell,
        Vector3Int targetCell)
    {
        var unit = session.GetUnitByUnitId(sourceUnitId);
        if (unit == null)
        {
            Debug.LogError($"[MoveEffect] Unit {sourceUnitId} not found!");
            return new ResolveResult { EffectId = -1 };
        }

        // Validate target
        if (!session.Map.IsWalkable(targetCell.x, targetCell.y))
        {
            Debug.LogWarning($"[MoveEffect] Target {targetCell} not walkable!");
            return new ResolveResult { EffectId = -1 };
        }

        if (session.GetUnitAt(targetCell) != null)
        {
            Debug.LogWarning($"[MoveEffect] Target {targetCell} occupied!");
            return new ResolveResult { EffectId = -1 };
        }

        Vector3Int fromCell = unit.CurrentCell;

        // Apply directly — no session helper
        unit.CurrentCell = targetCell;

        return new ResolveResult
        {
            EffectId = effectId,
            SourceUnitId = sourceUnitId,
            SourceCell = fromCell,
            TargetCells = new List<Vector3Int> { targetCell },
            Partial = SessionSnapshotData.Partial(new List<UnitData> { unit })
        };
    }
}