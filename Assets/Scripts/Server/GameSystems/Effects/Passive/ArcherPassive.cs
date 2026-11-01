// Server/GameSystems/Effects/Passive/ArcherPassive.cs
using UnityEngine;

/// <summary>
/// Archer unique passive (permanent): every resolved Archer attack (hit, miss or AoE = 1) adds a stack to a
/// permanent "charge" status. When the charge reaches `every`, it is converted: charge removed, the Archer gains
/// the empowered status (a DamageMultiplierBuff asset, 1 stack) — which boosts the NEXT shot.
/// Cycle with every = 3: shots 1–3 build charge → convert → shot 4 boosted (and counts as charge 1) → shot 7 boosted…
/// Counting happens after each attack resolves, so the converting shot itself is never boosted.
/// </summary>
[CreateAssetMenu(fileName = "ArcherPassive", menuName = "SRPG/Effects/Server/Passive/ArcherPassive")]
public class ArcherPassive : ServerPassiveEffectBase
{
    public override EffectType EffectType => EffectType.Passive;

    [Header("Archer")]
    [Tooltip("Charge stacks needed to convert into the empowered status")]
    [Min(1)] public int every = 3;
    [Tooltip("Effect id of the charge counter status (MarkerStatus, permanent); its stacks show the count")]
    public int chargeEffectId = 20014;
    [Tooltip("Effect id of the empowered status granted on conversion (DamageMultiplierBuff, permanent, consumed on trigger)")]
    public int empoweredBuffEffectId = 20015;

    // After the attack resolved — the empowered buff (if any) has already boosted this shot
    public override void OnAttackResolved(ServerMatchSession session, UnitData owner, ActiveEffectInstance inst, DamageEffectServerBase effect)
    {
        inst.counter++;
        var charge = session.GetUnitActiveEffects(owner.Id).Find(e => e.effect.effectId == chargeEffectId);

        if (inst.counter < every)
        {
            if (session.effectRegistry.Get(chargeEffectId) is ServerPassiveEffectBase chargeEffect)
                session.AddEffect(owner.Id, chargeEffect).stacks = inst.counter; // icon shows the count (1 = no number)
            return;
        }

        // Max charge → convert all charge stacks into one empowered stack
        inst.counter = 0;
        if (charge != null) session.RemoveEffect(owner.Id, charge);

        if (session.effectRegistry.Get(empoweredBuffEffectId) is ServerPassiveEffectBase buff)
            session.AddEffect(owner.Id, buff);
        else
            Debug.LogWarning($"[ArcherPassive] Effect {empoweredBuffEffectId} is not a passive effect — empowered shot not granted.");
    }

    public override ResolveResult Apply(ServerMatchSession session, int sourceUnitId, Vector3Int target)
        => new ResolveResult { EffectId = -1 }; // passive — never directly applied
}
