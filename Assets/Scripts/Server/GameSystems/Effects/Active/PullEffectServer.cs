using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "PullEffectServer", menuName = "SRPG/Effects/Server/PullEffect")]
public class PullEffectServer : ServerActiveEffectBase
{
    public override InstantType InstantType => InstantType.HalfInstant;
    public override EffectType EffectType => EffectType.Move;

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
            Debug.LogError($"[PullEffect] Any unit missing!");
            return new ResolveResult { EffectId = -1 };
        }

        Vector3Int from = targetUnit.CurrentCell;
        Vector3Int to = sourceUnit.CurrentCell;

        // BFS — only expand toward source
        var visited = new HashSet<Vector3Int> { from };
        var queue = new Queue<Vector3Int>();
        queue.Enqueue(from);

        Vector3Int bestCell = from;
        float bestDist = Vector3Int.Distance(from, to);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();

            var directions = new Vector3Int[]
            {
                new(1,0,0), new(-1,0,0), new(0,1,0), new(0,-1,0),
                new(1,1,0), new(1,-1,0), new(-1,1,0), new(-1,-1,0)
            };

            foreach (var dir in directions)
            {
                var next = current + dir;
                if (visited.Contains(next)) continue;
                if (!session.Map.IsWalkable(next.x, next.y)) continue;
                if (next == to) continue;
                if (session.GetUnitAt(next) != null) continue;
                if (!IsTowardSource(current, next, to)) continue;

                visited.Add(next);
                queue.Enqueue(next);

                float dist = Vector3Int.Distance(next, to);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    bestCell = next;
                }
            }
        }

        if (bestCell == from)
            return new ResolveResult { EffectId = -1 };

        targetUnit.CurrentCell = bestCell;

        return new ResolveResult
        {
            EffectId = effectId,
            SourceUnitId = sourceUnitId,
            SourceCell = sourceUnit.CurrentCell,
            TargetCells = new List<Vector3Int> { targetCell },
            Partial = SessionSnapshotData.Partial(new List<UnitData> { sourceUnit, targetUnit })
        };
    }

    bool IsTowardSource(Vector3Int current, Vector3Int next, Vector3Int source)
    {
        Vector2 toSource = new Vector2(source.x - current.x, source.y - current.y);
        Vector2 toNext = new Vector2(next.x - current.x, next.y - current.y);

        if (toSource == Vector2.zero || toNext == Vector2.zero) return false;

        float angle = Vector2.Angle(toSource, toNext);
        return angle < 45f;
    }
}