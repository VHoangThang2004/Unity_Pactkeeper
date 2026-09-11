using System.Collections;
using UnityEngine;

[CreateAssetMenu(fileName = "NormalAttackEffectVisual", menuName = "SRPG/Effects/Client/NormalAttack")]
public class NormalAttackEffectVisual : ClientActiveEffectBase
{
    public override InstantType InstantType => InstantType.NonInstant;

    public override IEnumerator Replay(
        ResolveResult result,
        ClientScene scene,
        ClientMatchSession session)
    {

        ClientUnit sceneUnit = scene.GetSceneUnitById(result.SourceUnitId);
        if (sceneUnit == null) yield break;
        if (result.TargetCells == null || result.TargetCells.Count == 0)
            yield break;

        // UnitData targetData = session.GetUnitDataAt(result.TargetCells[0]);
        // if (targetData == null) yield break;

        // ClientUnit targetUnit = scene.GetSceneUnitById(targetData.Id);
        // if (targetUnit == null) yield break;

        sceneUnit.isResolvingAnimation = true;
        // targetUnit.isResolvingAnimation = true;

        Vector3 startPos = sceneUnit.transform.position;
        Vector3 targetPos = scene.movableTilemap.GetCellCenterWorld(result.TargetCells[0]);
        float stopDistance = 0.6f; // world units before target
        Vector3 dir = (targetPos - startPos).normalized;
        Vector3 midPos = targetPos - dir * stopDistance;

        float valX = 0f, valY = 0f;
        if (Mathf.Abs(dir.x) >= Mathf.Abs(dir.y))
            valX = dir.x > 0 ? 1f : -1f;
        else
            valY = dir.y > 0 ? 1f : -1f;

        if (sceneUnit.VFXAnimator != null)
        {
            sceneUnit.VFXAnimator.SetFloat("ValX", valX);
            sceneUnit.VFXAnimator.SetFloat("ValY", valY);
            sceneUnit.VFXAnimator.SetBool("IsAttacking", true);
            sceneUnit.VFXAnimator.SetBool("IsIdling", false);
        }

        float half = resolveDuration * 0.5f;

        // Move toward target
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / half;
            sceneUnit.transform.position = Vector3.Lerp(startPos, midPos, t);
            yield return null;
        }

        // Move back
        t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / half;
            sceneUnit.transform.position = Vector3.Lerp(midPos, startPos, t);
            yield return null;
        }

        sceneUnit.transform.position = startPos;

        if (sceneUnit.VFXAnimator != null)
        {
            sceneUnit.VFXAnimator.SetBool("IsAttacking", false);
            sceneUnit.VFXAnimator.SetBool("IsIdling", true);
        }

        sceneUnit.isResolvingAnimation = false;
        // targetUnit.isResolvingAnimation = false;
    }
}