// Server/GameSystems/Effects/Passive/WarriorPassive.cs
using UnityEngine;

/// <summary>
/// Warrior unique passive (permanent): after the Warrior uses its own movement skill, it gains a status
/// (DamageMultiplierBuff, referenced by effect id). Forced moves (push/pull/guard swap) are not commands, so they don't count.
/// The move resolves immediately; the attack (NonInstant) lands at the end of the same instant with the buff.
/// </summary>
[CreateAssetMenu(fileName = "WarriorPassive", menuName = "SRPG/Effects/Server/Passive/WarriorPassive")]
public class WarriorPassive : ServerPassiveEffectBase
{
    public override EffectType EffectType => EffectType.Passive;

    [Header("Warrior")]
    [Tooltip("Effect id of the status granted after moving (DamageMultiplierBuff)")]
    public int afterMoveBuffEffectId = 20010;

    public override void OnCommand(ServerMatchSession session, UnitData owner, ActiveEffectInstance inst, in CommandContext cmd)
    {
        if (cmd.Slot != SkillSlot.Movement) return;

        if (session.effectRegistry.Get(afterMoveBuffEffectId) is ServerPassiveEffectBase buff)
            session.AddEffect(owner.Id, buff); // already up → duration refreshed, still 1 stack
        else
            Debug.LogWarning($"[WarriorPassive] Effect {afterMoveBuffEffectId} is not a passive effect — buff not granted.");
    }

    public override ResolveResult Apply(ServerMatchSession session, int sourceUnitId, Vector3Int target)
        => new ResolveResult { EffectId = -1 }; // passive — never directly applied
}
