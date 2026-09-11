using System.Collections;
using UnityEngine;

[CreateAssetMenu(fileName = "Effect_Move_Visual", menuName = "SRPG/Effects/Client/Move")]
public class MoveEffectVisual : ClientActiveEffectBase
{
    public override InstantType InstantType => InstantType.Instant;

    public override IEnumerator Replay(
        ResolveResult result,
        ClientScene scene,
        ClientMatchSession session)
    {
        ClientUnit sceneUnit = scene.GetSceneUnitById(result.SourceUnitId);
        if (sceneUnit == null) yield break;
        if (result.TargetCells == null || result.TargetCells.Count == 0)
        {
            sceneUnit.SyncPositionToCurrentSession();
            yield break;
        }

        UnitData unitData = session.GetUnitDataById(sceneUnit.unitId);
        Vector3Int targetCell = result.TargetCells[0];

        sceneUnit.isResolvingAnimation = true;

        Vector3 start = sceneUnit.transform.position;
        Vector3 end = scene.movableTilemap.GetCellCenterWorld(targetCell);

        float t = 0f;
        while (t < 1f)
        {
            if (!sceneUnit.isResolvingAnimation) yield break;
            t += Time.deltaTime / resolveDuration;
            sceneUnit.transform.position = Vector3.Lerp(start, end, t);
            yield return null;
        }

        sceneUnit.transform.position = end;
        unitData.CurrentCell = targetCell;
        sceneUnit.isResolvingAnimation = false;
    }
}