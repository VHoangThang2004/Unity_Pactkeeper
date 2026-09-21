using System.Collections;
using UnityEngine;

[CreateAssetMenu(fileName = "ClassSkillAssasinEffectClient", menuName = "SRPG/Effects/Client/ClassSkillAssasin")]
public class ClassSkillAssasinEffectClient : ClientActiveEffectBase
{
    public override InstantType InstantType => InstantType.Instant;
    public override string GetDescription(UnitData unit,ClientEffectRegistry effectRegistry)
    {
        return " reactive the movement skill.";
    }

    public override IEnumerator Replay(
        ResolveResult result,
        ClientScene scene,
        ClientMatchSession session)
    {
        if (result.TargetCells == null || result.TargetCells.Count == 0) yield break;

        ClientUnit sceneUnit = scene.GetSceneUnitById(result.SourceUnitId);
        scene.cameraController.CenterOn(sceneUnit.transform.position);

        if (sceneUnit == null) yield break;

        if (sceneUnit.VFXAnimator != null)
        {
            sceneUnit.VFXAnimator.SetBool("IsIdling", false);
            sceneUnit.VFXAnimator.Play("SpellSourceVFX", 0, 0f);

        }
        yield return new WaitForSeconds(resolveDuration);
        if (sceneUnit.VFXAnimator != null)
        {
            sceneUnit.VFXAnimator.SetBool("IsIdling", true);
            sceneUnit.VFXAnimator.Play("NoEffect", 0, 0f);

        }
    }
}