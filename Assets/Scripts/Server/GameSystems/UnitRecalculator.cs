using System.Collections.Generic;
using UnityEngine;

public static class UnitRecalculator
{
    private static void Recalculate(
        UnitData unit,
        List<ActiveEffectInstance> activeEffects,
        ServerMatchSession session)
    {
        // Reset to base stats from loadout
        unit.Speed = unit.BaseSpeed;
        unit.MaxHP = unit.BaseMaxHP;
        unit.MaxSkillPoint = unit.BaseMaxSkillPoint;
        unit.DamageMultiplier = unit.BaseDamageMultiplier;
        unit.DamageReduction = unit.BaseDamageReduction;

        // Apply passive stat modifiers
        foreach (var effect in activeEffects)
        {
            if (effect == null) continue;
            if (effect.effect is not ServerPassiveEffectBase passiveEffect) continue;
            passiveEffect.ApplyStatModifier(unit);
        }

        //Rebuild patterns from definitions and active effects
        InitSkillPatterns(unit, session);

        // Debug.Log($"[UnitRecalculator] Unit {unit.Id} recalculated — speed={unit.Speed} maxHP={unit.MaxHP} dmgMult={unit.DamageMultiplier} dmgReduce={unit.DamageReduction}");
    }
    private static void InitSkillPatterns(UnitData unit, ServerMatchSession session)
    {
        var skillIds = new List<int>
        {
            unit.MovementSkillId,
            unit.WeaponSkillId,
            unit.ClassSkillId,
            unit.TrinketSkillId
        };

        // Step 1 — translate raw offsets to world cells, store in TargetCells
        var patterns = new List<CurrentPatterns>();
        foreach (var skillId in skillIds)
        {
            if (skillId == -1) continue;
            var skill = session.skillLibrary.Get(skillId);
            if (skill == null) continue;

            var rawCells = new List<Vector3Int>();
            if (skill.targetPattern != null)
                foreach (var offset in skill.targetPattern.cells)
                    rawCells.Add(new Vector3Int(offset.x, offset.y, 0));

            // Get aoe pattern offsets
            Vector2Int[] aoeOffsets = null;
            if (skill.effectIds != null)
                foreach (var eId in skill.effectIds)
                {
                    var effect = session.effectRegistry.Get(eId);
                    if (effect?.aoePattern?.cells != null)
                    {
                        aoeOffsets = effect.aoePattern.cells;
                        break;
                    }
                }
            patterns.Add(new CurrentPatterns
            {
                SkillId = skillId,
                TargetCells = rawCells.ToArray(),
                AoePattern = aoeOffsets
            });

        }
        unit.SkillPatterns = patterns.ToArray();

        // Step 2 — apply passive pattern modifiers on world cells
        var activeEffects = session.GetUnitActiveEffects(unit.Id);
        foreach (var effect in activeEffects)
        {
            if (effect is ActiveEffectInstance passive)
                passive.effect.ApplyPatternModifier(unit, session);
        }

        // Step 3 — filter in place
        for (int i = 0; i < unit.SkillPatterns.Length; i++)
        {
            var skill = session.skillLibrary.Get(unit.SkillPatterns[i].SkillId);
            if (skill == null) continue;
            ServerPatternResolver.TranslateSkillPattern(unit, skill, session, i);
        }
    }


    public static void RecalculateAll(ServerMatchSession session)
    {
        foreach (var unit in session.units)
        {
            var activeEffects = session.GetUnitActiveEffects(unit.Id);
            Recalculate(unit, activeEffects, session);
        }
        // Debug.Log($"[UnitRecalculator] All {session.units.Count} units recalculated.");
    }
}