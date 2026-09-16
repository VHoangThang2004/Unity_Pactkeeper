using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SkillSlotUI : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private ClientMatchSession session;
    [SerializeField] private ClientScene scene;

    [Header("Slot Config")]
    [SerializeField] private SkillSlotType slotType;

    [Header("UI")]
    [SerializeField] private Image icon;

    public enum SkillSlotType { Weapon, Class, Trinket, UniquePassive }

    int GetSkillId()
    {
        UnitData unit = session.GetUnitDataById(session.selectedUnitId);
        if (unit == null) return -1;
        return slotType switch
        {
            SkillSlotType.Weapon => unit.WeaponSkillId,
            SkillSlotType.Class => unit.ClassSkillId,
            SkillSlotType.Trinket => unit.TrinketSkillId,
            SkillSlotType.UniquePassive => unit.PassiveSkillId,
            _ => -1
        };
    }

    public void OnSelect()
    {
        int skillId = GetSkillId();
        if (skillId == -1) return;
        scene.clientInteractionSystem.HandleDecisionSelectSkill(skillId);
    }

    public void OnInspect()
    {
        int skillId = GetSkillId();
        if (skillId == -1) return;
        SkillDefinition skill = scene.skillLibrary.Get(skillId);
        if (skill == null) return;
        Debug.Log($"[Inspect] {skill.skillName} — {skill.skillPointCost} SP, x{skill.stepCostMultiplier} step");
        // TODO: open inspect panel
        
        scene.clientInteractionSystem.HandleDecisionInspectSkill(skillId);
    }

    public void RefreshIcon()
    {
        int skillId = GetSkillId();
        SkillDefinition skill = scene.skillLibrary.Get(skillId);
        if (skill != null) icon.sprite = skill.icon;
        else icon.sprite = scene.visualController.noSkillIcon;
    }
}