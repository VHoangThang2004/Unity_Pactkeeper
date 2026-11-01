// Server/GameSystems/Effects/Passive/DruidPassive.cs
using UnityEngine;

/// <summary>
/// Druid unique passive (permanent): after the Druid uses its CLASS skill, it gains a status
/// (a WeaponAoeStatus asset, referenced by effect id) that turns its next weapon attack into an all-enemies-in-reach AoE.
/// </summary>
[CreateAssetMenu(fileName = "DruidPassive", menuName = "SRPG/Effects/Server/Passive/DruidPassive")]
public class DruidPassive : ServerPassiveEffectBase
{
    public override EffectType EffectType => EffectType.Passive;

    [Header("Druid")]
    [Tooltip("Effect id of the status granted after the class skill (WeaponAoeStatus)")]
    public int afterClassSkillStatusEffectId = 20017;

    public override void OnCommand(ServerMatchSession session, UnitData owner, ActiveEffectInstance inst, in CommandContext cmd)
    {
        if (cmd.Slot != SkillSlot.Class) return;

        if (session.effectRegistry.Get(afterClassSkillStatusEffectId) is ServerPassiveEffectBase status)
            session.AddEffect(owner.Id, status);
        else
            Debug.LogWarning($"[DruidPassive] Effect {afterClassSkillStatusEffectId} is not a passive effect — status not granted.");
    }

    public override ResolveResult Apply(ServerMatchSession session, int sourceUnitId, Vector3Int target)
        => new ResolveResult { EffectId = -1 }; // passive — never directly applied
}
