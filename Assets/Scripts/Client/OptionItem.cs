using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class OptionItem : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private GameObject OtherEquippedIndicator;
    [SerializeField] private GameObject UsingIndicator;
    [SerializeField] private Button equipButton;
    [SerializeField] private Image iconImage;
    [SerializeField] private Image backgroundImage;

    static readonly Color32 UsingColor = new Color32(0xF5, 0xF5, 0xF5, 0xFF);
    static readonly Color32 AvailableColor = new Color32(0xFF, 0xEB, 0x04, 0xFF);
    static readonly Color32 UnequipColor = new Color32(0xF4, 0xA4, 0x60, 0xFF);

    public void Setup(string name, bool isEquipped, bool equippedByOther, Sprite icon, Action onEquip, bool isUnequip = false)
    {
        if (nameText != null) nameText.text = name;
        if (iconImage != null) iconImage.sprite = icon;

        bool isUsing = isEquipped && !equippedByOther;
        UsingIndicator?.SetActive(isUsing);
        OtherEquippedIndicator?.SetActive(equippedByOther && !isEquipped);

        if (equipButton != null)
        {
            equipButton.transition = Selectable.Transition.None;
            equipButton.onClick.RemoveAllListeners();
            equipButton.interactable = !isEquipped || isUnequip;
            if (!isEquipped || isUnequip)
                equipButton.onClick.AddListener(() => onEquip());
        }

        Image background = backgroundImage != null ? backgroundImage : GetComponent<Image>();
        if (background != null)
        {
            if (isUnequip)
                background.color = UnequipColor;
            else if (isUsing)
                background.color = UsingColor;
            else
                background.color = AvailableColor;
        }
    }
}
