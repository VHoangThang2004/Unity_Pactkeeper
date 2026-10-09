using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Prefab script for a single item card in the shop grid.
/// Pattern mirrors QuestItem / InventoryItem.
/// </summary>
public class ShopItemEntry : MonoBehaviour
{
    [Header("UI Elements")]
    [SerializeField] private Image     rewardIcon;
    [SerializeField] private Image     rewardFrame;        // Unit class frame overlay (active for Unit rewards only)
    [SerializeField] private TMP_Text  rewardNameText;     // "Sharp Dagger" / "Gems" / "Druid"
    [SerializeField] private TMP_Text  priceText;          // "160 💎"
    [SerializeField] private TMP_Text  limitText;          // "2 left" / "Unlimited" — shown only when PurchaseLimit set
    [SerializeField] private Image     selectedBackground;
    [SerializeField] private Image     soldOutOverlay;     // semi-transparent overlay when RemainingPurchases == 0

    // ── Runtime ───────────────────────────────────────────────────────────────

    private ShopTabManager.PlayerShopItemDto itemData;
    private ShopTabManager manager;

    // ── Setup ─────────────────────────────────────────────────────────────────

    public void Setup(ShopTabManager.PlayerShopItemDto data, ShopTabManager shopManager)
    {
        itemData = data;
        manager  = shopManager;

        // Reward name
        if (rewardNameText != null)
            rewardNameText.text = shopManager.GetRewardName(data.reward);

        // Price
        if (priceText != null)
            priceText.text = $"{data.price} Gems";

        // Purchase limit label
        if (limitText != null)
        {
            if (data.HasPurchaseLimit)
            {
                int rem = data.HasRemainingPurchases ? data.remainingPurchases : data.purchaseLimit;
                limitText.text = rem > 0 ? $"{rem} left" : "Sold Out";
                limitText.gameObject.SetActive(true);
            }
            else
            {
                limitText.text = "Unlimited";
                limitText.gameObject.SetActive(true);
            }
        }

        // Reward icon + frame
        shopManager.ApplyRewardVisuals(data.reward, rewardIcon, rewardFrame);

        // Sold-out overlay
        bool soldOut = data.HasPurchaseLimit && data.HasRemainingPurchases && data.remainingPurchases <= 0;
        if (soldOutOverlay != null)
            soldOutOverlay.gameObject.SetActive(soldOut);

        SetSelected(false);
    }

    // ── Interaction ───────────────────────────────────────────────────────────

    public void OnClick()
    {
        if (itemData == null || manager == null) return;
        manager.OnItemSelected(this, itemData);
    }

    public void SetSelected(bool selected)
    {
        if (selectedBackground != null)
            selectedBackground.gameObject.SetActive(selected);
    }

    // ── Refresh after purchase ────────────────────────────────────────────────

    public void RefreshLimit(int remaining)
    {
        itemData.remainingPurchases = remaining;

        if (limitText != null && itemData.HasPurchaseLimit)
        {
            limitText.text = remaining > 0 ? $"{remaining} left" : "Sold Out";
        }

        bool soldOut = itemData.HasPurchaseLimit && remaining <= 0;
        if (soldOutOverlay != null)
            soldOutOverlay.gameObject.SetActive(soldOut);
    }
}
