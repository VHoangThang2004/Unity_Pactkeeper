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
        Vector3Int target)
    {
        var unit = session.GetUnit(sourceUnitId);
        if (unit == null)
        {
            Debug.LogError($"[MoveEffect] Unit {sourceUnitId} not found!");
            return default;
        }

        // Validate target
        if (!session.Map.IsWalkable(target.x, target.y))
        {
            Debug.LogWarning($"[MoveEffect] Target {target} not walkable!");
            return default;
        }

        if (session.GetUnitAt(target) != null)
        {
            Debug.LogWarning($"[MoveEffect] Target {target} occupied!");
            return default;
        }

        Vector3Int fromCell = unit.CurrentCell;

        // Apply directly — no session helper
        unit.CurrentCell = target;

        return new ResolveResult
        {
            EffectId = effectId,
            SourceUnitId = sourceUnitId,
            SourceCell = fromCell,
            TargetCells = new List<Vector3Int> { target },
            Partial = SessionSnapshotData.Partial(new List<UnitData> { unit })
        };
    }
}