// Server/GameSystems/Effects/Passive/PhysicianPassive.cs
using UnityEngine;

/// <summary>
/// Physician unique passive: after a heal that actually restores HP, regain SP (capped at MaxSkillPoint).
/// The SP is granted at Post command timing — after the heal's SP cost is paid — so a Physician at full SP
/// doesn't lose the refund to the cap.
/// </summary>
[CreateAssetMenu(fileName = "PhysicianPassive", menuName = "SRPG/Effects/Server/Passive/PhysicianPassive")]
public class PhysicianPassive : ServerPassiveEffectBase
{
    public override EffectType EffectType => EffectType.Passive;

    [Header("Physician")]
    public int spPerHeal = 1;

    public override void OnHealDone(ServerMatchSession session, UnitData owner, ActiveEffectInstance inst, in HealContext ctx)
    {
        if (ctx.Restored > 0) inst.primed = true; // full-HP heal (Restored 0) doesn't count
    }

    public override void OnCommand(ServerMatchSession session, UnitData owner, ActiveEffectInstance inst, in CommandContext cmd)
    {
        if (!inst.primed) return;
        inst.primed = false;
        owner.CurrentSkillPoint = Mathf.Min(owner.CurrentSkillPoint + spPerHeal, owner.MaxSkillPoint);
    }

    public override ResolveResult Apply(ServerMatchSession session, int sourceUnitId, Vector3Int target)
        => new ResolveResult { EffectId = -1 }; // passive — never directly applied
}
