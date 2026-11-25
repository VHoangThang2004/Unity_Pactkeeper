using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

public class ShopItemEntry : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    static readonly Color32 HoverColor = new Color32(0xFF, 0xF9, 0xB4, 0xFF);

    [Header("UI Elements")]
    [SerializeField] private Image     rewardIcon;
    [SerializeField] private Image     rewardFrame;
    [SerializeField] private TMP_Text  rewardNameText;
    [SerializeField] private TMP_Text  priceText;
    [SerializeField] private TMP_Text  limitText;
    [SerializeField] private TMP_Text  tierText;
    [SerializeField] private Image     cardImage;
    [SerializeField] private Image     selectedBackground;
    [SerializeField] private Image     soldOutOverlay;

    ShopTabManager.PlayerShopItemDto itemData;
    ShopTabManager manager;
    Color normalColor = Color.white;

    void Awake()
    {
        if (cardImage == null)
            cardImage = GetComponent<Image>();
        if (cardImage != null)
            normalColor = cardImage.color;

        var button = GetComponent<Button>();
        if (button != null)
            button.transition = Selectable.Transition.None;
    }

    public void Setup(ShopTabManager.PlayerShopItemDto data, ShopTabManager shopManager)
    {
        itemData = data;
        manager  = shopManager;

        if (rewardNameText != null)
            rewardNameText.text = shopManager.GetRewardName(data.reward);

        if (priceText != null)
            priceText.text = data.price.ToString();

        if (tierText != null)
            tierText.text = data.rewardTier.ToString();

        ApplyLimit();
        shopManager.ApplyRewardVisuals(data.reward, rewardIcon, rewardFrame);

        bool soldOut = IsSoldOut();
        if (soldOutOverlay != null)
            soldOutOverlay.gameObject.SetActive(soldOut);

        SetSelected(false);
        if (cardImage != null)
            cardImage.color = normalColor;
    }

    public void OnClick()
    {
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left || eventData.clickCount < 2)
            return;
        if (itemData == null || manager == null || IsSoldOut())
            return;
        manager.RequestPurchase(this, itemData);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (cardImage != null)
            cardImage.color = HoverColor;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (cardImage != null)
            cardImage.color = normalColor;
    }

    public void SetSelected(bool selected)
    {
        if (selectedBackground != null)
            selectedBackground.gameObject.SetActive(selected);
    }

    public void RefreshLimit(int remaining)
    {
        itemData.remainingPurchases = remaining;
        if (itemData.HasPurchaseLimit && remaining >= 0)
            itemData.purchasedCount = Mathf.Max(0, itemData.purchaseLimit - remaining);

        ApplyLimit();

        bool soldOut = itemData.HasPurchaseLimit && remaining >= 0 && remaining <= 0;
        if (soldOutOverlay != null)
            soldOutOverlay.gameObject.SetActive(soldOut);
    }

    void ApplyLimit()
    {
        if (limitText == null || itemData == null)
            return;

        if (!itemData.HasPurchaseLimit)
        {
            limitText.text = string.Empty;
            return;
        }

        int purchased = itemData.purchasedCount;
        if (itemData.HasRemainingPurchases)
            purchased = Mathf.Max(0, itemData.purchaseLimit - itemData.remainingPurchases);
        limitText.text = $"{purchased}/{itemData.purchaseLimit}";
    }

    bool IsSoldOut()
    {
        return itemData != null
            && itemData.HasPurchaseLimit
            && itemData.HasRemainingPurchases
            && itemData.remainingPurchases <= 0;
    }
}
