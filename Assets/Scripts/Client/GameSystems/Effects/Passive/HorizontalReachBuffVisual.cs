using System.Collections;
using UnityEngine;

[CreateAssetMenu(fileName = "HorizontalReachBuffVisual", menuName = "SRPG/Effects/Client/HorizontalReachBuff")]
public class HorizontalReachBuffVisual : ClientActiveEffectBase
{
    public override InstantType InstantType => InstantType.PassiveBuff;
    public override string GetDescription(UnitData unit, ClientEffectRegistry effectRegistry) => "Extends <u>range</u> on the furthest <u>horizontal</u> cells for 10 instants.";

    public override IEnumerator Replay(ResolveResult result, ClientScene scene, ClientMatchSession session)
    {
        yield break;
    }
}