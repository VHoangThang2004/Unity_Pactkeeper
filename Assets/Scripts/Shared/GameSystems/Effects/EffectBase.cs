using UnityEngine;

public abstract class EffectBase : ScriptableObject
{
    public int effectId;
    public float resolveDuration = 1f;
    public int baseValue = 1;
    public abstract InstantType InstantType { get; }

    [Header("AoE Pattern")]
    public SkillPattern aoePattern;
    public bool isAoePatternFixed;
}