using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.VisualScripting;
using System.Linq;

public class SlotConfig : MonoBehaviour
{
    public enum SlotType { MovementSkill, ClassSkill, Weapon, Trinket }

    [Header("Slot Display")]
    [SerializeField] private Image slotIcon;
    [SerializeField] private TextMeshProUGUI slotNameText;
    [SerializeField] private GameObject emptyIndicator;

    [Header("Description Panel")]
    [SerializeField] private GameObject descriptionPanel;
    [SerializeField] private Transform ContentContainer;

    [SerializeField] private TextMeshProUGUI ItemNameText;
    [SerializeField] private TextMeshProUGUI ItemDesctext;
    [SerializeField] private TextMeshProUGUI SkillNameText;
    [SerializeField] private TextMeshProUGUI SkillDescText;

    [SerializeField] private TextMeshProUGUI TargetPatternType;
    [SerializeField] private PatternInfo TargetPatternInfo;
    [SerializeField] private TextMeshProUGUI AoePatternType;
    [SerializeField] private PatternInfo AoePatternInfo;


    [Header("Select Options Panel")]
    [SerializeField] private GameObject selectOptionsPanel;
    [SerializeField] private Transform optionsContainer;
    [SerializeField] private GameObject optionItemPrefab;

    private UnitConfigManager manager;
    private SlotType slotType;

    public void Init(UnitConfigManager mgr, SlotType type)
    {
        manager = mgr;
        slotType = type;
        Refresh();
    }

    public void Refresh()
    {
        CloseAll();
        UpdateSlotDisplay();
        RebuildOptions();
    }

    // -------------------------------------------------------
    // Slot Display
    // -------------------------------------------------------

    void UpdateSlotDisplay()
    {
        if (manager?.CurrentUnit == null) return;

        bool hasItem = false;
        string name = "";

        switch (slotType)
        {
            case SlotType.MovementSkill:
                hasItem = manager.CurrentUnit.equippedMovementSkillId != -1;
                if (hasItem)
                {
                    var skill = manager.skillLibrary.Get(manager.CurrentUnit.equippedMovementSkillId);
                    if (skill == null)
                    {
                        slotIcon.sprite = emptyIndicator.GetComponent<Image>()?.sprite;
                        name = "Empty";
                    }
                    else
                    {
                        name = skill?.skillName ?? $"Skill {manager.CurrentUnit.equippedMovementSkillId}";
                        if (slotIcon != null) slotIcon.sprite = skill?.icon ?? emptyIndicator.GetComponent<Image>()?.sprite;
                    }
                }
                else
                {
                    if (slotIcon != null) slotIcon.sprite = emptyIndicator.GetComponent<Image>()?.sprite;
                    name = "Empty";
                }
                break;

            case SlotType.ClassSkill:
                hasItem = manager.CurrentUnit.equippedClassSkillId != -1;
                if (hasItem)
                {
                    var skill = manager.skillLibrary.Get(manager.CurrentUnit.equippedClassSkillId);
                    if (skill == null)
                    {
                        slotIcon.sprite = emptyIndicator.GetComponent<Image>()?.sprite;
                        name = "Empty";
                    }
                    else
                    {
                        name = skill?.skillName ?? $"Skill {manager.CurrentUnit.equippedClassSkillId}";
                        if (slotIcon != null) slotIcon.sprite = skill?.icon ?? emptyIndicator.GetComponent<Image>()?.sprite;
                    }
                }
                else
                {
                    if (slotIcon != null) slotIcon.sprite = emptyIndicator.GetComponent<Image>()?.sprite;
                    name = "Empty";
                }
                break;

            case SlotType.Weapon:
                hasItem = manager.CurrentUnit.equippedWeapon != null;
                if (hasItem)
                {
                    if (slotIcon != null) slotIcon.sprite = manager.weaponRegistry.GetIcon(manager.CurrentUnit.equippedWeapon.definitionId);
                    var def = manager.WeaponDefinitions.Find(w => w.weaponId == manager.CurrentUnit.equippedWeapon.definitionId);
                    name = def?.name ?? $"Weapon {manager.CurrentUnit.equippedWeapon.definitionId}";
                }
                else
                {
                    if (slotIcon != null) slotIcon.sprite = emptyIndicator.GetComponent<Image>()?.sprite;
                    name = "Empty";
                }
                break;

            case SlotType.Trinket:
                hasItem = manager.CurrentUnit.equippedTrinket != null;
                if (hasItem)
                {
                    if (slotIcon != null) slotIcon.sprite = manager.trinketRegistry.GetIcon(manager.CurrentUnit.equippedTrinket.definitionId);
                    var def = manager.TrinketDefinitions.Find(t => t.trinketId == manager.CurrentUnit.equippedTrinket.definitionId);
                    name = def?.name ?? $"Trinket {manager.CurrentUnit.equippedTrinket.definitionId}";
                }
                else
                {
                    if (slotIcon != null) slotIcon.sprite = emptyIndicator.GetComponent<Image>()?.sprite;
                    name = "Empty";
                }
                break;
        }

        if (slotNameText != null) slotNameText.text = hasItem ? name : "Empty";
        if (emptyIndicator != null) emptyIndicator.SetActive(!hasItem);
    }

    // -------------------------------------------------------
    // Options
    // -------------------------------------------------------

    void RebuildOptions()
    {
        if (optionsContainer == null) return;
        foreach (Transform child in optionsContainer) Destroy(child.gameObject);
        if (manager?.CurrentUnit == null) return;

        switch (slotType)
        {
            case SlotType.MovementSkill:
                foreach (var s in manager.AvailableMovementSkills)
                {
                    var skill = manager.skillLibrary.Get(s.skillId);
                    string name = skill?.skillName ?? $"Skill {s.skillId}";
                    bool equipped = s.skillId == manager.CurrentUnit.equippedMovementSkillId;
                    int id = s.skillId;
                    SpawnOption(name, equipped, false, skill?.icon ?? emptyIndicator.GetComponent<Image>()?.sprite,
                        () => manager.StartCoroutine(manager.PatchMovementSkill(id)));
                }
                if (manager.CurrentUnit.equippedMovementSkillId != -1)
                    SpawnUnequipOption(() => manager.StartCoroutine(manager.PatchMovementSkill(-1)));
                break;

            case SlotType.ClassSkill:
                foreach (var s in manager.AvailableClassSkills)
                {
                    var skill = manager.skillLibrary.Get(s.skillId);
                    string name = skill?.skillName ?? $"Skill {s.skillId}";
                    bool equipped = s.skillId == manager.CurrentUnit.equippedClassSkillId;
                    int id = s.skillId;
                    SpawnOption(name, equipped, false, skill?.icon ?? emptyIndicator.GetComponent<Image>()?.sprite,
                        () => manager.StartCoroutine(manager.PatchClassSkill(id)));
                }
                if (manager.CurrentUnit.equippedClassSkillId != -1)
                    SpawnUnequipOption(() => manager.StartCoroutine(manager.PatchClassSkill(-1)));
                break;

            case SlotType.Weapon:
                foreach (var w in manager.AvailableWeapons)
                {
                    var def = manager.WeaponDefinitions.Find(d => d.weaponId == w.weaponDefinitionId);
                    string name = def?.name ?? $"Weapon {w.weaponDefinitionId}";
                    bool isThisUnit = w.ownedWeaponId == manager.CurrentUnitDataWithUnlockedClass.equippedOwnedWeaponId;
                    bool equippedByOther = w.isEquipped && !isThisUnit;
                    string id = w.ownedWeaponId;
                    SpawnOption(name, isThisUnit, equippedByOther, manager.weaponRegistry.GetIcon(w.weaponDefinitionId) ?? emptyIndicator.GetComponent<Image>()?.sprite,
                        () => manager.StartCoroutine(manager.PatchWeapon(id)));

                }
                if (manager.CurrentUnit.equippedWeapon != null)
                    SpawnUnequipOption(() => manager.StartCoroutine(manager.PatchWeapon(string.Empty)));
                break;

            case SlotType.Trinket:
                foreach (var t in manager.AvailableTrinkets)
                {
                    var def = manager.TrinketDefinitions.Find(d => d.trinketId == t.trinketDefinitionId);
                    string name = def?.name ?? $"Trinket {t.trinketDefinitionId}";
                    string tId = t.ownedTrinketId;
                    bool isThisUnit = tId == manager.CurrentUnitDataWithUnlockedClass.equippedOwnedTrinketId;
                    bool equippedByOther = t.isEquipped && !isThisUnit;
                    SpawnOption(name, isThisUnit, equippedByOther, manager.trinketRegistry.GetIcon(t.trinketDefinitionId) ?? emptyIndicator.GetComponent<Image>()?.sprite,
                        () => manager.StartCoroutine(manager.PatchTrinket(tId)));
                    // Debug.Log($"Slot config: {manager.CurrentUnit.uId} equipped trinket = {manager.CurrentUnit.equippedTrinket} trinketId = {manager.CurrentUnit.equippedTrinket?.ownedItemId} | toggling trinket {tId} (isThisUnit: {isThisUnit}, equippedByOther: {equippedByOther})");
                }
                if (manager.CurrentUnit.equippedTrinket != null)
                    SpawnUnequipOption(() => manager.StartCoroutine(manager.PatchTrinket(string.Empty)));
                break;
        }
    }

    void SpawnOption(string name, bool isEquipped, bool equippedByOther, Sprite icon, Action onEquip)
    {
        var go = Instantiate(optionItemPrefab, optionsContainer);
        go.GetComponent<OptionItem>()?.Setup(name, isEquipped, equippedByOther, icon, () =>
        {
            CloseAll();
            onEquip();
        });
    }

    void SpawnUnequipOption(Action onUnequip)
    {
        var go = Instantiate(optionItemPrefab, optionsContainer);
        go.GetComponent<OptionItem>()?.Setup("Unequip", false, false, emptyIndicator.GetComponent<Image>()?.sprite, () =>
        {
            CloseAll();
            onUnequip();
        });
    }

    // -------------------------------------------------------
    // Panel Control
    // -------------------------------------------------------

    public void CloseAll()
    {
        descriptionPanel?.SetActive(false);
        selectOptionsPanel?.SetActive(false);
    }

    public void OnClickSlot()
    {
        if (manager?.CurrentUnit == null) return;
        manager.OnClickCloseAll();
        if (ContentContainer != null)
            ContentContainer.localPosition = new Vector3(ContentContainer.localPosition.x, 0f, ContentContainer.localPosition.z);

        bool hasItem = slotType switch
        {
            SlotType.MovementSkill => manager.CurrentUnitDataWithUnlockedClass.equippedMovementSkillId != -1,
            SlotType.ClassSkill => manager.CurrentUnitDataWithUnlockedClass.equippedClassSkillId != -1,
            SlotType.Weapon => manager.CurrentUnitDataWithUnlockedClass.equippedOwnedWeaponId != null && manager.CurrentUnitDataWithUnlockedClass.equippedOwnedWeaponId != "",
            SlotType.Trinket => manager.CurrentUnitDataWithUnlockedClass.equippedOwnedTrinketId != null && manager.CurrentUnitDataWithUnlockedClass.equippedOwnedTrinketId != "",
            _ => false
        };

        if (hasItem)
        {
            UpdateDescriptionPanel();
            descriptionPanel?.SetActive(true);
        }
        else
            selectOptionsPanel?.SetActive(true);
    }

    void UpdateDescriptionPanel()
    {
        if (ItemNameText == null || SkillDescText == null) return;
        ItemNameText.text = "";
        ItemDesctext.text = "";
        SkillNameText.text = "";
        SkillDescText.text = "";
        switch (slotType)
        {
            case SlotType.MovementSkill:
                {
                    var skill = manager.skillLibrary.Get(manager.CurrentUnit.equippedMovementSkillId);
                    if (skill != null)
                    {
                        UnitData unit = new UnitData
                        {
                            MaxHP = manager.CurrentStats.maxHP,
                            MaxSkillPoint = manager.CurrentStats.maxSkillPoint,
                            Speed = manager.CurrentStats.speed,
                            DamageMultiplier = manager.CurrentStats.damageMultiplier,
                            DamageReduction = manager.CurrentStats.damageReduction
                        };
                        string totalLimit = skill.useLimitTotal > 0 ? skill.useLimitTotal.ToString() : "∞";
                        string instantLimit = skill.useLimitPerInstant > 0 ? skill.useLimitPerInstant.ToString() : "∞";

                        SkillNameText.text = skill?.skillName ?? $"Skill {manager.CurrentUnit.equippedMovementSkillId}";
                        SkillDescText.text = skill.description + " => ";
                        string tptype = skill.isTargetPatternFixed ? "Fixed" : "Flexible";
                        TargetPatternType.text = $"Target Pattern ({tptype})";
                        TargetPatternInfo?.Init(skill.targetPattern.cells.ToList(), true);
                        if (skill.effectIds.Length > 0)
                        {
                            var effect0 = manager.effectRegistry.Get(skill.effectIds[0]);
                            if (effect0 != null)
                            {
                                string aoetype = effect0.isAoePatternFixed ? "Fixed" : "Flexible";
                                AoePatternType.text = $"Aoe Pattern ({tptype})";
                                AoePatternInfo?.Init(effect0.aoePattern.cells.ToList(), false);
                            }
                        }
                        foreach (var effectId in skill.effectIds)
                        {
                            ClientActiveEffectBase effect = manager.effectRegistry.Get(effectId);
                            SkillDescText.text += " " + effect.GetDescription(unit, manager.effectRegistry);
                        }
                        SkillDescText.text += $"\nSP Cost: {skill.skillPointCost}";
                        SkillDescText.text += $"\nStep multiplier: x{skill.stepCostMultiplier}";
                        SkillDescText.text += $"\nLimit per instant: {instantLimit} | Total Limit: {totalLimit}";

                    }
                    break;
                }
            case SlotType.ClassSkill:
                {
                    var skill = manager.skillLibrary.Get(manager.CurrentUnit.equippedClassSkillId);
                    if (skill != null)
                    {
                        UnitData unit = new UnitData
                        {
                            MaxHP = manager.CurrentStats.maxHP,
                            MaxSkillPoint = manager.CurrentStats.maxSkillPoint,
                            Speed = manager.CurrentStats.speed,
                            DamageMultiplier = manager.CurrentStats.damageMultiplier,
                            DamageReduction = manager.CurrentStats.damageReduction
                        };
                        string totalLimit = skill.useLimitTotal > 0 ? skill.useLimitTotal.ToString() : "∞";
                        string instantLimit = skill.useLimitPerInstant > 0 ? skill.useLimitPerInstant.ToString() : "∞";

                        SkillNameText.text = skill.skillName;
                        SkillDescText.text = skill.description + " => ";
                        string tptype = skill.isTargetPatternFixed ? "Fixed" : "Flexible";
                        TargetPatternType.text = $"Target Pattern ({tptype})";
                        TargetPatternInfo?.Init(skill.targetPattern.cells.ToList(), true);
                        if (skill.effectIds.Length > 0)
                        {
                            var effect0 = manager.effectRegistry.Get(skill.effectIds[0]);
                            if (effect0 != null)
                            {
                                string aoetype = effect0.isAoePatternFixed ? "Fixed" : "Flexible";
                                AoePatternType.text = $"Aoe Pattern ({tptype})";
                                AoePatternInfo?.Init(effect0.aoePattern.cells.ToList(), false);
                            }
                        }
                        foreach (var effectId in skill.effectIds)
                        {
                            ClientActiveEffectBase effect = manager.effectRegistry.Get(effectId);
                            SkillDescText.text += " " + effect.GetDescription(unit, manager.effectRegistry);
                        }
                        SkillDescText.text += $"\nSP Cost: {skill.skillPointCost}";
                        SkillDescText.text += $"\nStep multiplier: x{skill.stepCostMultiplier}";
                        SkillDescText.text += $"\nLimit per instant: {instantLimit} | Total Limit: {totalLimit}";
                    }
                    break;
                }
            case SlotType.Weapon:
                {
                    var def = manager.WeaponDefinitions.Find(
                        w => w.weaponId == manager.CurrentUnit.equippedWeapon.definitionId);
                    if (def != null)
                    {
                        ItemNameText.text = def.name;
                        string hpBonus = def.statModifiers.maxHP != 0 ? $"HP+{def.statModifiers.maxHP}\n " : "";
                        string spBonus = def.statModifiers.maxSkillPoint != 0 ? $"SP+{def.statModifiers.maxSkillPoint} \n" : "";
                        string speedBonus = def.statModifiers.speed != 0 ? $"Speed+{def.statModifiers.speed} \n" : "";
                        string damageBonus = def.statModifiers.damageMultiplier != 0 ? $"DMG +{def.statModifiers.damageMultiplier}% \n" : "";
                        string reductionBonus = def.statModifiers.damageReduction != 0 ? $"DMG Reduction +{def.statModifiers.damageReduction}% \n" : "";
                        ItemDesctext.text = hpBonus + spBonus + speedBonus + damageBonus + reductionBonus;
                        var skill = manager.skillLibrary.Get(def.skillId);
                        if (skill != null)
                        {
                            UnitData unit = new UnitData
                            {
                                MaxHP = manager.CurrentStats.maxHP,
                                MaxSkillPoint = manager.CurrentStats.maxSkillPoint,
                                Speed = manager.CurrentStats.speed,
                                DamageMultiplier = manager.CurrentStats.damageMultiplier,
                                DamageReduction = manager.CurrentStats.damageReduction
                            };
                            string totalLimit = skill.useLimitTotal > 0 ? skill.useLimitTotal.ToString() : "∞";
                            string instantLimit = skill.useLimitPerInstant > 0 ? skill.useLimitPerInstant.ToString() : "∞";

                            SkillNameText.text = skill.skillName;
                            SkillDescText.text = skill.description + " => ";
                            string tptype = skill.isTargetPatternFixed ? "Fixed" : "Flexible";
                            TargetPatternType.text = $"Target Pattern ({tptype})";
                            TargetPatternInfo?.Init(skill.targetPattern.cells.ToList(), true);
                            if (skill.effectIds.Length > 0)
                            {
                                var effect0 = manager.effectRegistry.Get(skill.effectIds[0]);
                                if (effect0 != null)
                                {
                                    string aoetype = effect0.isAoePatternFixed ? "Fixed" : "Flexible";
                                    AoePatternType.text = $"Aoe Pattern ({tptype})";
                                    AoePatternInfo?.Init(effect0.aoePattern.cells.ToList(), false);
                                }
                            }
                            foreach (var effectId in skill.effectIds)
                            {
                                ClientActiveEffectBase effect = manager.effectRegistry.Get(effectId);
                                SkillDescText.text += " " + effect.GetDescription(unit, manager.effectRegistry);
                            }
                            SkillDescText.text += $"\nSP Cost: {skill.skillPointCost}";
                            SkillDescText.text += $"\nStep multiplier: x{skill.stepCostMultiplier}";
                            SkillDescText.text += $"\nLimit per instant: {instantLimit} | Total Limit: {totalLimit}";
                        }
                    }
                    break;
                }
            case SlotType.Trinket:
                {
                    var def = manager.TrinketDefinitions.Find(
                        t => t.trinketId == manager.CurrentUnit.equippedTrinket.definitionId);
                    if (def != null)
                    {
                        ItemNameText.text = def.name;
                        string hpBonus = def.statModifiers.maxHP != 0 ? $"HP+{def.statModifiers.maxHP}\n " : "";
                        string spBonus = def.statModifiers.maxSkillPoint != 0 ? $"SP+{def.statModifiers.maxSkillPoint} \n" : "";
                        string speedBonus = def.statModifiers.speed != 0 ? $"Speed+{def.statModifiers.speed} \n" : "";
                        string damageBonus = def.statModifiers.damageMultiplier != 0 ? $"DMG +{def.statModifiers.damageMultiplier}% \n" : "";
                        string reductionBonus = def.statModifiers.damageReduction != 0 ? $"DMG Reduction +{def.statModifiers.damageReduction}% \n" : "";
                        ItemDesctext.text = hpBonus + spBonus + speedBonus + damageBonus + reductionBonus;

                        var skill = manager.skillLibrary.Get(def.skillId);
                        if (skill != null)
                        {
                            UnitData unit = new UnitData
                            {
                                MaxHP = manager.CurrentStats.maxHP,
                                MaxSkillPoint = manager.CurrentStats.maxSkillPoint,
                                Speed = manager.CurrentStats.speed,
                                DamageMultiplier = manager.CurrentStats.damageMultiplier,
                                DamageReduction = manager.CurrentStats.damageReduction
                            };
                            string totalLimit = skill.useLimitTotal > 0 ? skill.useLimitTotal.ToString() : "∞";
                            string instantLimit = skill.useLimitPerInstant > 0 ? skill.useLimitPerInstant.ToString() : "∞";

                            SkillNameText.text = skill.skillName;
                            SkillDescText.text = skill.description + " => ";
                            string tptype = skill.isTargetPatternFixed ? "Fixed" : "Flexible";
                            TargetPatternType.text = $"Target Pattern ({tptype})";
                            TargetPatternInfo?.Init(skill.targetPattern.cells.ToList(), true);
                            if (skill.effectIds.Length > 0)
                            {
                                var effect0 = manager.effectRegistry.Get(skill.effectIds[0]);
                                if (effect0 != null)
                                {
                                    string aoetype = effect0.isAoePatternFixed ? "Fixed" : "Flexible";
                                    AoePatternType.text = $"Aoe Pattern ({tptype})";
                                    AoePatternInfo?.Init(effect0.aoePattern.cells.ToList(), false);
                                }
                            }
                            foreach (var effectId in skill.effectIds)
                            {
                                ClientActiveEffectBase effect = manager.effectRegistry.Get(effectId);
                                SkillDescText.text += " " + effect.GetDescription(unit, manager.effectRegistry);
                            }
                            SkillDescText.text += $"\nSP Cost: {skill.skillPointCost}";
                            SkillDescText.text += $"\nStep multiplier: x{skill.stepCostMultiplier}";
                            SkillDescText.text += $"\nLimit per instant: {instantLimit} | Total Limit: {totalLimit}";
                        }
                    }
                    break;
                }
        }
    }

    public void OnClickChange()
    {
        descriptionPanel?.SetActive(true);
        selectOptionsPanel?.SetActive(true);
    }

    public void OnClickUnequip()
    {
        CloseAll();
        switch (slotType)
        {
            case SlotType.MovementSkill:
                manager.StartCoroutine(manager.PatchMovementSkill(-1)); break;
            case SlotType.ClassSkill:
                manager.StartCoroutine(manager.PatchClassSkill(-1)); break;
            case SlotType.Weapon:
                manager.StartCoroutine(manager.PatchWeapon(string.Empty)); break;
            case SlotType.Trinket:
                manager.StartCoroutine(manager.PatchTrinket(string.Empty)); break;
        }
    }

    public void OnClickCloseDescription() => descriptionPanel?.SetActive(false);
    public void OnClickCloseOptions() => selectOptionsPanel?.SetActive(false);
}