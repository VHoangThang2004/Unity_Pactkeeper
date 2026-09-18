using System.Collections;
using UnityEngine;

[CreateAssetMenu(fileName = "NormalHealEffectVisual", menuName = "SRPG/Effects/Client/NormalHeal")]
public class NormalHealEffectVisual : ClientActiveEffectBase
{
    public override InstantType InstantType => InstantType.HalfInstant;
    public override string GetDescription(UnitData unit)
    {
        // UnitData unit = session.GetUnitDataById(unitId);
        return " Recover " + baseValue + " HP to selected ally target.";
    }

    public override IEnumerator Replay(
        ResolveResult result,
        ClientScene scene,
        ClientMatchSession session)
    {

        ClientUnit sceneUnit = scene.GetSceneUnitById(result.SourceUnitId);
        if (sceneUnit == null) yield break;
        if (result.TargetCells == null || result.TargetCells.Count == 0)
            yield break;

        UnitData targetData = session.GetUnitDataAt(result.TargetCells[0]);
        if (targetData != null)
        {
            ClientUnit targetUnit = scene.GetSceneUnitById(targetData.Id);
            targetUnit.VFXAnimator.SetBool("IsIdling", false);
            targetUnit.VFXAnimator.Play("BeingHealedVFX", 0, 0f);
        }

        // ClientUnit targetUnit = scene.GetSceneUnitById(targetData.Id);
        // if (targetUnit == null) yield break;

        sceneUnit.isResolvingAnimation = true;
        // targetUnit.isResolvingAnimation = true;

        Vector3 startPos = sceneUnit.transform.position;
        Vector3 targetPos = scene.movableTilemap.GetCellCenterWorld(result.TargetCells[0]);
        Vector3 dir = (targetPos - startPos).normalized;

        float valX = 0f, valY = 0f;
        if (Mathf.Abs(dir.x) >= Mathf.Abs(dir.y))
            valX = dir.x > 0 ? 1f : -1f;
        else
            valY = dir.y > 0 ? 1f : -1f;

        Vector3 bobPos = startPos + new Vector3(valX, valY, 0f) * 0.2f; // small bob in attack direction


        if (sceneUnit.VFXAnimator != null)
        {
            sceneUnit.VFXAnimator.SetBool("IsIdling", false);
            sceneUnit.VFXAnimator.Play("SpellSourceVFX", 0, 0f);

        }

        float half = resolveDuration * 0.5f;

        // Move toward target
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / half;
            sceneUnit.transform.position = Vector3.Lerp(startPos, bobPos, t);
            yield return null;
        }

        // Move back
        t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / half;
            sceneUnit.transform.position = Vector3.Lerp(bobPos, startPos, t);
            yield return null;
        }
        if (targetData != null)
        {
            ClientUnit targetUnit = scene.GetSceneUnitById(targetData.Id);
            targetUnit.VFXAnimator.SetBool("IsIdling", true);
            targetUnit.VFXAnimator.Play("NoEffect", 0, 0f);
        }


        sceneUnit.transform.position = startPos;

        if (sceneUnit.VFXAnimator != null)
        {
            sceneUnit.VFXAnimator.SetBool("IsIdling", true);
            sceneUnit.VFXAnimator.Play("NoEffect", 0, 0f);
        }

        sceneUnit.isResolvingAnimation = false;
        // yield return new WaitForSeconds(2f);
    }
}