// Server/GameSystems/Effects/PassiveHooks.cs
using System.Collections.Generic;
using UnityEngine;

// What an accepted command did — passed to ServerPassiveEffectBase.OnCommand
public struct CommandContext
{
    public SkillDefinition Skill;
    public SkillSlot Slot;
    public bool IsAttack;        // skill has a DamageEffectServerBase effect
    public Vector3Int SourceCell; // unit's cell when the command was given
    public Vector3Int TargetCell; // aimed cell
}

/// <summary>
/// Raises passive hooks on every effect a unit carries. Iterates a copy, so a hook may add/remove effects.
/// </summary>
public static class PassiveHooks
{
    static ActiveEffectInstance[] On(ServerMatchSession s, UnitData unit) => s.GetUnitActiveEffects(unit.Id).ToArray();

    public static void InstantStart(ServerMatchSession s)
    {
        foreach (var unit in s.units.ToArray())
            foreach (var inst in On(s, unit))
                inst.effect.OnInstantStart(s, unit, inst);
    }

    public static void Command(ServerMatchSession s, UnitData unit, in CommandContext cmd)
    {
        foreach (var inst in On(s, unit))
            inst.effect.OnCommand(s, unit, inst, cmd);
    }

    public static void PreDamage(ServerMatchSession s, ref DamageContext ctx)
    {
        foreach (var inst in On(s, ctx.Source))
            inst.effect.OnPreDamageDealt(s, ctx.Source, inst, ref ctx);
        foreach (var inst in On(s, ctx.Target))
            inst.effect.OnPreDamageReceived(s, ctx.Target, inst, ref ctx);
    }

    public static void PostDamage(ServerMatchSession s, in DamageContext ctx)
    {
        foreach (var inst in On(s, ctx.Source))
            inst.effect.OnPostDamageDealt(s, ctx.Source, inst, ctx);
        foreach (var inst in On(s, ctx.Target))
            inst.effect.OnPostDamageReceived(s, ctx.Target, inst, ctx);
    }

    public static void ModifyAttackTargets(ServerMatchSession s, UnitData attacker, DamageEffectServerBase effect, List<Vector3Int> targets)
    {
        foreach (var inst in On(s, attacker))
            inst.effect.ModifyAttackTargets(s, attacker, inst, effect, targets);
    }

    public static void AttackResolved(ServerMatchSession s, UnitData attacker, DamageEffectServerBase effect)
    {
        foreach (var inst in On(s, attacker))
            inst.effect.OnAttackResolved(s, attacker, inst, effect);
    }

    public static void HealDone(ServerMatchSession s, in HealContext ctx)
    {
        foreach (var inst in On(s, ctx.Source))
            inst.effect.OnHealDone(s, ctx.Source, inst, ctx);
    }
}
