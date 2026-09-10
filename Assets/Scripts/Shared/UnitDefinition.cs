using UnityEngine;

[CreateAssetMenu(fileName = "Unit_", menuName = "SRPG/Unit Definition")]
public class UnitDefinition : ScriptableObject
{
    [Header("Identity")]
    public int uId;
    public string unitName;

    [Header("Base Stats")]
    public int speed;
    public int maxHp;
    public int maxSkillPoint;

    [Header("Skills")]
    public SkillDefinition movementSkill;
    public SkillDefinition[] passiveSkills;

    // Future:
    // public SkillDefinition weaponSkill;
    // public SkillDefinition classSkill;
    // public SkillDefinition[] activeSkills;
}