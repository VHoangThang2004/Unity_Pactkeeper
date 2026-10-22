using System.Collections;
using UnityEngine;

/// <summary>
/// Generic client side of a passive/status effect: no animation, description typed in the Inspector.
/// Use for new passives instead of writing a visual class per effect.
/// </summary>
[CreateAssetMenu(fileName = "StatusEffectVisual", menuName = "SRPG/Effects/Client/StatusEffect")]
public class StatusEffectVisual : ClientActiveEffectBase
{
    [TextArea(2, 5)] public string description;

    public override InstantType InstantType => InstantType.PassiveBuff;
    public override string GetDescription(UnitData unit, ClientEffectRegistry effectRegistry) => description;

    public override IEnumerator Replay(ResolveResult result, ClientScene scene, ClientMatchSession session)
    {
        yield break;
    }
}
