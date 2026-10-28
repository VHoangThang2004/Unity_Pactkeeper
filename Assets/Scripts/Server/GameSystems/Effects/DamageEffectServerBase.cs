// Server/GameSystems/Effects/DamageEffectServerBase.cs
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Base for every damaging effect. Game rule: damage is always NonInstant — aimed at a cell,
/// resolved at the end of the instant in speed order (sealed so no damage effect can break it).
/// Per target: DamagePipeline.Compute → Apply. AoE effects override SelectTargets.
/// </summary>
public abstract class DamageEffectServerBase : ServerActiveEffectBase
{
    public sealed override InstantType InstantType => InstantType.NonInstant;
    public sealed override EffectType EffectType => EffectType.Damage;

    // Cells this attack hits, already offset to the attacker's current position. Default: the aimed cell only.
    protected virtual List<Vector3Int> SelectTargets(ServerMatchSession session, UnitData source, Vector3Int targetCell)
        => new List<Vector3Int> { targetCell };

    public sealed override ResolveResult Apply(
        ServerMatchSession session,
        int sourceUnitId,
        Vector3Int sourceCell,
        Vector3Int targetCell)
    {
        UnitData unit = session.GetUnitByUnitId(sourceUnitId);
        if (unit == null)
        {
            // Attacker died earlier in this instant's batch — its queued attack is cancelled
            Debug.Log($"[{name}] Attacker {sourceUnitId} no longer exists — attack cancelled.");
            return new ResolveResult { EffectId = -1 };
        }

        Vector3Int fromCell = unit.CurrentCell;
        Vector3Int aimedCell = OffsetRecalculator.RecalculateNewTargetCell(sourceCell, targetCell, fromCell);
        List<Vector3Int> cells = SelectTargets(session, unit, aimedCell);
        PassiveHooks.ModifyAttackTargets(session, unit, this, cells);

        int sourceTeam = session.GetTeamIdByUnitId(sourceUnitId);
        var touched = new List<UnitData> { unit };
        bool anyEnemy = false, anyAlly = false, anyKilled = false;

        foreach (var cell in cells)
        {
            UnitData target = session.GetUnitAt(cell);
            if (target == null) continue;
            if (session.GetTeamIdByUnitId(target.Id) == sourceTeam) { anyAlly = true; continue; } // no friendly fire

            anyEnemy = true;
            var damage = DamagePipeline.Compute(session, unit, target, baseValue);
            if (DamagePipeline.Apply(session, ref damage)) anyKilled = true;
            else touched.Add(target);
        }

        PassiveHooks.AttackResolved(session, unit, this); // fires on hit, whiff and all-ally alike
        foreach (var u in touched) session.SyncEffectViews(u); // hooks may have changed stacks — send fresh icons with this result

        // Only allies in the hit area: same as before — the attack is dropped entirely
        if (!anyEnemy && anyAlly)
        {
            Debug.LogWarning("Cannot damage ally !");
            return new ResolveResult { EffectId = -1 };
        }

        if (!anyEnemy) Debug.LogWarning("Missing target!"); // whiff: enemy moved away

        return new ResolveResult
        {
            EffectId = effectId,
            SourceUnitId = sourceUnitId,
            SourceCell = fromCell,
            TargetCells = cells,
            Partial = anyKilled ? session.FullSnapshot() : SessionSnapshotData.Partial(touched),
        };
    }
}
