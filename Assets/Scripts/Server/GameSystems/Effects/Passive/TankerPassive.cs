// Server/GameSystems/Effects/Passive/TankerPassive.cs
using UnityEngine;

/// <summary>
/// Tank unique passive (permanent): if the Tank was commanded during a paused instant, it gains
/// "Iron Body" (IronBodyBuff, referenced by effect id) at the end of that instant — after all resolves.
/// Iron Body is unlimited-duration, 1 stack max, so re-granting while it's still up changes nothing.
/// </summary>
[CreateAssetMenu(fileName = "TankerPassive", menuName = "SRPG/Effects/Server/Passive/TankerPassive")]
public class TankerPassive : ServerPassiveEffectBase
{
    public override EffectType EffectType => EffectType.Passive;

    [Header("Tanker")]
    [Tooltip("Effect id of the Iron Body status (IronBodyBuff) granted at the end of a commanded instant")]
    public int ironBodyEffectId = 20008;

    // inst.primed = "this Tank was commanded during the current instant"
    public override void OnCommand(ServerMatchSession session, UnitData owner, ActiveEffectInstance inst, in CommandContext cmd)
        => inst.primed = true;

    public override void OnInstantEnd(ServerMatchSession session, UnitData owner, ActiveEffectInstance inst)
    {
        if (!inst.primed) return;
        inst.primed = false;

        if (session.effectRegistry.Get(ironBodyEffectId) is ServerPassiveEffectBase ironBody)
            session.AddEffect(owner.Id, ironBody);
        else
            Debug.LogWarning($"[TankerPassive] Effect {ironBodyEffectId} is not a passive effect — Iron Body not granted.");
    }

    public override ResolveResult Apply(ServerMatchSession session, int sourceUnitId, Vector3Int target)
        => new ResolveResult { EffectId = -1 }; // passive — never directly applied
}
