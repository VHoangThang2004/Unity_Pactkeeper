using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Shop Tab manager — layout theo sketch:
///   Left  : danh sách shop (ShopListPanel)
///   Center: grid items của shop đang chọn (ItemGridPanel)
///   Right : chi tiết item đang chọn + nút Mua (DetailPanel)
///   Bottom: gem balance + reset-period timer
///   Popup : confirm purchase + result
/// </summary>
public class ShopTabManager : MonoBehaviour
{
    // ── DTOs ─────────────────────────────────────────────────────────────────

    [System.Serializable]
    public class RewardDto
    {
        public string type;         // "Unit" | "Weapon" | "Trinket" | "Gems"
        public int    definitionId;
        public int    classId;      // Unit only
        public int    amount;
    }

    [System.Serializable]
    public class PlayerShopItemDto
    {
        public string   id;
        public RewardDto reward;
        public int      rewardTier;
        public int      price;
        // JsonUtility does not support int? — use -1 as sentinel for "unlimited"
        public int      purchaseLimit       = -1;
        public int      purchasedCount;
        public int      remainingPurchases  = -1;

        // Helpers — treat -1 as "no limit"
        public bool HasPurchaseLimit        => purchaseLimit >= 0;
        public bool HasRemainingPurchases   => remainingPurchases >= 0;
    }

    [System.Serializable]
    public class PlayerShopDto
    {
        public string   id;
        public string   name;
        public string   description;
        public int      displayOrder;
        public PlayerShopItemDto[] items;
        public string   resetPeriod;    // "Lifetime" | "Daily" | "Weekly"
        public string   startDate;
        public string   expiryDate;
        public string   dailyStartTime;
        public string   dailyEndTime;
    }

    [System.Serializable]
    public class ShopPurchaseResponseDto
    {
        public bool     success;
        public RewardDto reward;
        public int      quantity;
        public int      gemsSpent;
        public int      gemsRemaining;
        // JsonUtility does not support int? — use -1 as sentinel for "no limit"
        public int      remainingPurchases  = -1;
        public bool     isDuplicate;
        public int      compensationGems;

        public bool HasRemainingPurchases => remainingPurchases >= 0;
    }

    [System.Serializable]
    private class ShopArrayWrapper { public PlayerShopDto[] items; }

    [System.Serializable]
    private class PurchaseRequestDto
    {
        public string shopId;
        public string shopItemId;
        public int    quantity;
    }

    // ── Inspector ─────────────────────────────────────────────────────────────

    [Header("Config")]
    [SerializeField] private BackendConfig config;

    [Header("Registries")]
    [SerializeField] private WeaponDefinitionRegistry  weaponRegistry;
    [SerializeField] private TrinketDefinitionRegistry trinketRegistry;
    [SerializeField] private ClassDefinitionRegistry   classRegistry;
    [SerializeField] private UnitPrefabRegistry        unitPrefabRegistry;

    [Tooltip("Sprites assigned per shop index (0, 1, 2…). Cycles if shops > sprites.")]
    [SerializeField] private Sprite[] shopBackgroundSprites;
    [SerializeField] private Image    backgroundImage;

    [Tooltip("Default / gem icon.")]
    [SerializeField] private Sprite gemDefaultSprite;

    [Header("Root Panel")]
    [SerializeField] private GameObject panel;
    [SerializeField] private Button     closeButton;

    [Header("Left — Shop List")]
    [SerializeField] private Transform  shopListContainer;
    [SerializeField] private GameObject shopButtonPrefab;   // Prefab: Button + TMP_Text child

    [Header("Center — Item Grid")]
    [SerializeField] private Transform  itemGridContainer;
    [SerializeField] private GameObject shopItemPrefab;
    [SerializeField] private GameObject itemEmptyState;
    [SerializeField] private TMP_Text   shopNameText;
    [SerializeField] private TMP_Text   shopDescriptionText;

    [Header("Pagination — Item Grid")]
    [SerializeField] private int      itemsPerPage   = 8;  // 2 rows × 4 cols
    [SerializeField] private Button   itemPrevButton;
    [SerializeField] private Button   itemNextButton;
    [SerializeField] private TMP_Text itemPageLabel;

    [Header("Right — Item Detail")]
    [SerializeField] private GameObject detailPanel;
    [SerializeField] private GameObject detailEmptyState;
    [SerializeField] private Image      detailRewardIcon;
    [SerializeField] private Image      detailRewardFrame;
    [SerializeField] private TMP_Text   detailRewardNameText;
    [SerializeField] private TMP_Text   detailPriceText;
    [SerializeField] private TMP_Text   detailLimitText;     // "Remaining: 2 / 5"
    [SerializeField] private TMP_Text   detailRewardTierText; // "Tier 2"
    [SerializeField] private Button     buyButton;
    [SerializeField] private TMP_Text   buyButtonText;

    [Header("Bottom Bar")]
    [SerializeField] private TMP_Text   gemBalanceText;
    [SerializeField] private TMP_Text   resetPeriodText;     // "Resets: Daily" / expiry countdown

    [Header("Confirm Popup")]
    [SerializeField] private GameObject confirmPopup;
    [SerializeField] private TMP_Text   confirmMessageText;  // "Buy Sharp Dagger for 160 💎?"
    [SerializeField] private Button     confirmYesButton;
    [SerializeField] private Button     confirmNoButton;

    [Header("Result Popup")]
    [SerializeField] private GameObject resultPopup;
    [SerializeField] private TMP_Text   resultMessageText;
    [SerializeField] private Button     resultOkButton;

    // ── Runtime ───────────────────────────────────────────────────────────────

    private List<PlayerShopDto> allShops       = new();
    private PlayerShopDto       selectedShop;
    private PlayerShopItemDto   selectedItem;
    private ShopItemEntry       selectedEntry;
    private int                 localGems      = 0;
    private int                 currentItemPage = 0;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    void Awake()
    {
        if (closeButton     != null) closeButton.onClick.AddListener(Close);
        if (confirmYesButton != null) confirmYesButton.onClick.AddListener(OnConfirmYes);
        if (confirmNoButton  != null) confirmNoButton.onClick.AddListener(OnConfirmNo);
        if (resultOkButton   != null) resultOkButton.onClick.AddListener(OnResultOk);
        if (itemPrevButton   != null) itemPrevButton.onClick.AddListener(ItemPrevPage);
        if (itemNextButton   != null) itemNextButton.onClick.AddListener(ItemNextPage);

        HidePanel();
        HideDetailPanel();
        HidePopups();
    }

    void OnDestroy()
    {
        if (closeButton     != null) closeButton.onClick.RemoveListener(Close);
        if (confirmYesButton != null) confirmYesButton.onClick.RemoveAllListeners();
        if (confirmNoButton  != null) confirmNoButton.onClick.RemoveAllListeners();
        if (resultOkButton   != null) resultOkButton.onClick.RemoveAllListeners();
        if (itemPrevButton   != null) itemPrevButton.onClick.RemoveListener(ItemPrevPage);
        if (itemNextButton   != null) itemNextButton.onClick.RemoveListener(ItemNextPage);
    }

    // ── Public API ────────────────────────────────────────────────────────────

    public void Open()
    {
        weaponRegistry?.Init();
        trinketRegistry?.Init();
        classRegistry?.Init();
        unitPrefabRegistry?.Init();

        HideDetailPanel();
        HidePopups();
        selectedShop  = null;
        selectedItem  = null;
        selectedEntry = null;

        if (panel != null) panel.SetActive(true);
        StartCoroutine(LoadShopsCoroutine());
    }

    public void Close()
    {
        HidePanel();
        HidePopups();
        ClearShopList();
        ClearItemGrid();
    }

    // ── Called by ShopItemEntry ───────────────────────────────────────────────

    public void OnItemSelected(ShopItemEntry entry, PlayerShopItemDto item)
    {
        if (selectedEntry != null) selectedEntry.SetSelected(false);
        selectedEntry = entry;
        selectedItem  = item;
        selectedEntry.SetSelected(true);
        ShowItemDetail(item);
    }

    // ── Data loading ──────────────────────────────────────────────────────────

    IEnumerator LoadShopsCoroutine()
    {
        ClearShopList();
        ClearItemGrid();
        allShops.Clear();

        using var req = UnityWebRequest.Get($"{config.backendUrl}/api/shop/active");
        config.SetHeaders(req, PlayerSession.Token);
        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError($"[Shop] Failed to load shops: {req.error}");
            if (itemEmptyState != null) itemEmptyState.SetActive(true);
            yield break;
        }

        var wrapper = JsonUtility.FromJson<ShopArrayWrapper>(
            "{\"items\":" + req.downloadHandler.text + "}");

        if (wrapper.items != null)
            allShops.AddRange(wrapper.items);

        RefreshGemBalance();
        RenderShopList();

        // Auto-select first shop
        if (allShops.Count > 0)
            SelectShop(allShops[0], 0);
    }

    // ── Shop List (Left Panel) ────────────────────────────────────────────────

    void RenderShopList()
    {
        ClearShopList();
        if (allShops.Count == 0) return;

        for (int i = 0; i < allShops.Count; i++)
        {
            int idx = i;
            var shop = allShops[i];

            var btnObj = Instantiate(shopButtonPrefab, shopListContainer);
            var btn    = btnObj.GetComponent<Button>();
            var lbl    = btnObj.GetComponentInChildren<TMP_Text>();

            if (lbl != null) lbl.text = shop.name;
            if (btn != null) btn.onClick.AddListener(() => SelectShop(shop, idx));
        }
    }

    void SelectShop(PlayerShopDto shop, int index)
    {
        selectedShop = shop;
        selectedItem  = null;
        selectedEntry = null;
        HideDetailPanel();

        // Background swap
        if (backgroundImage != null && shopBackgroundSprites != null && shopBackgroundSprites.Length > 0)
            backgroundImage.sprite = shopBackgroundSprites[index % shopBackgroundSprites.Length];

        // Shop header
        if (shopNameText        != null) shopNameText.text        = shop.name;
        if (shopDescriptionText != null) shopDescriptionText.text = shop.description;

        // Reset period / expiry
        if (resetPeriodText != null)
            resetPeriodText.text = BuildResetLabel(shop);

        currentItemPage = 0;
        RenderItemGrid(shop);
    }

    // ── Item Grid (Center Panel) ──────────────────────────────────────────────

    void RenderItemGrid(PlayerShopDto shop)
    {
        ClearItemGrid();

        if (shop.items == null || shop.items.Length == 0)
        {
            if (itemEmptyState != null) itemEmptyState.SetActive(true);
            UpdateItemPageLabel(0, 0);
            return;
        }

        if (itemEmptyState != null) itemEmptyState.SetActive(false);

        int total      = shop.items.Length;
        int totalPages = Mathf.CeilToInt((float)total / itemsPerPage);
        currentItemPage = Mathf.Clamp(currentItemPage, 0, Mathf.Max(0, totalPages - 1));

        int startIndex = currentItemPage * itemsPerPage;
        int endIndex   = Mathf.Min(startIndex + itemsPerPage, total);

        for (int i = startIndex; i < endIndex; i++)
        {
            var obj   = Instantiate(shopItemPrefab, itemGridContainer);
            var entry = obj.GetComponent<ShopItemEntry>();
            entry?.Setup(shop.items[i], this);
        }

        UpdateItemPageLabel(currentItemPage + 1, totalPages);
    }

    void ItemPrevPage()
    {
        if (selectedShop == null || currentItemPage <= 0) return;
        currentItemPage--;
        selectedItem  = null;
        selectedEntry = null;
        HideDetailPanel();
        RenderItemGrid(selectedShop);
    }

    void ItemNextPage()
    {
        if (selectedShop == null) return;
        int totalPages = Mathf.CeilToInt((float)selectedShop.items.Length / itemsPerPage);
        if (currentItemPage >= totalPages - 1) return;
        currentItemPage++;
        selectedItem  = null;
        selectedEntry = null;
        HideDetailPanel();
        RenderItemGrid(selectedShop);
    }

    void UpdateItemPageLabel(int current, int total)
    {
        if (itemPageLabel  != null)
            itemPageLabel.text = total > 0 ? $"Page {current} / {total}" : string.Empty;
        if (itemPrevButton != null)
            itemPrevButton.interactable = current > 1;
        if (itemNextButton != null)
            itemNextButton.interactable = current < total;
    }

    // ── Item Detail (Right Panel) ─────────────────────────────────────────────

    void ShowItemDetail(PlayerShopItemDto item)
    {
        if (detailPanel      != null) detailPanel.SetActive(true);
        if (detailEmptyState != null) detailEmptyState.SetActive(false);

        // Icon + frame
        ApplyRewardVisuals(item.reward, detailRewardIcon, detailRewardFrame);

        // Name
        if (detailRewardNameText != null)
            detailRewardNameText.text = GetRewardName(item.reward);

        // Price
        if (detailPriceText != null)
            detailPriceText.text = $"Price: {item.price} Gems";

        // Tier
        if (detailRewardTierText != null)
            detailRewardTierText.text = $"Tier {item.rewardTier}";

        // Limit
        if (detailLimitText != null)
        {
            if (item.HasPurchaseLimit)
            {
                int rem = item.HasRemainingPurchases ? item.remainingPurchases : item.purchaseLimit;
                detailLimitText.text = $"Remaining: {rem} / {item.purchaseLimit}";
                detailLimitText.gameObject.SetActive(true);
            }
            else
            {
                detailLimitText.text = "No purchase limit";
                detailLimitText.gameObject.SetActive(true);
            }
        }

        // Buy button state
        bool soldOut   = item.HasPurchaseLimit && item.HasRemainingPurchases && item.remainingPurchases <= 0;
        bool canAfford = localGems >= item.price;
        bool canBuy    = !soldOut && canAfford;

        if (buyButton != null)
            buyButton.interactable = canBuy;

        if (buyButtonText != null)
        {
            if (soldOut)        buyButtonText.text = "Sold Out";
            else if (!canAfford) buyButtonText.text = "Not Enough Gems";
            else                buyButtonText.text = "Buy";
        }
    }

    // ── Buy Flow ──────────────────────────────────────────────────────────────

    /// <summary>Called by BuyButton OnClick() in Inspector.</summary>
    public void OnClickBuy()
    {
        if (selectedItem == null || selectedShop == null) return;

        string itemName = GetRewardName(selectedItem.reward);
        if (confirmMessageText != null)
            confirmMessageText.text =
                $"Buy {itemName} (x{selectedItem.reward.amount})\nfor {selectedItem.price} Gems?";

        if (confirmPopup != null) confirmPopup.SetActive(true);
    }

    void OnConfirmYes()
    {
        if (confirmPopup != null) confirmPopup.SetActive(false);
        if (selectedItem == null || selectedShop == null) return;
        StartCoroutine(PurchaseCoroutine(selectedShop.id, selectedItem));
    }

    void OnConfirmNo()
    {
        if (confirmPopup != null) confirmPopup.SetActive(false);
    }

    IEnumerator PurchaseCoroutine(string shopId, PlayerShopItemDto item)
    {
        if (buyButton != null) buyButton.interactable = false;

        var requestBody = new PurchaseRequestDto
        {
            shopId     = shopId,
            shopItemId = item.id,
            quantity   = 1
        };

        string json = JsonUtility.ToJson(requestBody);
        using var req = new UnityWebRequest($"{config.backendUrl}/api/shop/purchase", "POST");
        req.uploadHandler   = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(json));
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");
        config.SetHeaders(req, PlayerSession.Token);

        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError($"[Shop] Purchase failed: {req.error}");
            ShowResult($"Purchase failed.\n{req.error}");
            if (buyButton != null) buyButton.interactable = true;
            yield break;
        }

        var response = JsonUtility.FromJson<ShopPurchaseResponseDto>(req.downloadHandler.text);

        if (!response.success)
        {
            ShowResult("Purchase failed.");
            if (buyButton != null) buyButton.interactable = true;
            yield break;
        }

        // ── Update local state ────────────────────────────────────────────────
        localGems = response.gemsRemaining;
        if (gemBalanceText != null) gemBalanceText.text = $"{localGems} Gems";

        // Update remaining on the item
        item.remainingPurchases = response.remainingPurchases;
        item.purchasedCount++;

        // Refresh the selected entry card
        selectedEntry?.RefreshLimit(response.remainingPurchases);

        // Refresh detail panel
        ShowItemDetail(item);

        // ── Build result message ──────────────────────────────────────────────
        string itemName = GetRewardName(response.reward);
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"✓ Purchased: {itemName}");
        sb.AppendLine($"Gems spent: {response.gemsSpent}");
        sb.AppendLine($"Gems remaining: {response.gemsRemaining}");

        if (response.isDuplicate && response.compensationGems > 0)
            sb.AppendLine($"(Duplicate — received {response.compensationGems} gem compensation)");

        if (response.HasRemainingPurchases)
            sb.AppendLine($"Purchases remaining: {response.remainingPurchases}");

        ShowResult(sb.ToString().TrimEnd());
    }

    // ── Result popup ──────────────────────────────────────────────────────────

    void ShowResult(string message)
    {
        if (resultMessageText != null) resultMessageText.text = message;
        if (resultPopup       != null) resultPopup.SetActive(true);
    }

    void OnResultOk()
    {
        if (resultPopup != null) resultPopup.SetActive(false);
    }

    // ── Public helpers used by ShopItemEntry ──────────────────────────────────

    public string GetRewardName(RewardDto reward)
    {
        if (reward == null) return string.Empty;
        switch (reward.type)
        {
            case "Weapon":
                var wDef = weaponRegistry?.GetDefinition(reward.definitionId);
                if (wDef != null && !string.IsNullOrEmpty(wDef.name)) return wDef.name;
                return "Weapon";
            case "Trinket":
                var tDef = trinketRegistry?.GetDefinition(reward.definitionId);
                if (tDef != null && !string.IsNullOrEmpty(tDef.name)) return tDef.name;
                return "Trinket";
            case "Unit":
                string uName = unitPrefabRegistry?.GetName(reward.definitionId);
                if (!string.IsNullOrEmpty(uName)) return uName;
                return "Unit";
            default:
                return $"Gems x{reward.amount}";
        }
    }

    public void ApplyRewardVisuals(RewardDto reward, Image iconImg, Image frameImg)
    {
        if (frameImg != null) frameImg.gameObject.SetActive(false);

        if (reward == null)
        {
            if (iconImg != null) iconImg.sprite = gemDefaultSprite;
            return;
        }

        switch (reward.type)
        {
            case "Weapon":
                if (iconImg != null)
                    iconImg.sprite = weaponRegistry?.GetIcon(reward.definitionId) ?? gemDefaultSprite;
                break;

            case "Trinket":
                if (iconImg != null)
                    iconImg.sprite = trinketRegistry?.GetIcon(reward.definitionId) ?? gemDefaultSprite;
                break;

            case "Unit":
                Sprite bodySprite = null;
                if (unitPrefabRegistry != null)
                {
                    var prefab = unitPrefabRegistry.Get(reward.definitionId);
                    bodySprite = prefab?.GetComponent<ClientUnit>()?.unitImg;
                }
                if (iconImg != null)
                    iconImg.sprite = bodySprite != null ? bodySprite : gemDefaultSprite;

                if (frameImg != null && classRegistry != null)
                {
                    var frameSprite = classRegistry.GetFullFrame(reward.classId)
                                   ?? classRegistry.GetHalfFrame(reward.classId);
                    if (frameSprite != null)
                    {
                        frameImg.sprite = frameSprite;
                        frameImg.gameObject.SetActive(true);
                    }
                }
                break;

            default: // Gems
                if (iconImg != null) iconImg.sprite = gemDefaultSprite;
                break;
        }
    }

    // ── Bottom bar helpers ────────────────────────────────────────────────────

    void RefreshGemBalance()
    {
        // Try reading from the "Gem" GameObject in MainMenu (same pattern as TopUpManager)
        var gemObj = GameObject.Find("Gem");
        if (gemObj != null)
        {
            var tmp = gemObj.GetComponentInChildren<TMP_Text>();
            if (tmp != null && int.TryParse(
                System.Text.RegularExpressions.Regex.Replace(tmp.text, "[^0-9]", ""),
                out int parsed))
            {
                localGems = parsed;
            }
        }

        if (gemBalanceText != null)
            gemBalanceText.text = $"{localGems} Gems";
    }

    static string BuildResetLabel(PlayerShopDto shop)
    {
        var sb = new System.Text.StringBuilder();

        switch (shop.resetPeriod)
        {
            case "Daily":   sb.Append("Resets: Daily");   break;
            case "Weekly":  sb.Append("Resets: Weekly");  break;
            default:        sb.Append("One-time Shop");   break;
        }

        // Daily time window
        if (!string.IsNullOrEmpty(shop.dailyStartTime) && !string.IsNullOrEmpty(shop.dailyEndTime))
            sb.Append($"  |  Open {shop.dailyStartTime.Substring(0, 5)}–{shop.dailyEndTime.Substring(0, 5)} UTC");

        // Expiry
        if (!string.IsNullOrEmpty(shop.expiryDate) &&
            System.DateTime.TryParse(shop.expiryDate, out var expiry))
        {
            var remaining = expiry - System.DateTime.UtcNow;
            if (remaining.TotalSeconds > 0)
            {
                if (remaining.TotalDays >= 1)
                    sb.Append($"  |  Expires in {(int)remaining.TotalDays}d {remaining.Hours}h");
                else
                    sb.Append($"  |  Expires in {remaining.Hours}h {remaining.Minutes}m");
            }
            else
            {
                sb.Append("  |  Expired");
            }
        }

        return sb.ToString();
    }

    // ── UI helpers ────────────────────────────────────────────────────────────

    void HidePanel()        { if (panel != null) panel.SetActive(false); }
    void HideDetailPanel()
    {
        if (detailPanel      != null) detailPanel.SetActive(false);
        if (detailEmptyState != null) detailEmptyState.SetActive(true);
    }
    void HidePopups()
    {
        if (confirmPopup != null) confirmPopup.SetActive(false);
        if (resultPopup  != null) resultPopup.SetActive(false);
    }
    void ClearShopList()  { foreach (Transform t in shopListContainer)  Destroy(t.gameObject); }
    void ClearItemGrid()  { foreach (Transform t in itemGridContainer)  Destroy(t.gameObject); }
}
