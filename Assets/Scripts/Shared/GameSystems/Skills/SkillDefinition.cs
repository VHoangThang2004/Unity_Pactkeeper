// Shared/GameSystems/Skills/SkillDefinition.cs
using UnityEngine;

[CreateAssetMenu(fileName = "Skill_", menuName = "SRPG/Skill Definition")]
public class SkillDefinition : ScriptableObject
{
    [Header("Identity")]
    public int skillId;
    public string skillName;

    [Header("Targeting")]
    public SkillPattern targetPattern;
    public bool isTargetPatternFixed;
    public TargetFilter targeting;

    [Header("Cost")]
    public int skillPointCost = 1;
    public float stepCostMultiplier = 1f;
    [Header("Usage Limits")]
    public int useLimitPerInstant = 1;  // max times usable per paused instant, -1 = unlimited
    public int useLimitTotal = -1;      // max times usable entire match, -1 = unlimited

    [Header("Effects — IDs only")]
    public int[] effectIds;
}