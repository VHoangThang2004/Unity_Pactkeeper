// Server/GameSystems/Effects/Passive/HotBloodedBuff.cs
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Status: after the owner deals damage, it recovers HP equal to the damage dealt. Can overheal:
/// HP above MaxHP raises MaxHP permanently (BaseMaxHP too, so UnitRecalculator keeps it).
/// Emits its own result so the client plays the heal (and thunder on overheal) — Value = overheal amount.
/// </summary>
[CreateAssetMenu(fileName = "HotBloodedBuff", menuName = "SRPG/Effects/Server/Passive/HotBloodedBuff")]
public class HotBloodedBuff : ServerPassiveEffectBase
{
    public override EffectType EffectType => EffectType.Passive;

    public override void OnPostDamageDealt(ServerMatchSession session, UnitData owner, ActiveEffectInstance inst, in DamageContext ctx)
    {
        int heal = ctx.Final; // `in` params can't be captured
        // Heal as its own step after the attack result, so that result still shows the attacker un-healed
        session.Trigger(() =>
        {
            if (session.GetUnitByUnitId(owner.Id) == null || owner.CurrentHP <= 0)
                return new ResolveResult { EffectId = -1 }; // died before it resolved — dead attackers don't heal

            owner.CurrentHP += heal;
            int over = Mathf.Max(0, owner.CurrentHP - owner.MaxHP);
            owner.MaxHP += over;
            owner.BaseMaxHP += over;

            return new ResolveResult
            {
                EffectId = effectId,
                SourceUnitId = owner.Id,
                SourceCell = owner.CurrentCell,
                TargetCells = new List<Vector3Int> { owner.CurrentCell },
                Partial = SessionSnapshotData.Partial(new List<UnitData> { owner }),
                Value = over,
            };
        });
    }

    public override ResolveResult Apply(ServerMatchSession session, int sourceUnitId, Vector3Int target)
        => new ResolveResult { EffectId = -1 }; // passive — never directly applied
}
