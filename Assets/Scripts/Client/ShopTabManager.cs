using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using TMPro;

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

    [Tooltip("Default / gem icon.")]
    [SerializeField] private Sprite gemDefaultSprite;

    [Header("Root Panel")]
    [SerializeField] private GameObject panel;
    [SerializeField] private Button     closeButton;

    const int MaxShopButtons = 4;

    [Header("Shop Tag")]
    [SerializeField] private Image  shopTag;
    [SerializeField] private Sprite standardTagSprite;
    [SerializeField] private Sprite limitedTagSprite;

    [Header("Left — Shop List")]
    [SerializeField] private Transform  shopListContainer;
    [SerializeField] private GameObject shopButtonPrefab;

    [Header("Center — Item Grid")]
    [SerializeField] private Transform  itemGridContainer;
    [SerializeField] private GameObject shopItemPrefab;
    [SerializeField] private GameObject itemEmptyState;

    [Header("Pagination — Item Grid")]
    [SerializeField] private int      itemsPerPage   = 8;
    [SerializeField] private Button   itemPrevButton;
    [SerializeField] private Button   itemNextButton;
    [SerializeField] private TMP_Text itemPageLabel;

    [Header("Bottom Bar")]
    [SerializeField] private TMP_Text   gemBalanceText;
    [SerializeField] private TMP_Text   resetPeriodText;
    [SerializeField] private TMP_Text   shopDescriptionText;

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

    private readonly ScrollingText descriptionScroll = new ScrollingText();
    private GameObject outsideClick;
    private List<PlayerShopDto> allShops       = new();
    private readonly List<ShopListButton> shopButtons = new();
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

        EnsureOutsideClick();
        HidePanel();
        HidePopups();
    }

    void Update()
    {
        descriptionScroll.Tick();
    }

    void LateUpdate()
    {
        descriptionScroll.LateTick();
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

        HidePopups();
        selectedShop  = null;
        selectedItem  = null;
        selectedEntry = null;

        SetOutsideClick(true);
        this.OpenPanel(panel);
        StartCoroutine(LoadShopsCoroutine());
    }

    public void Close()
    {
        SetOutsideClick(false);
        this.ClosePanel(panel);
        HidePopups();
        ClearShopList();
        ClearItemGrid();
    }

    // ── Called by ShopItemEntry ───────────────────────────────────────────────

    public void RequestPurchase(ShopItemEntry entry, PlayerShopItemDto item)
    {
        if (item == null || selectedShop == null)
            return;

        bool soldOut = item.HasPurchaseLimit && item.HasRemainingPurchases && item.remainingPurchases <= 0;
        if (soldOut)
            return;

        selectedEntry = entry;
        selectedItem  = item;
        OnClickBuy();
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

        allShops.Sort((a, b) => a.displayOrder.CompareTo(b.displayOrder));
        if (allShops.Count > MaxShopButtons)
            allShops.RemoveRange(MaxShopButtons, allShops.Count - MaxShopButtons);

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
        if (allShops.Count == 0 || shopButtonPrefab == null || shopListContainer == null)
            return;

        int count = Mathf.Min(MaxShopButtons, allShops.Count);
        for (int i = 0; i < count; i++)
        {
            int idx = i;
            var shop = allShops[i];
            var btnObj = Instantiate(shopButtonPrefab, shopListContainer);
            btnObj.SetActive(true);
            var view = btnObj.GetComponent<ShopListButton>();
            if (view == null)
                view = btnObj.AddComponent<ShopListButton>();
            view.Setup(shop, idx, () => SelectShop(allShops[idx], idx));
            shopButtons.Add(view);
        }
    }

    void SelectShop(PlayerShopDto shop, int index)
    {
        selectedShop = shop;
        selectedItem  = null;
        selectedEntry = null;

        for (int i = 0; i < shopButtons.Count; i++)
        {
            if (shopButtons[i] != null)
                shopButtons[i].SetSelected(i == index);
        }

        ApplyShopTag(shop);

        if (resetPeriodText != null)
            resetPeriodText.text = string.IsNullOrEmpty(shop.resetPeriod) ? string.Empty : shop.resetPeriod;

        descriptionScroll.Play(shopDescriptionText, shop.description ?? string.Empty, 40f);

        currentItemPage = 0;
        RenderItemGrid(shop);
    }

    void ApplyShopTag(PlayerShopDto shop)
    {
        if (shopTag == null)
            return;

        bool limited = IsLimitedShop(shop);
        Sprite sprite = limited ? limitedTagSprite : standardTagSprite;
        if (sprite != null)
            shopTag.sprite = sprite;
        shopTag.enabled = sprite != null;
    }

    static bool IsLimitedShop(PlayerShopDto shop)
    {
        if (shop == null || string.IsNullOrEmpty(shop.expiryDate))
            return false;
        if (string.Equals(shop.resetPeriod, "Daily", System.StringComparison.OrdinalIgnoreCase))
            return false;
        return true;
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
        RenderItemGrid(selectedShop);
    }

    void UpdateItemPageLabel(int current, int total)
    {
        if (itemPageLabel  != null)
            itemPageLabel.text = total > 0 ? $"{current}/{total}" : string.Empty;
        if (itemPrevButton != null)
            itemPrevButton.interactable = current > 1;
        if (itemNextButton != null)
            itemNextButton.interactable = current < total;
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
            yield break;
        }

        var response = JsonUtility.FromJson<ShopPurchaseResponseDto>(req.downloadHandler.text);

        if (!response.success)
        {
            ShowResult("Purchase failed.");
            yield break;
        }

        // ── Update local state ────────────────────────────────────────────────
        localGems = response.gemsRemaining;
        if (gemBalanceText != null) gemBalanceText.text = localGems.ToString();

        // Update remaining on the item
        item.remainingPurchases = response.remainingPurchases;
        item.purchasedCount++;

        if (selectedEntry != null)
            selectedEntry.RefreshLimit(response.remainingPurchases);

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
            gemBalanceText.text = localGems.ToString();
    }

    // ── UI helpers ────────────────────────────────────────────────────────────

    void HidePanel()        { this.HidePanelImmediate(panel); }

    void EnsureOutsideClick()
    {
        if (outsideClick != null || panel == null || panel.transform.parent == null)
            return;

        outsideClick = new GameObject("ShopOutsideClick", typeof(RectTransform));
        outsideClick.transform.SetParent(panel.transform.parent, false);

        RectTransform rect = outsideClick.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Image image = outsideClick.AddComponent<Image>();
        image.color = new Color(0f, 0f, 0f, 0f);

        Button button = outsideClick.AddComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.onClick.AddListener(Close);

        CanvasGroup group = outsideClick.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = false;
        group.interactable = false;

        outsideClick.transform.SetSiblingIndex(panel.transform.GetSiblingIndex());
    }

    void SetOutsideClick(bool open)
    {
        if (outsideClick == null || panel == null)
            return;

        outsideClick.transform.SetSiblingIndex(panel.transform.GetSiblingIndex());
        CanvasGroup group = outsideClick.GetComponent<CanvasGroup>();
        if (group == null)
            return;
        group.blocksRaycasts = open;
        group.interactable = open;
    }
    void HidePopups()
    {
        if (confirmPopup != null) confirmPopup.SetActive(false);
        if (resultPopup  != null) resultPopup.SetActive(false);
    }
    void ClearShopList()
    {
        shopButtons.Clear();
        if (shopListContainer == null)
            return;
        for (int i = shopListContainer.childCount - 1; i >= 0; i--)
            Destroy(shopListContainer.GetChild(i).gameObject);
    }

    void ClearItemGrid()
    {
        if (itemGridContainer == null)
            return;
        for (int i = itemGridContainer.childCount - 1; i >= 0; i--)
            Destroy(itemGridContainer.GetChild(i).gameObject);
    }
}
