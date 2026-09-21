using System.Collections;
using UnityEngine;

[CreateAssetMenu(fileName = "ApplyBuffEffectVisual", menuName = "SRPG/Effects/Client/ApplyBuffEffect")]
public class ApplyBuffEffectVisual : ClientActiveEffectBase
{
    public override InstantType InstantType => InstantType.Instant;
    public int[] buffEffectIds;
    public override string GetDescription(UnitData unit, ClientEffectRegistry effectRegistry)
    {
        string description = "Apply buff(s) to the target:";
        foreach (int eId in buffEffectIds)
        {
            description += "\n +" + effectRegistry.Get(eId)?.GetDescription(unit, effectRegistry);
        }
        return description;
    }

    public override IEnumerator Replay(
    ResolveResult result,
    ClientScene scene,
    ClientMatchSession session)
    {
        if (result.TargetCells == null || result.TargetCells.Count == 0) yield break;

        ClientUnit sceneUnit = scene.GetSceneUnitById(session.GetUnitDataAt(result.TargetCells[0]).Id);
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