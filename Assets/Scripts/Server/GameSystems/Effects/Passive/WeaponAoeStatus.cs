// Server/GameSystems/Effects/Passive/WeaponAoeStatus.cs
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Status: the owner's next WEAPON attack hits EVERY cell within the weapon's reach (its current target pattern,
/// "selectable" flag ignored) except cells holding allies — empty cells included (impact VFX), enemies take damage.
/// Aimed cell first. Consumed (Stacks Lost On Trigger; asset: Is Permanent ON, -1) only if an enemy was in reach.
/// Targeting/network unchanged: the player aims any cell as usual; the server expands the hits at resolve.
/// </summary>
[CreateAssetMenu(fileName = "WeaponAoeStatus", menuName = "SRPG/Effects/Server/Passive/WeaponAoeStatus")]
public class WeaponAoeStatus : ServerPassiveEffectBase
{
    public override EffectType EffectType => EffectType.Passive;

    public override void ModifyAttackTargets(ServerMatchSession session, UnitData owner, ActiveEffectInstance inst,
                                             DamageEffectServerBase effect, List<Vector3Int> targets)
    {
        if (!IsWeaponEffect(session, owner, effect)) return;

        // Every cell in reach except allied ones: empty cells are "hit" too (client plays an impact there), enemies take damage
        int ownerTeam = session.GetTeamIdByUnitId(owner.Id);
        bool IsAlly(Vector3Int cell)
        {
            var unit = session.GetUnitAt(cell);
            return unit != null && session.GetTeamIdByUnitId(unit.Id) == ownerTeam;
        }

        var reach = new List<Vector3Int>();
        bool anyEnemy = false;
        foreach (var p in owner.SkillPatterns)
        {
            if (p.SkillId != owner.WeaponSkillId || p.TargetCells == null) continue;
            foreach (var c in p.TargetCells)
            {
                var cell = new Vector3Int(c.x, c.y, 0);
                if (IsAlly(cell) || reach.Contains(cell)) continue;
                reach.Add(cell);
                anyEnemy |= session.GetUnitAt(cell) != null;
            }
        }
        if (reach.Count == 0) return;

        // Aimed cell first (camera focus) unless it holds an ally, then the rest of the reach
        Vector3Int aimed = targets.Count > 0 ? targets[0] : reach[0];
        targets.Clear();
        if (reach.Remove(aimed)) targets.Add(aimed);
        targets.AddRange(reach);

        // Consumed only if it actually reached an enemy; a sweep over empty cells keeps the status
        inst.primed = anyEnemy;
    }

    public override void OnAttackResolved(ServerMatchSession session, UnitData owner, ActiveEffectInstance inst, DamageEffectServerBase effect)
    {
        if (!inst.primed) return;
        inst.primed = false;
        ConsumeOnTrigger(session, owner, inst);
    }

    static bool IsWeaponEffect(ServerMatchSession session, UnitData owner, DamageEffectServerBase effect)
    {
        var weapon = session.skillLibrary.Get(owner.WeaponSkillId);
        return weapon?.effectIds != null && System.Array.IndexOf(weapon.effectIds, effect.effectId) >= 0;
    }

    public override ResolveResult Apply(ServerMatchSession session, int sourceUnitId, Vector3Int target)
        => new ResolveResult { EffectId = -1 }; // passive — never directly applied
}
