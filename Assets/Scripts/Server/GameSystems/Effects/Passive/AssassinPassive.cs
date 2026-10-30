// Server/GameSystems/Effects/Passive/AssassinPassive.cs
using UnityEngine;

/// <summary>
/// Assassin unique passive (permanent): when the Assassin gives an attack command aimed at a cell BELOW itself
/// (target y lower than its own y, any x), it gains a status (a DamageMultiplierBuff asset, referenced by effect id).
/// The attack is NonInstant, so the buff is already up when it lands at the end of the instant; a miss simply wastes it.
/// The aimed offset is kept at resolve (OffsetRecalculator), so "below" still holds when the hit lands.
/// </summary>
[CreateAssetMenu(fileName = "AssassinPassive", menuName = "SRPG/Effects/Server/Passive/AssassinPassive")]
public class AssassinPassive : ServerPassiveEffectBase
{
    public override EffectType EffectType => EffectType.Passive;

    [Header("Assassin")]
    [Tooltip("Effect id of the status granted when attacking downward (DamageMultiplierBuff)")]
    public int attackBelowBuffEffectId = 20012;

    public override void OnCommand(ServerMatchSession session, UnitData owner, ActiveEffectInstance inst, in CommandContext cmd)
    {
        if (!cmd.IsAttack || cmd.TargetCell.y >= cmd.SourceCell.y) return;

        if (session.effectRegistry.Get(attackBelowBuffEffectId) is ServerPassiveEffectBase buff)
            session.AddEffect(owner.Id, buff);
        else
            Debug.LogWarning($"[AssassinPassive] Effect {attackBelowBuffEffectId} is not a passive effect — buff not granted.");
    }

    public override ResolveResult Apply(ServerMatchSession session, int sourceUnitId, Vector3Int target)
        => new ResolveResult { EffectId = -1 }; // passive — never directly applied
}
