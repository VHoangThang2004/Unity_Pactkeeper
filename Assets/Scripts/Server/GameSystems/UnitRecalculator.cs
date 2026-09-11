using System.Collections.Generic;
using UnityEngine;

public static class UnitRecalculator
{
    private static void Recalculate(
        UnitData unit,
        UnitDefinition def,
        List<EffectBase> activeEffects,
        ServerMatchSession session)
    {
        // Reset to base stats
        unit.Speed = def.speed;
        unit.MaxHP = def.maxHp;
        unit.MaxSkillPoint = def.maxSkillPoint;
        unit.DamageMultiplier = 1f;
        unit.DamageReduction = 1f;
        InitSkillPatterns(unit, session);

        // Apply passive stat modifiers
        foreach (var effect in activeEffects)
        {
            if (effect == null) continue;
            if (effect is not ServerPassiveEffectBase passiveEffect) continue;
            passiveEffect.ApplyStatModifier(unit);
        }

        // Debug.Log($"[UnitRecalculator] Unit {unit.Id} recalculated — speed={unit.Speed} maxHP={unit.MaxHP} dmgMult={unit.DamageMultiplier} dmgReduce={unit.DamageReduction}");
    }

    private static void InitSkillPatterns(UnitData unit, ServerMatchSession session)
    {
        var skillIds = new List<int>
    {
        unit.MovementSkillId,
        unit.WeaponSkillId,
        unit.ClassSkillId,
        unit.EquipmentSkillId
    };

        var patterns = new List<CurrentPatterns>();
        foreach (var skillId in skillIds)
        {
            if (skillId == -1) continue;
            var skill = session.skillLibrary.Get(skillId);
            if (skill == null) continue;
            patterns.Add(ServerPatternResolver.BuildSkillPattern(unit, skill, session));
        }

        unit.SkillPatterns = patterns.ToArray();
        // Debug.Log($"[SpawnManager] Unit {unit.Id} — {patterns.Count} skill patterns initialized.");
    }

    public static void RecalculateAll(ServerMatchSession session)
    {
        foreach (var unit in session.units)
        {
            var def = session.unitLibrary.Get(unit.UId);
            if (def == null) continue;
            var activeEffects = session.GetUnitActiveEffects(unit.Id);
            Recalculate(unit, def, activeEffects, session);
        }
        // Debug.Log($"[UnitRecalculator] All {session.units.Count} units recalculated.");
    }
}