using System.Collections;
using UnityEngine;

[CreateAssetMenu(fileName = "GuardEffectVisual", menuName = "SRPG/Effects/Client/Guard")]
public class GuardEffectVisual : ClientActiveEffectBase
{
    public override InstantType InstantType => InstantType.Instant;
    public override string GetDescription(UnitData unit, ClientEffectRegistry effectRegistry)
    {
        return " Swap position with the selected ally target.";
    }

    public override IEnumerator Replay(
        ResolveResult result,
        ClientScene scene,
        ClientMatchSession session)
    {
        if (result.TargetCells == null || result.TargetCells.Count == 0) yield break;

        UnitData sourceUnitData = session.GetUnitDataById(result.SourceUnitId);
        UnitData targetUnitData = session.GetUnitDataAt(result.TargetCells[0]);
        if (sourceUnitData == null || targetUnitData == null) yield break;

        ClientUnit sourceSceneUnit = scene.GetSceneUnitById(result.SourceUnitId);
        ClientUnit targetSceneUnit = scene.GetSceneUnitById(targetUnitData.Id);
        if (sourceSceneUnit == null || targetSceneUnit == null) yield break;
        
        Vector3 midPoint = (sourceSceneUnit.transform.position + targetSceneUnit.transform.position) / 2f;
        scene.cameraController.CenterOn(midPoint);

        sourceSceneUnit.isResolvingAnimation = true;
        targetSceneUnit.isResolvingAnimation = true;

        Vector3 sourceStart = sourceSceneUnit.transform.position;
        Vector3 targetStart = targetSceneUnit.transform.position;

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / resolveDuration;
            sourceSceneUnit.transform.position = Vector3.Lerp(sourceStart, targetStart, t);
            targetSceneUnit.transform.position = Vector3.Lerp(targetStart, sourceStart, t);
            yield return null;
        }

        sourceSceneUnit.transform.position = targetStart;
        targetSceneUnit.transform.position = sourceStart;

        sourceUnitData.CurrentCell = result.TargetCells[0];
        targetUnitData.CurrentCell = result.SourceCell;

        sourceSceneUnit.isResolvingAnimation = false;
        targetSceneUnit.isResolvingAnimation = false;
    }
}