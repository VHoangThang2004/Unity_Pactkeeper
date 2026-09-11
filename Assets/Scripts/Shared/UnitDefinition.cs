using UnityEngine;

[CreateAssetMenu(fileName = "Unit_", menuName = "SRPG/Unit Definition")]
public class UnitDefinition : ScriptableObject
{
    [Header("Identity")]
    public int uId;
    public string unitName;

    [Header("Fixed Stats")]
    public int speed;
    public int maxHp;
    public int maxSkillPoint;

    [Header("Fixed Skills")]
    public SkillDefinition passiveSkill; // only fixed skill — innate to unit type
}