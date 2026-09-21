using System.Collections;
using UnityEngine;

[CreateAssetMenu(fileName = "MovementVerticalReachBuffVisual", menuName = "SRPG/Effects/Client/MovementVerticalReachBuff")]
public class MovementVerticalReachBuffVisual : ClientActiveEffectBase
{
    public override InstantType InstantType => InstantType.PassiveBuff;
    public override string GetDescription(UnitData unit, ClientEffectRegistry effectRegistry) => "Extends <u>Movement range</u> on the furthest <u>vertical</u> cells for 10 instants.";

    public override IEnumerator Replay(ResolveResult result, ClientScene scene, ClientMatchSession session)
    {
        yield break;
    }
}