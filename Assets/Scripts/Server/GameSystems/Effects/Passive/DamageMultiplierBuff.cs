// Server/GameSystems/Effects/Passive/DamageMultiplierBuff.cs
using UnityEngine;

/// <summary>
/// Status: the owner's attacks deal `multiplierPercent`% damage PER STACK (200 = ×2, 3 stacks = ×8), applied as a
/// pipeline multiplier (multiplies with other multipliers). Not consumed by attacking — it lasts for its duration.
/// Re-granting adds a stack (up to Max Stacks) and refreshes the duration; all stacks share that duration.
/// Removal options (combine as needed): duration, Stacks Lost Per Instant, Stacks Lost On Trigger (after an attack it boosted).
/// e.g. Warrior: lost per instant. Archer empowered shot: permanent + Stacks Lost On Trigger -1 (gone after the boosted attack).
/// </summary>
[CreateAssetMenu(fileName = "DamageMultiplierBuff", menuName = "SRPG/Effects/Server/Passive/DamageMultiplierBuff")]
public class DamageMultiplierBuff : ServerPassiveEffectBase
{
    public override EffectType EffectType => EffectType.Passive;

    [Header("Damage Multiplier")]
    public int multiplierPercent = 200;

    public override void OnPreDamageDealt(ServerMatchSession session, UnitData owner, ActiveEffectInstance inst, ref DamageContext ctx)
    {
        for (int i = 0; i < inst.stacks; i++)
            ctx.Multiply(multiplierPercent);
    }

    // "Takes effect" = boosted a hit that landed. Consumed once the whole attack resolved, so every AoE target
    // gets the boost; a miss boosts nothing and leaves the buff up. Only matters if Stacks Lost On Trigger != 0.
    public override void OnPostDamageDealt(ServerMatchSession session, UnitData owner, ActiveEffectInstance inst, in DamageContext ctx)
        => inst.primed = true;

    public override void OnAttackResolved(ServerMatchSession session, UnitData owner, ActiveEffectInstance inst, DamageEffectServerBase effect)
    {
        if (!inst.primed) return;
        inst.primed = false;
        ConsumeOnTrigger(session, owner, inst);
    }

    public override ResolveResult Apply(ServerMatchSession session, int sourceUnitId, Vector3Int target)
        => new ResolveResult { EffectId = -1 }; // passive — never directly applied
}
