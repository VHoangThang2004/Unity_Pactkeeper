using System;
using System.Collections;
using UnityEngine;

[CreateAssetMenu(fileName = "NormalProjectileAttackEffectVisual", menuName = "SRPG/Effects/Client/NormalProjectileAttack")]
public class NormalProjectileAttackEffectVisual : ClientActiveEffectBase
{
    public override InstantType InstantType => InstantType.NonInstant;
    public string sourceVFXname;
    public string targetVFXname;
    public override string GetDescription(UnitData unit, ClientEffectRegistry effectRegistry)
    {
        return " Deal " + Mathf.RoundToInt(baseValue * unit.DamageMultiplier / 100) + " damage to selected enemy target.";
    }

    public override IEnumerator Replay(
        ResolveResult result,
        ClientScene scene,
        ClientMatchSession session)
    {

        ClientUnit sceneUnit = scene.GetSceneUnitById(result.SourceUnitId);
        if (sceneUnit == null) yield break;
        scene.cameraController.CenterOn(sceneUnit.transform.position);

        if (result.TargetCells == null || result.TargetCells.Count == 0)
            yield break;

        Vector3 startPos = sceneUnit.VFXSprite.transform.position;
        Vector3 targetPos = scene.movableTilemap.GetCellCenterWorld(result.TargetCells[0]);

        Vector3 midPoint = (startPos + targetPos) / 2f;
        scene.cameraController.CenterOn(midPoint);


        sceneUnit.VFXSprite.transform.position = startPos;
        sceneUnit.VFXAnimator.SetBool("IsIdling", false);
        sceneUnit.VFXAnimator.Play(sourceVFXname, 0, 0f);

        yield return null;
        float animLength = sceneUnit.VFXAnimator.GetCurrentAnimatorStateInfo(0).length;
        yield return new WaitForSeconds(animLength);

        yield return new WaitForSeconds(MathF.Max(resolveDuration - animLength * 2, 0));

        sceneUnit.VFXSprite.transform.position = targetPos;
        sceneUnit.VFXAnimator.SetBool("IsIdling", false);
        sceneUnit.VFXAnimator.Play(targetVFXname, 0, 0f);
        yield return null;
        float targetAnimLength = sceneUnit.VFXAnimator.GetCurrentAnimatorStateInfo(0).length;
        yield return new WaitForSeconds(targetAnimLength);

        sceneUnit.VFXAnimator.SetBool("IsIdling", true);
        sceneUnit.VFXAnimator.Play("NoEffect", 0, 0f);
        sceneUnit.VFXSprite.transform.position = startPos;
    }
}