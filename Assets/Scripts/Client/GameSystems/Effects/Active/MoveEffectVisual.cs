using System.Collections;
using UnityEngine;

[CreateAssetMenu(fileName = "Effect_Move_Visual", menuName = "SRPG/Effects/Client/Move")]
public class MoveEffectVisual : ClientActiveEffectBase
{
    public override InstantType InstantType => InstantType.Instant;

    public override IEnumerator Replay(
        ClientUnit unit,
        ResolveResult result,
        ClientScene scene,
        ClientMatchSession session)
    {
        if (result.TargetCells == null || result.TargetCells.Count == 0)
        {
            unit.SyncPositionToCurrentSession();
            yield break;
        }

        UnitData unitData = session.GetUnitDataById(unit.unitId);
        Vector3Int targetCell = result.TargetCells[0];

        unit.isResolvingAnimation = true;

        Vector3 start = unit.transform.position;
        Vector3 end = scene.movableTilemap.GetCellCenterWorld(targetCell);

        float t = 0f;
        while (t < 1f)
        {
            if (!unit.isResolvingAnimation) yield break;
            t += Time.deltaTime / resolveDuration;
            unit.transform.position = Vector3.Lerp(start, end, t);
            yield return null;
        }

        unit.transform.position = end;
        unitData.CurrentCell = targetCell;
        unit.isResolvingAnimation = false;
    }
}