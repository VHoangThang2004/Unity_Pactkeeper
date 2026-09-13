using System;
using System.Collections;
using UnityEngine;

[CreateAssetMenu(fileName = "NormalProjectileAttackEffectVisual", menuName = "SRPG/Effects/Client/NormalProjectileAttack")]
public class NormalProjectileAttackEffectVisual : ClientActiveEffectBase
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

        Vector3 startPos = sceneUnit.VFXSprite.transform.position;
        Vector3 targetPos = scene.movableTilemap.GetCellCenterWorld(result.TargetCells[0]);


        sceneUnit.VFXSprite.transform.position = startPos;
        sceneUnit.VFXAnimator.SetBool("IsIdling", false);
        sceneUnit.VFXAnimator.Play("ArrowSourceVFX", 0, 0f);

        yield return null;
        float animLength = sceneUnit.VFXAnimator.GetCurrentAnimatorStateInfo(0).length;
        yield return new WaitForSeconds(animLength);

        yield return new WaitForSeconds(MathF.Max(resolveDuration - animLength * 2, 0));

        sceneUnit.VFXSprite.transform.position = targetPos;
        sceneUnit.VFXAnimator.SetBool("IsIdling", false);
        sceneUnit.VFXAnimator.Play("ArrowTargetVFX", 0, 0f);
        yield return null; 
        float targetAnimLength = sceneUnit.VFXAnimator.GetCurrentAnimatorStateInfo(0).length;
        yield return new WaitForSeconds(targetAnimLength);

        sceneUnit.VFXAnimator.SetBool("IsIdling", true);
        sceneUnit.VFXAnimator.Play("NoEffect", 0, 0f);
        sceneUnit.VFXSprite.transform.position = startPos;
    }
}