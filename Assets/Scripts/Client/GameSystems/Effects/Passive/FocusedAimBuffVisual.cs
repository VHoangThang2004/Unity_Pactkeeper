using System.Collections;
using UnityEngine;

[CreateAssetMenu(fileName = "FocusedAimBuffVisual", menuName = "SRPG/Effects/Client/FocusedAimBuff")]
public class FocusedAimBuffVisual : ClientActiveEffectBase
{
    public override InstantType InstantType => InstantType.PassiveBuff;
    public override string GetDescription(UnitData unit, ClientEffectRegistry effectRegistry) => "Extends range on the furthest cells for 10 instants.";

    public override IEnumerator Replay(ResolveResult result, ClientScene scene, ClientMatchSession session)
    {
        yield break;
    }
}