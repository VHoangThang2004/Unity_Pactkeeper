using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class InventoryManager : MonoBehaviour
{
    [Header("Panel")]
    [SerializeField] private GameObject panel;
    [SerializeField] private Button closeButton;
    [SerializeField] private Button backButton;

    [Header("Config")]
    [SerializeField] private BackendConfig config;
    [SerializeField] private SceneConfig sceneConfig;
    [SerializeField] private bool isStandaloneScene = false;

    [Header("Item Panel")]
    [SerializeField] private Transform weaponListContainer;
    [SerializeField] private Transform trinketListContainer;
    [SerializeField] private GameObject inventoryItemPrefab;

    [Header("Pagination")]
    [SerializeField] private int itemsPerPage = 16;
    [SerializeField] private Button prevButton;
    [SerializeField] private Button nextButton;
    [SerializeField] private TMP_Text pageLabel;

    [Header("Detail - Command")]
    [SerializeField] private GameObject commandPanel;
    [SerializeField] private TMP_Text commandText;

    [Header("Detail - Name and type")]
    [SerializeField] private GameObject nameTypePanel;
    [SerializeField] private Image detailIcon;
    [SerializeField] private TMP_Text descriptionText;
    [SerializeField] private Button detailButton;
    [SerializeField] private Button nameTypeCloseButton;

    [Header("Detail - Stats")]
    [SerializeField] private GameObject statPanel;
    [SerializeField] private TMP_Text detailStatText;
    [SerializeField] private Button statCloseButton;

    [Header("Registries")]
    [SerializeField] private WeaponDefinitionRegistry weaponRegistry;
    [SerializeField] private TrinketDefinitionRegistry trinketRegistry;

    const string WelcomeHasItems = "Welcome to inventory. Please select an item to view details";
    const string WelcomeEmpty = "Welcome to inventory. There is no item in your inventory at all";

    readonly List<InventoryItemData> weapons = new();
    readonly List<InventoryItemData> trinkets = new();

    InventoryItem selectedItem;
    InventoryItemData selectedData;
    int currentPage;

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
        if (backButton != null)
            backButton.onClick.AddListener(Close);
        if (detailButton != null)
            detailButton.onClick.AddListener(OnClickShowStats);
        if (nameTypeCloseButton != null)
            nameTypeCloseButton.onClick.AddListener(OnClickCloseNameType);
        if (statCloseButton != null)
            statCloseButton.onClick.AddListener(OnClickCloseStat);
        if (prevButton != null)
            prevButton.onClick.AddListener(PrevPage);
        if (nextButton != null)
            nextButton.onClick.AddListener(NextPage);

        HideDetailImmediate(nameTypePanel);
        HideDetailImmediate(statPanel);
        HideDetailImmediate(commandPanel);

        if (isStandaloneScene)
        {
            if (!PlayerSession.IsLoggedIn)
            {
                SceneManager.LoadScene(sceneConfig.mainMenuScene);
                return;
            }

            if (weaponRegistry != null) weaponRegistry.Init();
            if (trinketRegistry != null) trinketRegistry.Init();
            if (panel != null) panel.SetActive(true);
            LoadInventory();
        }
        else
        {
            if (panel != null) panel.SetActive(false);
        }
    }

    void OnDestroy()
    {
        if (closeButton != null) closeButton.onClick.RemoveListener(Close);
        if (backButton != null) backButton.onClick.RemoveListener(Close);
        if (detailButton != null) detailButton.onClick.RemoveListener(OnClickShowStats);
        if (nameTypeCloseButton != null) nameTypeCloseButton.onClick.RemoveListener(OnClickCloseNameType);
        if (statCloseButton != null) statCloseButton.onClick.RemoveListener(OnClickCloseStat);
        if (prevButton != null) prevButton.onClick.RemoveListener(PrevPage);
        if (nextButton != null) nextButton.onClick.RemoveListener(NextPage);
    }

    public void Open()
    {
        if (weaponRegistry != null) weaponRegistry.Init();
        if (trinketRegistry != null) trinketRegistry.Init();
        if (panel != null) panel.SetActive(true);
        currentPage = 0;
        selectedItem = null;
        selectedData = null;
        LoadInventory();
    }

    public void Close()
    {
        if (isStandaloneScene)
            SceneManager.LoadScene(sceneConfig.mainMenuScene);
        else if (panel != null)
            panel.SetActive(false);
    }

    void LoadInventory()
    {
        StartCoroutine(LoadInventoryCoroutine());
    }

    IEnumerator LoadInventoryCoroutine()
    {
        using var request = UnityWebRequest.Get($"{config.backendUrl}/api/PlayerProfile");
        config.SetHeaders(request, PlayerSession.Token);
        yield return request.SendWebRequest();

        weapons.Clear();
        trinkets.Clear();
        selectedItem = null;
        selectedData = null;

        if (request.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError("[Inventory] Failed to load inventory: " + request.error);
            RenderLists();
            ShowWelcome();
            yield break;
        }

        var profile = JsonUtility.FromJson<PlayerProfileResponse>(request.downloadHandler.text);
        if (profile.ownedWeapons != null)
        {
            foreach (var weapon in profile.ownedWeapons)
            {
                var def = weaponRegistry != null ? weaponRegistry.GetDefinition(weapon.weaponDefinitionId) : null;
                if (def == null) continue;
                weapons.Add(new InventoryItemData
                {
                    ownedId = weapon.ownedWeaponId,
                    definitionId = weapon.weaponDefinitionId,
                    type = InventoryItemData.ItemType.Weapon,
                    name = def.name,
                    icon = weaponRegistry.GetIcon(weapon.weaponDefinitionId),
                    stats = def.statModifiers
                });
            }
        }

        if (profile.ownedTrinkets != null)
        {
            foreach (var trinket in profile.ownedTrinkets)
            {
                var def = trinketRegistry != null ? trinketRegistry.GetDefinition(trinket.trinketDefinitionId) : null;
                if (def == null) continue;
                trinkets.Add(new InventoryItemData
                {
                    ownedId = trinket.ownedTrinketId,
                    definitionId = trinket.trinketDefinitionId,
                    type = InventoryItemData.ItemType.Trinket,
                    name = def.name,
                    icon = trinketRegistry.GetIcon(trinket.trinketDefinitionId),
                    stats = def.statModifiers
                });
            }
        }

        weapons.Sort((a, b) => a.definitionId.CompareTo(b.definitionId));
        trinkets.Sort((a, b) => a.definitionId.CompareTo(b.definitionId));
        currentPage = 0;
        RenderLists();
        ShowWelcome();
    }

    void RenderLists()
    {
        int pages = TotalPages();
        currentPage = Mathf.Clamp(currentPage, 0, Mathf.Max(0, pages - 1));
        FillPage(weaponListContainer, weapons);
        FillPage(trinketListContainer, trinkets);

        if (pageLabel != null)
            pageLabel.text = pages > 0 ? $"Page {currentPage + 1} / {pages}" : string.Empty;
        if (prevButton != null)
            prevButton.interactable = currentPage > 0;
        if (nextButton != null)
            nextButton.interactable = currentPage < pages - 1;
    }

    int TotalPages()
    {
        int perPage = Mathf.Max(1, itemsPerPage);
        int weaponPages = Mathf.CeilToInt(weapons.Count / (float)perPage);
        int trinketPages = Mathf.CeilToInt(trinkets.Count / (float)perPage);
        return Mathf.Max(weaponPages, trinketPages);
    }

    void FillPage(Transform container, List<InventoryItemData> source)
    {
        if (container == null) return;
        for (int i = container.childCount - 1; i >= 0; i--)
            Destroy(container.GetChild(i).gameObject);

        int perPage = Mathf.Max(1, itemsPerPage);
        int start = currentPage * perPage;
        int end = Mathf.Min(start + perPage, source.Count);
        for (int i = start; i < end; i++)
        {
            if (inventoryItemPrefab == null) return;
            var itemObj = Instantiate(inventoryItemPrefab, container);
            var view = itemObj.GetComponent<InventoryItem>();
            if (view == null) continue;
            view.Setup(source[i], this);
            if (selectedData != null && source[i].ownedId == selectedData.ownedId)
            {
                selectedItem = view;
                view.SetSelected(true);
            }
        }
    }

    void PrevPage()
    {
        if (currentPage <= 0) return;
        currentPage--;
        RenderLists();
    }

    void NextPage()
    {
        if (currentPage >= TotalPages() - 1) return;
        currentPage++;
        RenderLists();
    }

    public void OnItemSelected(InventoryItem item, InventoryItemData itemData)
    {
        if (selectedItem != null)
            selectedItem.SetSelected(false);

        selectedItem = item;
        selectedData = itemData;
        selectedItem.SetSelected(true);
        ShowNameType(itemData);
    }

    public void OnClickShowStats()
    {
        if (selectedData == null) return;

        if (detailStatText != null)
            detailStatText.text = DescribeStats(selectedData);

        RiseOpen(statPanel);
    }

    public void OnClickCloseNameType()
    {
        FadeClose(nameTypePanel);
        FadeClose(statPanel);
        RiseOpen(commandPanel);
    }

    public void OnClickCloseStat()
    {
        FadeClose(statPanel);
    }

    void ShowWelcome()
    {
        bool empty = weapons.Count == 0 && trinkets.Count == 0;
        if (commandText != null)
            commandText.text = empty ? WelcomeEmpty : WelcomeHasItems;

        HideDetailImmediate(nameTypePanel);
        HideDetailImmediate(statPanel);
        RiseOpen(commandPanel);
    }

    void ShowNameType(InventoryItemData itemData)
    {
        if (detailIcon != null)
        {
            detailIcon.sprite = itemData.icon;
            detailIcon.enabled = itemData.icon != null;
        }
        if (descriptionText != null)
            descriptionText.text = $"This is {itemData.name}. {DescribeItem(itemData)}";

        RiseClose(commandPanel);
        HideDetailImmediate(statPanel);
        RiseOpen(nameTypePanel);
    }

    void RiseClose(GameObject target)
    {
        if (target == null) return;
        var anim = target.GetComponent<PanelAnimator>();
        if (anim != null && !anim.IsVisible) return;
        if (anim != null)
        {
            anim.openAnimation = PanelAnimator.AnimationType.Rise;
            anim.closeAnimation = PanelAnimator.AnimationType.Rise;
        }
        this.ClosePanel(target);
    }

    static string DescribeItem(InventoryItemData item)
    {
        if (item.type == InventoryItemData.ItemType.Weapon &&
            WeaponBlurbs.TryGetValue(item.definitionId, out var weaponLine))
            return weaponLine;
        if (item.type == InventoryItemData.ItemType.Trinket &&
            TrinketBlurbs.TryGetValue(item.definitionId, out var trinketLine))
            return trinketLine;
        return "It doesn't have a story written down yet.";
    }

    static string DescribeStats(InventoryItemData item)
    {
        var stats = item.stats;
        if (stats == null)
            return $"This is {item.name}'s Stat: nothing readable.";

        return $"This is {item.name}'s Stat: HP {stats.maxHP}, Speed {stats.speed}, Skill Point {stats.maxSkillPoint}, Damage {stats.damageMultiplier}, Defense {stats.damageReduction}.";
    }

    void FadeClose(GameObject target)
    {
        if (target == null) return;
        var anim = target.GetComponent<PanelAnimator>();
        if (anim != null && !anim.IsVisible) return;
        if (anim != null)
            anim.closeAnimation = PanelAnimator.AnimationType.Fade;
        this.ClosePanel(target);
    }

    static readonly Dictionary<int, string> WeaponBlurbs = new()
    {
        { 1, "A close thrust to the side or downward." },
        { 2, "A slash to the side or upward." },
        { 3, "A swing into the cells on either side." },
        { 4, "A shot two cells straight out." },
        { 5, "Heals a nearby ally." },
        { 6, "Throws thorns along the side." },
        { 7, "A sharper thrust to the side or downward." },
        { 8, "A guarded slash to the side or upward." },
        { 9, "A heavy swing into the cells on either side." },
        { 10, "A cleaner shot, two cells straight out." },
        { 11, "Heals a nearby ally." },
        { 12, "Throws thorns along the side." }
    };

    static readonly Dictionary<int, string> TrinketBlurbs = new()
    {
        { 1, "Extends your movement range." },
        { 2, "Extends your vertical range." },
        { 3, "Extends your movement range." },
        { 4, "Extends your horizontal range." },
        { 5, "Extends your movement range." },
        { 6, "Extends your vertical range." },
        { 7, "Extends your movement range." },
        { 8, "Extends your horizontal range." }
    };

    void RiseOpen(GameObject target)
    {
        if (target == null) return;
        var anim = target.GetComponent<PanelAnimator>();
        if (anim != null)
        {
            anim.openAnimation = PanelAnimator.AnimationType.Rise;
            anim.closeAnimation = PanelAnimator.AnimationType.Rise;
        }
        this.OpenPanel(target);
    }

    void HideDetailImmediate(GameObject target)
    {
        this.HidePanelImmediate(target);
    }
}
