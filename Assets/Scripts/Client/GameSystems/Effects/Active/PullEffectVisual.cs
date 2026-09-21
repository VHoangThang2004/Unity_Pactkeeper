using System.Collections;
using UnityEngine;

[CreateAssetMenu(fileName = "PullEffectVisual", menuName = "SRPG/Effects/Client/PullEffect")]
public class PullEffectVisual : ClientActiveEffectBase
{
    public override InstantType InstantType => InstantType.HalfInstant;
    public override string GetDescription(UnitData unit,ClientEffectRegistry effectRegistry)
    {
        return " Pull the selected enemy target.";
    }

    public override IEnumerator Replay(
        ResolveResult result,
        ClientScene scene,
        ClientMatchSession session)
    {
        if (result.TargetCells == null || result.TargetCells.Count == 0) yield break;

        UnitData sourceUnit = session.GetUnitDataById(result.SourceUnitId);
        UnitData targetUnit = session.GetUnitDataAt(result.TargetCells[0]);
        UnitData postTargetData = result.Partial.Units.Find(u => u.Id == targetUnit.Id);

        if (sourceUnit == null || targetUnit == null || postTargetData == null) yield break;

        ClientUnit sourceSceneUnit = scene.GetSceneUnitById(result.SourceUnitId);
        ClientUnit targetSceneUnit = scene.GetSceneUnitById(targetUnit.Id);
        if (sourceSceneUnit == null || targetSceneUnit == null) yield break;
        scene.cameraController.CenterOn(sourceSceneUnit.transform.position);


        targetSceneUnit.isResolvingAnimation = true;

        Vector3 targetStartPos = targetSceneUnit.transform.position;
        Vector3 targetEndPos = scene.movableTilemap.GetCellCenterWorld(postTargetData.CurrentCell);
        //0. Rope extends from source toward target
        scene.ropeLine.Show(sourceSceneUnit.transform.position, sourceSceneUnit.transform.position);
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / (resolveDuration * 0.2f);
            scene.ropeLine.SetPositions(
                sourceSceneUnit.transform.position,
                Vector3.Lerp(sourceSceneUnit.transform.position, targetStartPos, t));
            yield return null;
        }

        // 1. Show rope latched to both ends
        scene.ropeLine.Show(sourceSceneUnit.transform.position, targetStartPos);
        // 2. Play tied VFX on target (async)
        scene.StartCoroutine(PlayBindedVFX(targetSceneUnit));


        yield return new WaitForSeconds(resolveDuration * 0.4f);

        // 3. Move target toward source
        t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / (resolveDuration * 0.4f);
            targetSceneUnit.transform.position = Vector3.Lerp(targetStartPos, targetEndPos, t);
            scene.ropeLine.SetPositions(
                sourceSceneUnit.transform.position,
                targetSceneUnit.transform.position);
            yield return null;
        }

        targetSceneUnit.transform.position = targetEndPos;
        scene.ropeLine.Hide();

        targetSceneUnit.isResolvingAnimation = false;
    }

    IEnumerator PlayBindedVFX(ClientUnit targetUnit)
    {
        if (targetUnit.VFXAnimator != null)
        {
            targetUnit.VFXAnimator.SetBool("IsIdling", false);
            targetUnit.VFXAnimator.Play("BindedVFX", 0, 0f);
        }
        yield return new WaitForSeconds(resolveDuration * 0.8f);
        if (targetUnit.VFXAnimator != null)
        {
            targetUnit.VFXAnimator.Play("NoEffect", 0, 0f);
            targetUnit.VFXAnimator.SetBool("IsIdling", true);
        }
    }
}