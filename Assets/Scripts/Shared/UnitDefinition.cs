using UnityEngine;

[CreateAssetMenu(fileName = "Unit_", menuName = "SRPG/Unit Definition")]
public class UnitDefinition : ScriptableObject
{
    [Header("Identity")]
    public int uId;           // Matches DB uid later
    public string unitName;

    [Header("Stats")]
    public int moveRange;

    // Future: public int hp, armor, ap...
    // Future: public SkillDefinition[] skills;
}