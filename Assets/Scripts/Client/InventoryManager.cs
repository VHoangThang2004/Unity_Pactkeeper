using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;
using TMPro;
using System.Collections.Generic;

public class InventoryManager : MonoBehaviour
{
    [Header("Panel")]
    [SerializeField] private GameObject panel;
    [SerializeField] private Button closeButton;

    [Header("Left Panel - Item List")]
    [SerializeField] private Transform itemListContainer;
    [SerializeField] private GameObject inventoryItemPrefab;
    [SerializeField] private GameObject emptyStatePanel;

    [Header("Pagination")]
    [SerializeField] private int      itemsPerPage = 35; // 5 rows × 7 cols
    [SerializeField] private Button   prevButton;
    [SerializeField] private Button   nextButton;
    [SerializeField] private TMP_Text pageLabel;

    [Header("Filter Buttons")]
    [SerializeField] private Button filterAllButton;
    [SerializeField] private Button filterWeaponsButton;
    [SerializeField] private Button filterTrinketsButton;

    [Header("Right Panel - Item Detail")]
    [SerializeField] private GameObject detailPanel;
    [SerializeField] private GameObject detailEmptyState;
    [SerializeField] private Image detailIcon;
    [SerializeField] private TMP_Text detailNameText;
    [SerializeField] private TMP_Text detailTypeText;
    [SerializeField] private TMP_Text detailStatText;

    [Header("Config")]
    [SerializeField] private BackendConfig config;

    [Header("Registries")]
    [SerializeField] private WeaponDefinitionRegistry weaponRegistry;
    [SerializeField] private TrinketDefinitionRegistry trinketRegistry;

    private List<InventoryItemData> allItems = new List<InventoryItemData>();
    private List<InventoryItemData> filteredItems = new List<InventoryItemData>();
    private InventoryItem selectedItem = null;
    private ItemFilter currentFilter = ItemFilter.All;
    private int currentPage = 0;

    private enum ItemFilter
    {
        All,
        Weapon,
        Trinket
    }

    public class InventoryItemData
    {
        public string ownedId;
        public int definitionId;
        public ItemType type;
        public string name;
        public Sprite icon;
        public EquipmentStatModifiersDto stats;

        public enum ItemType
        {
            Weapon,
            Trinket
        }
    }

    void Awake()
    {
        if (closeButton != null)
            closeButton.onClick.AddListener(Close);

        if (filterAllButton != null)
            filterAllButton.onClick.AddListener(() => SetFilter(ItemFilter.All));
        if (filterWeaponsButton != null)
            filterWeaponsButton.onClick.AddListener(() => SetFilter(ItemFilter.Weapon));
        if (filterTrinketsButton != null)
            filterTrinketsButton.onClick.AddListener(() => SetFilter(ItemFilter.Trinket));
        if (prevButton != null)
            prevButton.onClick.AddListener(PrevPage);
        if (nextButton != null)
            nextButton.onClick.AddListener(NextPage);

        HidePanel();
    }

    void OnDestroy()
    {
        if (closeButton != null)
            closeButton.onClick.RemoveListener(Close);

        if (filterAllButton != null)
            filterAllButton.onClick.RemoveAllListeners();
        if (filterWeaponsButton != null)
            filterWeaponsButton.onClick.RemoveAllListeners();
        if (filterTrinketsButton != null)
            filterTrinketsButton.onClick.RemoveAllListeners();
        if (prevButton != null)
            prevButton.onClick.RemoveListener(PrevPage);
        if (nextButton != null)
            nextButton.onClick.RemoveListener(NextPage);
    }

    public void Open()
    {
        // Initialize registries
        if (weaponRegistry != null)
            weaponRegistry.Init();
        if (trinketRegistry != null)
            trinketRegistry.Init();

        if (panel != null)
            panel.SetActive(true);
        currentPage = 0;
        LoadInventory();
    }

    void LoadInventory()
    {
        StartCoroutine(LoadInventoryCoroutine());
    }

    public void Close()
    {
        HidePanel();
        ClearItemList();
        HideDetailPanel();
    }

    void HidePanel()
    {
        if (panel != null)
            panel.SetActive(false);
    }

    System.Collections.IEnumerator LoadInventoryCoroutine()
    {
        using var request = UnityWebRequest.Get($"{config.backendUrl}/api/PlayerProfile");
        config.SetHeaders(request, PlayerSession.Token);
        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError("[Inventory] Failed to load inventory: " + request.error);
            ShowEmptyState();
            yield break;
        }

        Debug.Log("[Inventory] API Response: " + request.downloadHandler.text);
        var profile = JsonUtility.FromJson<PlayerProfileResponse>(request.downloadHandler.text);
        Debug.Log("[Inventory] Weapons count: " + (profile.ownedWeapons != null ? profile.ownedWeapons.Length : 0));
        Debug.Log("[Inventory] Trinkets count: " + (profile.ownedTrinkets != null ? profile.ownedTrinkets.Length : 0));
        ProcessInventoryData(profile);
    }

    void ProcessInventoryData(PlayerProfileResponse profile)
    {
        allItems.Clear();

        // Process weapons
        if (profile.ownedWeapons != null)
        {
            foreach (var weapon in profile.ownedWeapons)
            {
                Debug.Log($"[Inventory] Processing weapon: {weapon.weaponDefinitionId}");
                var weaponDef = GetWeaponDefinition(weapon.weaponDefinitionId);
                if (weaponDef != null)
                {
                    Debug.Log($"[Inventory] Weapon name: {weaponDef.name}");
                    allItems.Add(new InventoryItemData
                    {
                        ownedId = weapon.ownedWeaponId,
                        definitionId = weapon.weaponDefinitionId,
                        type = InventoryItemData.ItemType.Weapon,
                        name = weaponDef.name,
                        icon = weaponRegistry?.GetIcon(weapon.weaponDefinitionId),
                        stats = weaponDef.statModifiers
                    });
                }
                else
                {
                    Debug.LogError($"[Inventory] Weapon definition not found for ID: {weapon.weaponDefinitionId}");
                }
            }
        }

        // Process trinkets
        if (profile.ownedTrinkets != null)
        {
            foreach (var trinket in profile.ownedTrinkets)
            {
                Debug.Log($"[Inventory] Processing trinket: {trinket.trinketDefinitionId}");
                var trinketDef = GetTrinketDefinition(trinket.trinketDefinitionId);
                if (trinketDef != null)
                {
                    Debug.Log($"[Inventory] Trinket name: {trinketDef.name}");
                    allItems.Add(new InventoryItemData
                    {
                        ownedId = trinket.ownedTrinketId,
                        definitionId = trinket.trinketDefinitionId,
                        type = InventoryItemData.ItemType.Trinket,
                        name = trinketDef.name,
                        icon = trinketRegistry?.GetIcon(trinket.trinketDefinitionId),
                        stats = trinketDef.statModifiers
                    });
                }
                else
                {
                    Debug.LogError($"[Inventory] Trinket definition not found for ID: {trinket.trinketDefinitionId}");
                }
            }
        }

        Debug.Log($"[Inventory] Total items loaded: {allItems.Count}");
        ApplyFilter();
    }

    void ApplyFilter()
    {
        filteredItems.Clear();

        foreach (var item in allItems)
        {
            switch (currentFilter)
            {
                case ItemFilter.All:
                    filteredItems.Add(item);
                    break;
                case ItemFilter.Weapon:
                    if (item.type == InventoryItemData.ItemType.Weapon)
                        filteredItems.Add(item);
                    break;
                case ItemFilter.Trinket:
                    if (item.type == InventoryItemData.ItemType.Trinket)
                        filteredItems.Add(item);
                    break;
            }
        }

        RenderItemList();
    }

    void SetFilter(ItemFilter filter)
    {
        currentFilter = filter;
        currentPage   = 0;
        ApplyFilter();
    }

    void RenderItemList()
    {
        ClearItemList();

        if (filteredItems.Count == 0)
        {
            ShowEmptyState();
            UpdatePageLabel(0, 0);
            return;
        }

        HideEmptyState();

        int totalPages = Mathf.CeilToInt((float)filteredItems.Count / itemsPerPage);
        currentPage = Mathf.Clamp(currentPage, 0, Mathf.Max(0, totalPages - 1));

        int startIndex = currentPage * itemsPerPage;
        int endIndex   = Mathf.Min(startIndex + itemsPerPage, filteredItems.Count);

        for (int i = startIndex; i < endIndex; i++)
        {
            var itemObj       = Instantiate(inventoryItemPrefab, itemListContainer);
            var itemComponent = itemObj.GetComponent<InventoryItem>();
            if (itemComponent != null)
                itemComponent.Setup(filteredItems[i], this);
        }

        UpdatePageLabel(currentPage + 1, totalPages);
    }

    void PrevPage()
    {
        if (currentPage <= 0) return;
        currentPage--;
        RenderItemList();
    }

    void NextPage()
    {
        int totalPages = Mathf.CeilToInt((float)filteredItems.Count / itemsPerPage);
        if (currentPage >= totalPages - 1) return;
        currentPage++;
        RenderItemList();
    }

    void UpdatePageLabel(int current, int total)
    {
        if (pageLabel  != null)
            pageLabel.text = total > 0 ? $"Page {current} / {total}" : string.Empty;
        if (prevButton != null)
            prevButton.interactable = current > 1;
        if (nextButton != null)
            nextButton.interactable = current < total;
    }

    void ClearItemList()
    {
        foreach (Transform child in itemListContainer)
        {
            Destroy(child.gameObject);
        }
    }

    void ShowEmptyState()
    {
        if (emptyStatePanel != null)
            emptyStatePanel.SetActive(true);
    }

    void HideEmptyState()
    {
        if (emptyStatePanel != null)
            emptyStatePanel.SetActive(false);
    }

    public void OnItemSelected(InventoryItem item, InventoryItemData itemData)
    {
        // Deselect previous
        if (selectedItem != null)
            selectedItem.SetSelected(false);

        // Select new
        selectedItem = item;
        selectedItem.SetSelected(true);

        // Show detail
        ShowItemDetail(itemData);
    }

    void ShowItemDetail(InventoryItemData itemData)
    {
        Debug.Log("[Inventory] Showing detail for item: " + itemData.name);

        if (detailPanel != null)
            detailPanel.SetActive(true);
        if (detailEmptyState != null)
            detailEmptyState.SetActive(false);

        if (detailIcon != null)
            detailIcon.sprite = itemData.icon;
        if (detailNameText != null)
        {
            detailNameText.text = itemData.name;
            Debug.Log("[Inventory] Detail name set to: " + itemData.name);
        }
        else
        {
            Debug.LogError("[Inventory] detailNameText is null!");
        }

        if (detailTypeText != null)
            detailTypeText.text = itemData.type.ToString();

        if (detailStatText != null)
        {
            var stats = itemData.stats;
            detailStatText.text = $"HP: {stats.maxHP}\n" +
                                  $"Speed: {stats.speed}\n" +
                                  $"Skill Point: {stats.maxSkillPoint}\n" +
                                  $"Damage: {stats.damageMultiplier}\n" +
                                  $"Defense: {stats.damageReduction}";
        }
    }

    void HideDetailPanel()
    {
        if (detailPanel != null)
            detailPanel.SetActive(false);
        if (detailEmptyState != null)
            detailEmptyState.SetActive(true);
        selectedItem = null;
    }

    // Helper methods to get definitions
    private WeaponDefinitionDto GetWeaponDefinition(int weaponId)
    {
        if (weaponRegistry != null)
            return weaponRegistry.GetDefinition(weaponId);
        return null;
    }

    private TrinketDefinitionDto GetTrinketDefinition(int trinketId)
    {
        if (trinketRegistry != null)
            return trinketRegistry.GetDefinition(trinketId);
        return null;
    }
}
