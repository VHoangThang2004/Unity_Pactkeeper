using System.Collections;
using UnityEngine;

/// <summary>
/// Moves the source unit to target cell.
/// Instant effect — resolves immediately.
/// Step cost = pathLength / moveRange (dynamic, capped at 1.0).
/// </summary>
[CreateAssetMenu(fileName = "Effect_Move", menuName = "SRPG/Effects/Move")]
public class MoveEffect : EffectBase
{
    public override bool IsInstant => true;

    public override EffectResult Apply(ServerMatchSession session, int sourceUnitId, Vector3Int target)
    {
        var unit = session.GetUnit(sourceUnitId);
        if (unit == null)
        {
            Debug.LogError($"[MoveEffect] Unit {sourceUnitId} not found!");
            return default;
        }

        var occupied = session.GetOccupiedCells(sourceUnitId);
        var path = GridPathfinder.FindPath(session.Map, unit.CurrentCell, target, occupied);
        int pathLength = path != null ? path.Count : 0;

        session.ApplyMove(sourceUnitId, target);

        return new EffectResult
        {
            Type = EffectType.Move,
            SourceUnitId = sourceUnitId,
            TargetUnitId = -1,
            Cell = target,
            Value = pathLength,
        };
    }

    public override IEnumerator Replay(ClientUnit unit, EffectResult result, ClientScene scene, ClientMatchSession session)
    {
        var path = GridPathfinder.FindPath(
            scene.movableTilemap,
            session.GetUnitDataById(unit.unitId).CurrentCell,
            result.Cell);

        if (path == null)
        {
            //error happens, cant replay resolve, proceed to skip and mark as completed 
            unit.SyncPosition();
            yield break;
        }

        // animate along path
        unit.isResolvingAnimation = true;
        foreach (var cell in path)
        {
            Vector3 start = unit.transform.position;
            Vector3 end = scene.movableTilemap.GetCellCenterWorld(cell);
            float t = 0f;
            while (t < 1f)
            {
                if (!unit.isResolvingAnimation) yield break; // emergency sync cancelled us
                t += Time.deltaTime / unit.moveDuration;
                unit.transform.position = Vector3.Lerp(start, end, t);
                yield return null;
            }
            unit.transform.position = end;
            session.GetUnitDataById(unit.unitId).CurrentCell = cell;
        }
        unit.isResolvingAnimation = false;
    }

    /// <summary>
    /// Dynamic step cost — pathLength / moveRange instead of fixed multiplier.
    /// </summary>
    public override float GetStepMultiplier(EffectResult result, UnitData unit)
    {
        if (unit == null || unit.MoveRange <= 0) return 1f;
        return Mathf.Clamp01((float)result.Value / unit.MoveRange);
    }
}