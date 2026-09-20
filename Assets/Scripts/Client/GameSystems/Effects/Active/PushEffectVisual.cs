using System.Collections;
using UnityEngine;

[CreateAssetMenu(fileName = "PushEffectVisual", menuName = "SRPG/Effects/Client/PushEffect")]
public class PushEffectVisual : ClientActiveEffectBase
{
    public override InstantType InstantType => InstantType.Instant;
    public override string GetDescription(UnitData unit)
    {
        return " Push the selected enemy target.";
    }

    public override IEnumerator Replay(
        ResolveResult result,
        ClientScene scene,
        ClientMatchSession session)
    {
        if (result.TargetCells == null || result.TargetCells.Count == 0) yield break;

        UnitData sourceUnit = session.GetUnitDataById(result.SourceUnitId); // data before resolve ~ data in session
        UnitData targetUnit = session.GetUnitDataAt(result.TargetCells[0]); // data before resolve ~ data in session
        UnitData postTargetData = result.Partial.Units.Find(u => u.Id == targetUnit.Id); // data after resolve

        if (sourceUnit == null || targetUnit == null || postTargetData == null) yield break;

        ClientUnit sourceSceneUnit = scene.GetSceneUnitById(result.SourceUnitId);
        ClientUnit targetSceneUnit = scene.GetSceneUnitById(targetUnit.Id);
        if (sourceSceneUnit == null || targetSceneUnit == null) yield break;

        sourceSceneUnit.isResolvingAnimation = true;
        targetSceneUnit.isResolvingAnimation = true;

        Vector3 sourceStartPos = sourceSceneUnit.transform.position;
        Vector3 targetStartPos = targetSceneUnit.transform.position;
        Vector3 targetEndPos = scene.movableTilemap.GetCellCenterWorld(postTargetData.CurrentCell);
        float stopDistance = 0.8f;
        Vector3 dir = (targetStartPos - sourceStartPos).normalized;
        Vector3 midPos = targetStartPos - dir * stopDistance;
        yield return sourceToTarget(sourceSceneUnit, sourceStartPos, targetStartPos);
        yield return new WaitForSeconds(resolveDuration * 0.1f);
        scene.StartCoroutine(targetToNewPos(targetSceneUnit, targetStartPos, scene.movableTilemap.GetCellCenterWorld(postTargetData.CurrentCell)));
        yield return new WaitForSeconds(resolveDuration * 0.1f);
        yield return sourceToSource(sourceSceneUnit, midPos, sourceStartPos);


        sourceSceneUnit.transform.position = sourceStartPos;
        targetSceneUnit.transform.position = targetEndPos;

        targetUnit.CurrentCell = postTargetData.CurrentCell;

        sourceSceneUnit.isResolvingAnimation = false;
        targetSceneUnit.isResolvingAnimation = false;
    }

    public IEnumerator sourceToTarget(ClientUnit sourceUnit, Vector3 startPos, Vector3 targetPos)
    {
        float stopDistance = 0.8f;
        Vector3 dir = (targetPos - startPos).normalized;
        Vector3 midPos = targetPos - dir * stopDistance;

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / (resolveDuration * 0.4f);
            sourceUnit.transform.position = Vector3.Lerp(startPos, midPos, t);
            yield return null;
        }
    }

    public IEnumerator targetToNewPos(ClientUnit targetUnit, Vector3 startPos, Vector3 endPos)
    {
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / (resolveDuration * 0.4f);
            targetUnit.transform.position = Vector3.Lerp(startPos, endPos, t);
            yield return null;
        }
        targetUnit.transform.position = endPos;
    }

    public IEnumerator sourceToSource(ClientUnit sourceUnit, Vector3 midPos, Vector3 startPos)
    {
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / (resolveDuration * 0.4f);
            sourceUnit.transform.position = Vector3.Lerp(midPos, startPos, t);
            yield return null;
        }
        sourceUnit.transform.position = startPos;
    }

}