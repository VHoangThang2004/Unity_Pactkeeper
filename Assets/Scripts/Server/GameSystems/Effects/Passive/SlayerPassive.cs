// Server/GameSystems/Effects/Passive/SlayerPassive.cs
using UnityEngine;

/// <summary>
/// Slayer unique passive (permanent): after taking damage, gain 1 Hot Blooded and 1 Anger.
/// When Anger reaches its Max Stacks (2), all Anger is converted into Fury (DamageMultiplierBuff ×2, permanent, 1 stack max).
/// </summary>
[CreateAssetMenu(fileName = "SlayerPassive", menuName = "SRPG/Effects/Server/Passive/SlayerPassive")]
public class SlayerPassive : ServerPassiveEffectBase
{
    public override EffectType EffectType => EffectType.Passive;

    [Header("Slayer")]
    [Tooltip("HotBloodedBuff — lifesteal, duration 1")]
    public int hotBloodedEffectId = 20019;
    [Tooltip("MarkerStatus — permanent, Max Stacks 2")]
    public int angerEffectId = 20020;
    [Tooltip("DamageMultiplierBuff — permanent, Max Stacks 1, 200%")]
    public int furyEffectId = 20021;

    public override void OnPostDamageReceived(ServerMatchSession session, UnitData owner, ActiveEffectInstance inst, in DamageContext ctx)
    {
        Grant(session, owner, hotBloodedEffectId);

        var anger = Grant(session, owner, angerEffectId);
        if (anger == null || anger.stacks < anger.effect.maxStacks) return;
        session.RemoveEffect(owner.Id, anger);
        Grant(session, owner, furyEffectId); // already furious → nothing changes (1 stack max, permanent)
    }

    static ActiveEffectInstance Grant(ServerMatchSession session, UnitData owner, int effectId)
    {
        if (session.effectRegistry.Get(effectId) is ServerPassiveEffectBase e) return session.AddEffect(owner.Id, e);
        Debug.LogWarning($"[SlayerPassive] Effect {effectId} is not a passive effect — not granted.");
        return null;
    }

    public override ResolveResult Apply(ServerMatchSession session, int sourceUnitId, Vector3Int target)
        => new ResolveResult { EffectId = -1 }; // passive — never directly applied
}
