using System.Collections.Generic;
using UnityEngine;

public abstract class ServerPassiveEffectBase : EffectBase
{
    public override InstantType InstantType => InstantType.PassiveBuff;
    public abstract EffectType EffectType { get; }

    [Header("Duration")]
    public bool isPermanent = false;
    public int durationInstants = 1;

    [Header("Stacking")]
    [Min(1)] public int maxStacks = 1;
    [Tooltip("Stacks lost at every instant start (same clock as duration). 0 = never, X = lose X, -1 = lose all. " +
             "The status is removed when its stacks reach 0 or its duration runs out, whichever comes first.")]
    [Min(-1)] public int stacksLostPerInstant = 0;
    [Tooltip("Stacks lost each time this status takes effect (what counts as 'taking effect' is defined by the status). " +
             "0 = never, X = lose X, -1 = lose all. At 0 stacks the status is removed.")]
    [Min(-1)] public int stacksLostOnTrigger = 0;

    // Call when this status has just taken effect: applies stacksLostOnTrigger, removes the status at 0 stacks
    protected void ConsumeOnTrigger(ServerMatchSession session, UnitData owner, ActiveEffectInstance inst)
    {
        if (stacksLostOnTrigger == 0) return;
        inst.stacks = stacksLostOnTrigger < 0 ? 0 : inst.stacks - stacksLostOnTrigger;
        if (inst.stacks <= 0) session.RemoveEffect(owner.Id, inst);
    }

    // Modifies unit derived stats — called by UnitRecalculator
    public virtual void ApplyStatModifier(UnitData unit) { }

    // Modifies patterns
    public virtual void ApplyPatternModifier(UnitData unit, ServerMatchSession session) { }

    // -------------------------------------------------------
    // Event hooks — raised by PassiveHooks for every effect on `owner`.
    // Keep per-unit state on `inst` (stacks/counter/primed), never on this SO.
    // -------------------------------------------------------

    // Start of every instant, after durations tick
    public virtual void OnInstantStart(ServerMatchSession session, UnitData owner, ActiveEffectInstance inst) { }

    // End of a paused instant: after all commands, the NonInstant resolve batch and step costs
    public virtual void OnInstantEnd(ServerMatchSession session, UnitData owner, ActiveEffectInstance inst) { }

    // Owner's command was accepted (Post command timing). NonInstant damage from it resolves later.
    public virtual void OnCommand(ServerMatchSession session, UnitData owner, ActiveEffectInstance inst, in CommandContext cmd) { }

    // Damage being computed — owner is the attacker / the target. Must not change game state (Compute is preview-safe).
    public virtual void OnPreDamageDealt(ServerMatchSession session, UnitData owner, ActiveEffectInstance inst, ref DamageContext ctx) { }
    public virtual void OnPreDamageReceived(ServerMatchSession session, UnitData owner, ActiveEffectInstance inst, ref DamageContext ctx) { }

    // Damage applied (HP already reduced, kill not yet processed)
    public virtual void OnPostDamageDealt(ServerMatchSession session, UnitData owner, ActiveEffectInstance inst, in DamageContext ctx) { }
    public virtual void OnPostDamageReceived(ServerMatchSession session, UnitData owner, ActiveEffectInstance inst, in DamageContext ctx) { }

    // Owner's attack is choosing its cells (AoE, retargeting); edit `targets` in place
    public virtual void ModifyAttackTargets(ServerMatchSession session, UnitData owner, ActiveEffectInstance inst, DamageEffectServerBase effect, List<Vector3Int> targets) { }

    // Owner's attack finished resolving — hit, whiff or all-ally
    public virtual void OnAttackResolved(ServerMatchSession session, UnitData owner, ActiveEffectInstance inst, DamageEffectServerBase effect) { }

    // Owner healed someone (ctx.Restored = HP actually gained)
    public virtual void OnHealDone(ServerMatchSession session, UnitData owner, ActiveEffectInstance inst, in HealContext ctx) { }
    // Returns 1 ResolveResult so client can sync unit data
    public abstract ResolveResult Apply(
        ServerMatchSession session,
        int sourceUnitId,
        Vector3Int target);
}