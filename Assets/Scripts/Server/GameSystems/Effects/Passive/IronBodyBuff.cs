// Server/GameSystems/Effects/Passive/IronBodyBuff.cs
using UnityEngine;

/// <summary>
/// "Iron Body" status (granted by TankerPassive at the end of an instant the Tank was commanded in):
/// incoming hits are capped at `damageCap`; each hit taken removes one stack, and at 0 stacks the status is removed.
/// Asset: Max Stacks 1, Is Permanent ON (lasts until consumed).
/// </summary>
[CreateAssetMenu(fileName = "IronBodyBuff", menuName = "SRPG/Effects/Server/Passive/IronBodyBuff")]
public class IronBodyBuff : ServerPassiveEffectBase
{
    public override EffectType EffectType => EffectType.Passive;

    [Header("Iron Body")]
    public int damageCap = 1;

    public override void OnPreDamageReceived(ServerMatchSession session, UnitData owner, ActiveEffectInstance inst, ref DamageContext ctx)
        => ctx.Cap = Mathf.Min(ctx.Cap, damageCap); // read-only: Compute must stay side-effect free

    public override void OnPostDamageReceived(ServerMatchSession session, UnitData owner, ActiveEffectInstance inst, in DamageContext ctx)
    {
        inst.stacks--;
        if (inst.stacks <= 0) session.RemoveEffect(owner.Id, inst);
    }

    public override ResolveResult Apply(ServerMatchSession session, int sourceUnitId, Vector3Int target)
        => new ResolveResult { EffectId = -1 }; // passive — never directly applied
}
