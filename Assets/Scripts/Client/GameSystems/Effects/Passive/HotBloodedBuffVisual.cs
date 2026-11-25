using System.Collections;
using UnityEngine;

/// <summary>
/// Hot Blooded status (20019). Replays the server-emitted heal result: heal VFX on the owner,
/// plus Lightning at the same time when result.Value (overheal amount) > 0.
/// </summary>
[CreateAssetMenu(fileName = "HotBloodedBuffVisual", menuName = "SRPG/Effects/Client/HotBloodedBuff")]
public class HotBloodedBuffVisual : StatusEffectVisual
{
    public override IEnumerator Replay(ResolveResult result, ClientScene scene, ClientMatchSession session)
    {
        ClientUnit unit = scene.GetSceneUnitById(result.SourceUnitId);
        Animator anim = unit != null ? unit.VFXAnimator : null;
        if (anim == null) yield break;

        anim.SetBool("IsIdling", false);
        anim.Play("BeingHealedVFX", 0, 0f);

        GameObject thunder = null;
        if (result.Value > 0)
        {
            thunder = Object.Instantiate(anim.gameObject, anim.transform.parent); // one animator plays one state — clone for the second
            var tAnim = thunder.GetComponent<Animator>();
            tAnim.speed = anim.speed;
            tAnim.SetBool("IsIdling", false);
            tAnim.Play("LightningVFX", 0, 0f);
        }

        yield return new WaitForSeconds(UnitAnimationSpeed.Duration(resolveDuration));

        if (thunder != null) Object.Destroy(thunder);
        anim.SetBool("IsIdling", true);
        anim.Play("NoEffect", 0, 0f);
    }
}
