using System.Collections;
using UnityEngine;

[CreateAssetMenu(fileName = "VerticalReachBuffVisual", menuName = "SRPG/Effects/Client/VerticalReachBuff")]
public class VerticalReachBuffVisual : ClientActiveEffectBase
{
    public override InstantType InstantType => InstantType.PassiveBuff;
    public override string GetDescription(UnitData unit, ClientEffectRegistry effectRegistry) => "Extends <u>range</u> on the furthest <u>vertical</u> cells for 10 instants.";

    public override IEnumerator Replay(ResolveResult result, ClientScene scene, ClientMatchSession session)
    {
        yield break;
    }
}