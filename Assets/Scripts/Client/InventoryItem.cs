using UnityEngine;
using UnityEngine.UI;

public class InventoryItem : MonoBehaviour
{
    [Header("UI Elements")]
    [SerializeField] private Image iconImage;
    [SerializeField] private Image selectedBackground;

    private InventoryManager.InventoryItemData itemData;
    private InventoryManager inventoryManager;

    public void Setup(InventoryManager.InventoryItemData data, InventoryManager manager)
    {
        itemData = data;
        inventoryManager = manager;

        if (iconImage != null)
            iconImage.sprite = data.icon;

        SetSelected(false);
    }

    public void OnClick()
    {
        if (inventoryManager != null)
            inventoryManager.OnItemSelected(this, itemData);
    }

    public void SetSelected(bool isSelected)
    {
        if (selectedBackground != null)
            selectedBackground.gameObject.SetActive(isSelected);
    }
}
