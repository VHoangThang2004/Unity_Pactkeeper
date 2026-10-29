// Server/GameSystems/Effects/Passive/DamageMultiplierBuff.cs
using UnityEngine;

/// <summary>
/// Status: the owner's attacks deal `multiplierPercent`% damage PER STACK (200 = ×2, 3 stacks = ×8), applied as a
/// pipeline multiplier (multiplies with other multipliers). Not consumed by attacking — it lasts for its duration.
/// Re-granting adds a stack (up to Max Stacks) and refreshes the duration; all stacks share that duration.
/// Warrior's "after moving" buff: Duration Instants 1 → every stack expires at the next instant start, used or not.
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

    public override ResolveResult Apply(ServerMatchSession session, int sourceUnitId, Vector3Int target)
        => new ResolveResult { EffectId = -1 }; // passive — never directly applied
}
