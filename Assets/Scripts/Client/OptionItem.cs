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

    public void Setup(string name, bool isEquipped, bool equippedByOther, Sprite icon, Action onEquip)
    {
        if (nameText != null) nameText.text = name;
        if (iconImage != null) iconImage.sprite = icon;

        UsingIndicator?.SetActive(isEquipped && !equippedByOther);
        OtherEquippedIndicator?.SetActive(equippedByOther && !isEquipped);

        if (equipButton != null)
        {
            equipButton.onClick.RemoveAllListeners();
            equipButton.interactable = !isEquipped;
            if (!isEquipped)
                equipButton.onClick.AddListener(() => onEquip());
        }
    }
}