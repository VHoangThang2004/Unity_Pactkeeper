// Server/GameSystems/UnitRecalculator.cs
using System.Collections.Generic;
using UnityEngine;

public static class UnitRecalculator
{
    public static void Recalculate(
        UnitData unit,
        UnitDefinition def,
        List<EffectBase> activeEffects)
    {
        // Reset to base stats from definition
        unit.Speed = def.speed;
        unit.MaxHP = def.maxHp;
        unit.MaxSkillPoint = def.maxSkillPoint;
        unit.DamageMultiplier = 1f;
        unit.DamageReduction = 1f;

        // Apply each passive status effect's stat modifier in order
        foreach (var effect in activeEffects)
        {
            if (effect == null) continue;
            if (effect is not ServerPassiveEffectBase passiveEffect) continue;
            passiveEffect.ApplyStatModifier(unit);
        }

        Debug.Log($"[UnitRecalculator] Unit {unit.Id} recalculated — speed={unit.Speed} maxHP={unit.MaxHP} dmgMult={unit.DamageMultiplier} dmgReduce={unit.DamageReduction}");
    }
}