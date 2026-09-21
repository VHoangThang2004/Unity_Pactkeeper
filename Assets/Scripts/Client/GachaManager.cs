using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.Networking;
using TMPro;

public class GachaManager : MonoBehaviour
{
    [Header("Config")]
    [SerializeField] public SceneConfig sceneConfig;
    [SerializeField] public BackendConfig clientBackendConfig;
    [SerializeField] public UnitPrefabRegistry unitPrefabRegistry;
    [SerializeField] public ClassDefinitionRegistry classLibrary;
    [SerializeField] public WeaponDefinitionRegistry weaponRegistry;
    [SerializeField] public TrinketDefinitionRegistry trinketRegistry;

    [Header("Loading")]
    [SerializeField] public LoadingScreen loadingScreen;

    [Header("UI Display")]
    [SerializeField] private TextMeshProUGUI gemsText; // Wired in editor

    [Header("Formation (repurposed for Featured Characters)")]
    [SerializeField] public List<UnitContainer> formationSlots;

    [Header("Banners List (repurposed from Unit List)")]
    [SerializeField] public Transform unitListContainer; // Banners will spawn here
    [SerializeField] public GameObject unitContainerPrefab; // Reuse unit item prefab for banners and results

    [Header("Details Panel References (Wired in Editor)")]
    [SerializeField] public Image bannerPromotionalImage;
    [SerializeField] public TMP_Text bannerTitleText;
    [SerializeField] public TMP_Text bannerDescText;
    [SerializeField] public Button dropRatesButton;
    [SerializeField] public Button roll1Button;
    [SerializeField] public Button roll10Button;

    [Header("Rates Popup (Wired in Editor)")]
    [SerializeField] public GameObject ratesPanel;
    [SerializeField] public TMP_Text ratesText;
    [SerializeField] public Button ratesCloseButton;

    [Header("Results Popup (Wired in Editor)")]
    [SerializeField] public GameObject resultsPanel;
    [SerializeField] public Transform resultsContainer;
    [SerializeField] public Button resultsCloseButton;

    [Header("Error Popup (Wired in Editor)")]
    [SerializeField] public GameObject errorPanel;
    [SerializeField] public TMP_Text errorText;
    [SerializeField] public Button errorCloseButton;

    [Header("Custom Banner Settings")]
    [SerializeField] public GachaBannerRegistry bannerRegistry;
    [SerializeField] public GameObject bannerButtonPrefab;

    private List<GachaBannerDto> activeBanners = new List<GachaBannerDto>();
    private GachaBannerDto selectedBanner;
    private TMP_FontAsset gameFont;

    private List<WeaponDefinitionDto> weaponDefinitions = new List<WeaponDefinitionDto>();
    private List<TrinketDefinitionDto> trinketDefinitions = new List<TrinketDefinitionDto>();

    void Start()
    {
        // Initialize registries
        if (unitPrefabRegistry != null) unitPrefabRegistry.Init();
        if (classLibrary != null) classLibrary.Init();
        if (weaponRegistry != null) weaponRegistry.Init();
        if (trinketRegistry != null) trinketRegistry.Init();
        if (bannerRegistry != null) bannerRegistry.Init();

        // Try to capture font asset from the scene
        var existingText = FindAnyObjectByType<TMP_Text>();
        if (existingText != null) gameFont = existingText.font;

        // Setup UI panels dynamically as a fallback if not wired in Inspector
        SetupFallbacksIfNeeded();

        // Start initialization sequence
        StartCoroutine(InitSequence());
    }

    void SetupFallbacksIfNeeded()
    {
        if (bannerTitleText == null || bannerDescText == null || roll1Button == null || roll10Button == null)
        {
            Debug.Log("[GachaManager] Details Panel UI fields missing, setting up dynamically.");
            SetupDetailsPanel();
        }
        else
        {
            roll1Button.onClick.RemoveAllListeners();
            roll1Button.onClick.AddListener(OnClickRoll1);
            roll10Button.onClick.RemoveAllListeners();
            roll10Button.onClick.AddListener(OnClickRoll10);
            if (dropRatesButton != null)
            {
                dropRatesButton.onClick.RemoveAllListeners();
                dropRatesButton.onClick.AddListener(OnClickShowRates);
            }
        }

        if (ratesPanel == null)
        {
            Debug.Log("[GachaManager] Rates Panel missing, setting up dynamically.");
            SetupRatesPopup();
        }
        else if (ratesCloseButton != null)
        {
            ratesCloseButton.onClick.RemoveAllListeners();
            ratesCloseButton.onClick.AddListener(OnClickCloseRates);
        }

        if (resultsPanel == null)
        {
            Debug.Log("[GachaManager] Results Panel missing, setting up dynamically.");
            SetupResultsPopup();
        }
        else if (resultsCloseButton != null)
        {
            resultsCloseButton.onClick.RemoveAllListeners();
            resultsCloseButton.onClick.AddListener(OnClickCloseResults);
        }

        if (errorPanel == null)
        {
            Debug.Log("[GachaManager] Error Panel missing, setting up dynamically.");
            SetupErrorPopup();
        }
        else if (errorCloseButton != null)
        {
            errorCloseButton.onClick.RemoveAllListeners();
            errorCloseButton.onClick.AddListener(OnClickCloseError);
        }
    }

    IEnumerator InitSequence()
    {
        loadingScreen?.Show();

        // 1. Fetch Weapon and Trinket definitions from the backend catalog
        yield return StartCoroutine(FetchCatalogDefinitions());

        // 2. Fetch Player Profile to render starting gems balance
        yield return StartCoroutine(FetchPlayerProfile());

        // 3. Fetch Active Summons Banners
        yield return StartCoroutine(FetchActiveBanners());

        // 4. Render Banners
        PopulateBannersList();

        // 5. Select Default Banner
        if (activeBanners.Count > 0)
        {
            SelectBanner(activeBanners[0]);
            loadingScreen?.Hide();
        }
    }

    IEnumerator FetchCatalogDefinitions()
    {
        using (var req = UnityWebRequest.Get($"{clientBackendConfig.backendUrl}/api/WeaponDefinition"))
        {
            clientBackendConfig.SetHeaders(req, PlayerSession.Token);
            yield return req.SendWebRequest();
            if (req.result == UnityWebRequest.Result.Success)
            {
                var wrapper = JsonUtility.FromJson<WeaponDefinitionWrapper>("{\"items\":" + req.downloadHandler.text + "}");
                if (wrapper != null && wrapper.items != null)
                    weaponDefinitions = new List<WeaponDefinitionDto>(wrapper.items);
            }
        }

        using (var req = UnityWebRequest.Get($"{clientBackendConfig.backendUrl}/api/TrinketDefinition"))
        {
            clientBackendConfig.SetHeaders(req, PlayerSession.Token);
            yield return req.SendWebRequest();
            if (req.result == UnityWebRequest.Result.Success)
            {
                var wrapper = JsonUtility.FromJson<TrinketDefinitionWrapper>("{\"items\":" + req.downloadHandler.text + "}");
                if (wrapper != null && wrapper.items != null)
                    trinketDefinitions = new List<TrinketDefinitionDto>(wrapper.items);
            }
        }
    }

    [System.Serializable] private class WeaponDefinitionWrapper { public WeaponDefinitionDto[] items; }
    [System.Serializable] private class TrinketDefinitionWrapper { public TrinketDefinitionDto[] items; }

    IEnumerator FetchPlayerProfile()
    {
        using var request = UnityWebRequest.Get($"{clientBackendConfig.backendUrl}/api/PlayerProfile");
        clientBackendConfig.SetHeaders(request, PlayerSession.Token);
        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError($"[Gacha] FetchPlayerProfile failed: {request.error}");
            ShowErrorOverlay("Error: Failed to connect to server. Check your connection.");
            yield break;
        }

        var profile = JsonUtility.FromJson<PlayerProfileResponse>(request.downloadHandler.text);
        UpdateGemsDisplay(profile.gems);
    }

    IEnumerator FetchActiveBanners()
    {
        using var request = UnityWebRequest.Get($"{clientBackendConfig.backendUrl}/api/gachabanner/active");
        clientBackendConfig.SetHeaders(request, PlayerSession.Token);
        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError($"[Gacha] FetchActiveBanners failed: {request.error}");
            ShowErrorOverlay("Error: Failed to retrieve active Gacha banners.");
            yield break;
        }

        var wrapper = JsonUtility.FromJson<GachaBannersWrapper>("{\"items\":" + request.downloadHandler.text + "}");
        activeBanners = wrapper != null && wrapper.items != null ? new List<GachaBannerDto>(wrapper.items) : new List<GachaBannerDto>();

        if (activeBanners.Count == 0)
        {
            ShowErrorOverlay("There are currently no active summon banners.");
        }
    }

    void UpdateGemsDisplay(int gems)
    {
        if (gemsText != null)
        {
            gemsText.text = gems.ToString();
        }
    }

    void ShowErrorOverlay(string message)
    {
        loadingScreen?.Show(message);
    }

    void PopulateBannersList()
    {
        if (unitListContainer == null) return;

        foreach (Transform child in unitListContainer)
        {
            Destroy(child.gameObject);
        }

        for (int i = 0; i < activeBanners.Count; i++)
        {
            var banner = activeBanners[i];
            
            // Determine which prefab to use (custom banner button vs original unit card container)
            GameObject usePrefab = (bannerButtonPrefab != null) ? bannerButtonPrefab : unitContainerPrefab;
            GameObject itemGo = Instantiate(usePrefab, unitListContainer);
            itemGo.name = $"BannerItem_{i}";

            GachaBannerButton bannerBtn = itemGo.GetComponent<GachaBannerButton>();
            if (bannerBtn != null && bannerRegistry != null)
            {
                var art = bannerRegistry.GetArt(banner.id, banner.name);
                bannerBtn.Setup(art?.listButtonArt);
                
                Button btn = bannerBtn.button;
                if (btn == null) btn = itemGo.GetComponent<Button>();
                if (btn != null)
                {
                    btn.onClick.RemoveAllListeners();
                    btn.onClick.AddListener(() => SelectBanner(banner));
                }
            }
            else
            {
                UnitContainer container = itemGo.GetComponent<UnitContainer>();
                if (container != null)
                {
                    Sprite bannerIcon = null;
                    var featured = System.Array.Find(banner.items, x => x.isFeatured);
                    if (featured == null && banner.items.Length > 0) featured = banner.items[0];
                    if (featured != null)
                    {
                        bannerIcon = GetRewardIcon(featured.reward);
                    }

                    container.Setup(
                        banner.id,
                        i,
                        bannerIcon,
                        banner.name,
                        null,
                        null,
                        false
                    );
                }

                Button btn = itemGo.GetComponent<Button>();
                if (btn == null) btn = itemGo.GetComponentInChildren<Button>();
                if (btn != null)
                {
                    btn.onClick.RemoveAllListeners();
                    btn.onClick.AddListener(() => SelectBanner(banner));
                }
            }
        }
    }

    public void SelectBanner(GachaBannerDto banner)
    {
        selectedBanner = banner;
        if (selectedBanner == null) return;

        Debug.Log($"[GachaManager] Selected Banner: {banner.name}");

        if (bannerTitleText != null) bannerTitleText.text = banner.name;
        if (bannerDescText != null) bannerDescText.text = banner.description;

        if (bannerPromotionalImage != null)
        {
            Sprite customPromoSprite = null;
            if (bannerRegistry != null)
            {
                var art = bannerRegistry.GetArt(banner.id, banner.name);
                if (art != null) customPromoSprite = art.Value.promoArt;
            }

            if (customPromoSprite != null)
            {
                bannerPromotionalImage.sprite = customPromoSprite;
                bannerPromotionalImage.color = Color.white;
            }
            else
            {
                var featured = System.Array.Find(banner.items, x => x.isFeatured);
                if (featured == null && banner.items.Length > 0) featured = banner.items[0];
                if (featured != null)
                {
                    bannerPromotionalImage.sprite = GetRewardIcon(featured.reward);
                    bannerPromotionalImage.color = Color.white;
                }
                else
                {
                    bannerPromotionalImage.sprite = null;
                    bannerPromotionalImage.color = new Color(0.2f, 0.2f, 0.25f, 1f);
                }
            }
        }

        PopulateFeaturedSlots(banner);
    }

    void PopulateFeaturedSlots(GachaBannerDto banner)
    {
        if (formationSlots == null) return;

        List<BannerItemDto> featuredUnits = new List<BannerItemDto>();
        foreach (var item in banner.items)
        {
            if (item.reward.type == "Unit" && item.isFeatured)
            {
                featuredUnits.Add(item);
            }
        }

        if (featuredUnits.Count == 0)
        {
            foreach (var item in banner.items)
            {
                if (item.reward.type == "Unit")
                {
                    featuredUnits.Add(item);
                }
            }
        }

        for (int i = 0; i < formationSlots.Count; i++)
        {
            var slot = formationSlots[i];
            if (slot == null) continue;

            if (i < featuredUnits.Count)
            {
                var item = featuredUnits[i];
                string name = GetRewardName(item.reward);
                Sprite portrait = GetRewardIcon(item.reward);

                slot.gameObject.SetActive(true);
                slot.Setup(
                    $"featured_{item.reward.definitionId}",
                    item.reward.definitionId,
                    portrait,
                    name + "\n(Featured)",
                    null,
                    null,
                    true
                );
                slot.ShowDefault();
            }
            else
            {
                slot.gameObject.SetActive(false);
            }
        }
    }

    Sprite GetRewardIcon(RewardDto reward)
    {
        if (reward == null) return null;
        switch (reward.type)
        {
            case "Unit":
                if (unitPrefabRegistry != null)
                {
                    GameObject unitPrefab = unitPrefabRegistry.Get(reward.definitionId);
                    return unitPrefab?.GetComponent<ClientUnit>()?.unitImg;
                }
                break;
            case "Weapon":
                if (weaponRegistry != null)
                {
                    return weaponRegistry.GetIcon(reward.definitionId);
                }
                break;
            case "Trinket":
                if (trinketRegistry != null)
                {
                    return trinketRegistry.GetIcon(reward.definitionId);
                }
                break;
            case "Gems":
                var existingBtn = FindAnyObjectByType<Button>();
                if (existingBtn != null)
                {
                    var img = existingBtn.GetComponent<Image>();
                    if (img != null) return img.sprite;
                }
                break;
        }
        return null;
    }

    string GetRewardName(RewardDto reward)
    {
        if (reward == null) return "Unknown";
        switch (reward.type)
        {
            case "Unit":
                return unitPrefabRegistry != null ? unitPrefabRegistry.GetName(reward.definitionId) : $"Unit {reward.definitionId}";
            case "Weapon":
                var wDef = weaponDefinitions.Find(d => d.weaponId == reward.definitionId);
                return wDef != null ? wDef.name : $"Weapon {reward.definitionId}";
            case "Trinket":
                var tDef = trinketDefinitions.Find(d => d.trinketId == reward.definitionId);
                return tDef != null ? tDef.name : $"Trinket {reward.definitionId}";
            case "Gems":
                return $"{reward.amount} Gems";
        }
        return "Unknown";
    }

    public void OnClickRoll1()
    {
        if (selectedBanner == null) return;
        StartCoroutine(SendPullRequest("Single"));
    }

    public void OnClickRoll10()
    {
        if (selectedBanner == null) return;
        StartCoroutine(SendPullRequest("Ten"));
    }

    IEnumerator SendPullRequest(string pullType)
    {
        loadingScreen?.Show("Summoning...");

        var dto = new GachaPullRequestDto { bannerId = selectedBanner.id, pullType = pullType };
        string body = JsonUtility.ToJson(dto);
        byte[] bodyBytes = System.Text.Encoding.UTF8.GetBytes(body);

        using (var request = new UnityWebRequest($"{clientBackendConfig.backendUrl}/api/gacha/pull", "POST"))
        {
            request.uploadHandler = new UploadHandlerRaw(bodyBytes);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            clientBackendConfig.SetHeaders(request, PlayerSession.Token);

            yield return request.SendWebRequest();

            loadingScreen?.Hide();

            if (request.result != UnityWebRequest.Result.Success)
            {
                string errMsg = "Summon failed.";
                try
                {
                    var errorObj = JsonUtility.FromJson<ErrorResponse>(request.downloadHandler.text);
                    if (errorObj != null && !string.IsNullOrEmpty(errorObj.message))
                        errMsg = errorObj.message;
                }
                catch {}
                
                Debug.LogError($"[Gacha] Pull failed: {request.error} | Response: {request.downloadHandler.text}");
                ShowErrorPopup(errMsg);
                yield break;
            }

            var response = JsonUtility.FromJson<GachaPullResponseDto>(request.downloadHandler.text);
            UpdateGemsDisplay(response.gemsRemaining);
            DisplayRollResults(response.results);
        }
    }

    [System.Serializable]
    private class ErrorResponse { public string message; }

    private void DisplayRollResults(GachaPullResultItemDto[] results)
    {
        if (resultsContainer == null || resultsPanel == null) return;

        foreach (Transform child in resultsContainer)
        {
            Destroy(child.gameObject);
        }

        foreach (var result in results)
        {
            GameObject cardGo = Instantiate(unitContainerPrefab, resultsContainer);
            UnitContainer container = cardGo.GetComponent<UnitContainer>();
            if (container != null)
            {
                string name = GetRewardName(result.reward);
                if (result.isDuplicate)
                {
                    name += $"\n(Duplicate)\n+{result.compensationGems} Gems";
                }
                Sprite portrait = GetRewardIcon(result.reward);

                container.Setup(
                    $"rolled_{result.reward.definitionId}_{System.Guid.NewGuid().ToString().Substring(0,4)}",
                    result.reward.definitionId,
                    portrait,
                    name,
                    null,
                    null,
                    false
                );
                container.ShowDefault();
            }

            Button btn = cardGo.GetComponent<Button>();
            if (btn == null) btn = cardGo.GetComponentInChildren<Button>();
            if (btn != null)
            {
                btn.onClick.RemoveAllListeners();
            }
        }

        resultsPanel.SetActive(true);
    }

    public void OnClickShowRates()
    {
        if (selectedBanner == null) return;
        StartCoroutine(FetchDropRates(selectedBanner.id));
    }

    IEnumerator FetchDropRates(string bannerId)
    {
        loadingScreen?.Show("Fetching drop rates...");

        using (var request = UnityWebRequest.Get($"{clientBackendConfig.backendUrl}/api/gachabanner/{bannerId}/droprates"))
        {
            clientBackendConfig.SetHeaders(request, PlayerSession.Token);
            yield return request.SendWebRequest();

            loadingScreen?.Hide();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[Gacha] FetchDropRates failed: {request.error}");
                yield break;
            }

            var ratesDto = JsonUtility.FromJson<BannerDropRatesDto>(request.downloadHandler.text);
            
            string desc = $"{ratesDto.bannerName.ToUpper()} DROP RATES:\n\n";
            foreach (var item in ratesDto.items)
            {
                string typeLabel = item.reward.type;
                string name = GetRewardName(item.reward);
                string featuredLabel = item.isFeatured ? " [Featured]" : "";
                desc += $"★ {name} ({typeLabel}){featuredLabel}: {item.dropRate:0.00}%\n";
            }

            if (ratesText != null) ratesText.text = desc;
            if (ratesPanel != null) ratesPanel.SetActive(true);
        }
    }

    public void OnClickCloseRates()
    {
        if (ratesPanel != null)
        {
            ratesPanel.SetActive(false);
        }
    }

    public void OnClickCloseResults()
    {
        if (resultsPanel != null)
        {
            resultsPanel.SetActive(false);
        }
    }

    public void OnClickReturnMainMenu()
    {
        if (sceneConfig == null)
        {
            SceneManager.LoadScene("3_MainMenu");
        }
        else
        {
            SceneManager.LoadScene(sceneConfig.mainMenuScene);
        }
    }

    // -------------------------------------------------------
    // Dynamic Fallback UI Creation
    // -------------------------------------------------------

    void SetupDetailsPanel()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null) canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null) return;

        GameObject detailPanelGo = new GameObject("GachaDetailsPanel", typeof(RectTransform));
        detailPanelGo.transform.SetParent(canvas.transform, false);
        RectTransform rt = detailPanelGo.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(-150f, 60f);
        rt.sizeDelta = new Vector2(900f, 500f);

        Image bgImage = detailPanelGo.AddComponent<Image>();
        bgImage.color = new Color(0.12f, 0.12f, 0.16f, 0.95f);

        GameObject promoGo = new GameObject("PromoImage", typeof(RectTransform));
        promoGo.transform.SetParent(detailPanelGo.transform, false);
        RectTransform promoRt = promoGo.GetComponent<RectTransform>();
        promoRt.anchorMin = new Vector2(0f, 0.5f);
        promoRt.anchorMax = new Vector2(0f, 0.5f);
        promoRt.anchoredPosition = new Vector2(200f, 0f);
        promoRt.sizeDelta = new Vector2(350f, 400f);
        bannerPromotionalImage = promoGo.AddComponent<Image>();
        bannerPromotionalImage.color = new Color(0.2f, 0.2f, 0.25f, 1f);

        GameObject titleGo = new GameObject("BannerTitle", typeof(RectTransform));
        titleGo.transform.SetParent(detailPanelGo.transform, false);
        RectTransform titleRt = titleGo.GetComponent<RectTransform>();
        titleRt.anchorMin = new Vector2(0.5f, 1f);
        titleRt.anchorMax = new Vector2(0.5f, 1f);
        titleRt.anchoredPosition = new Vector2(180f, -60f);
        titleRt.sizeDelta = new Vector2(480f, 50f);
        bannerTitleText = titleGo.AddComponent<TextMeshProUGUI>();
        if (gameFont != null) bannerTitleText.font = gameFont;
        bannerTitleText.fontSize = 32;
        bannerTitleText.alignment = TextAlignmentOptions.Left;
        bannerTitleText.color = Color.yellow;

        GameObject descGo = new GameObject("BannerDesc", typeof(RectTransform));
        descGo.transform.SetParent(detailPanelGo.transform, false);
        RectTransform descRt = descGo.GetComponent<RectTransform>();
        descRt.anchorMin = new Vector2(0.5f, 1f);
        descRt.anchorMax = new Vector2(0.5f, 1f);
        descRt.anchoredPosition = new Vector2(180f, -170f);
        descRt.sizeDelta = new Vector2(480f, 120f);
        bannerDescText = descGo.AddComponent<TextMeshProUGUI>();
        if (gameFont != null) bannerDescText.font = gameFont;
        bannerDescText.fontSize = 20;
        bannerDescText.alignment = TextAlignmentOptions.TopLeft;
        bannerDescText.color = Color.white;

        Sprite buttonSprite = null;
        var existingButton = FindAnyObjectByType<Button>();
        if (existingButton != null)
        {
            var img = existingButton.GetComponent<Image>();
            if (img != null) buttonSprite = img.sprite;
        }

        GameObject roll1Go = CreateUIButton("Roll1Button", detailPanelGo.transform, "Roll 1 Time", new Vector2(480f, -380f), new Vector2(200f, 60f), buttonSprite);
        roll1Button = roll1Go.GetComponent<Button>();
        roll1Button.onClick.AddListener(OnClickRoll1);
        roll1Button.GetComponent<Image>().color = new Color(0.18f, 0.48f, 0.76f);

        GameObject roll10Go = CreateUIButton("Roll10Button", detailPanelGo.transform, "Roll 10 Times", new Vector2(700f, -380f), new Vector2(200f, 60f), buttonSprite);
        roll10Button = roll10Go.GetComponent<Button>();
        roll10Button.onClick.AddListener(OnClickRoll10);
        roll10Button.GetComponent<Image>().color = new Color(0.78f, 0.56f, 0.16f);

        GameObject rateGo = CreateUIButton("RatesButton", detailPanelGo.transform, "Drop Rates", new Vector2(480f, -270f), new Vector2(150f, 40f), buttonSprite);
        dropRatesButton = rateGo.GetComponent<Button>();
        dropRatesButton.onClick.AddListener(OnClickShowRates);
        dropRatesButton.GetComponent<Image>().color = new Color(0.35f, 0.35f, 0.45f);
        var rateText = rateGo.GetComponentInChildren<TMP_Text>();
        if (rateText != null) rateText.fontSize = 16;
    }

    GameObject CreateUIButton(string name, Transform parent, string text, Vector2 anchoredPos, Vector2 size, Sprite sprite)
    {
        GameObject btnGo = new GameObject(name, typeof(RectTransform));
        btnGo.transform.SetParent(parent, false);
        RectTransform rt = btnGo.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;

        Image img = btnGo.AddComponent<Image>();
        img.sprite = sprite;
        img.type = Image.Type.Sliced;

        Button btn = btnGo.AddComponent<Button>();

        GameObject txtGo = new GameObject("Text", typeof(RectTransform));
        txtGo.transform.SetParent(btnGo.transform, false);
        RectTransform txtRt = txtGo.GetComponent<RectTransform>();
        txtRt.anchorMin = Vector2.zero;
        txtRt.anchorMax = Vector2.one;
        txtRt.sizeDelta = Vector2.zero;

        var tmpText = txtGo.AddComponent<TextMeshProUGUI>();
        if (gameFont != null) tmpText.font = gameFont;
        tmpText.text = text;
        tmpText.fontSize = 20;
        tmpText.color = Color.white;
        tmpText.alignment = TextAlignmentOptions.Center;

        return btnGo;
    }

    void SetupRatesPopup()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null) canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null) return;

        ratesPanel = new GameObject("GachaRatesPanel", typeof(RectTransform));
        ratesPanel.transform.SetParent(canvas.transform, false);
        RectTransform rt = ratesPanel.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.sizeDelta = Vector2.zero;
        ratesPanel.SetActive(false);

        Image dim = ratesPanel.AddComponent<Image>();
        dim.color = new Color(0f, 0f, 0f, 0.8f);

        GameObject box = new GameObject("Box", typeof(RectTransform));
        box.transform.SetParent(ratesPanel.transform, false);
        RectTransform boxRt = box.GetComponent<RectTransform>();
        boxRt.anchorMin = new Vector2(0.5f, 0.5f);
        boxRt.anchorMax = new Vector2(0.5f, 0.5f);
        boxRt.sizeDelta = new Vector2(600f, 500f);
        Image boxImg = box.AddComponent<Image>();
        boxImg.color = new Color(0.18f, 0.18f, 0.22f, 1f);

        GameObject titleGo = new GameObject("Title", typeof(RectTransform));
        titleGo.transform.SetParent(box.transform, false);
        RectTransform titleRt = titleGo.GetComponent<RectTransform>();
        titleRt.anchorMin = new Vector2(0.5f, 1f);
        titleRt.anchorMax = new Vector2(0.5f, 1f);
        titleRt.anchoredPosition = new Vector2(0f, -30f);
        titleRt.sizeDelta = new Vector2(500f, 40f);
        var titleText = titleGo.AddComponent<TextMeshProUGUI>();
        if (gameFont != null) titleText.font = gameFont;
        titleText.text = "BANNER DROP RATES";
        titleText.fontSize = 24;
        titleText.color = Color.yellow;
        titleText.alignment = TextAlignmentOptions.Center;

        // Create Scroll View container
        GameObject scrollViewGo = new GameObject("RatesScrollView", typeof(RectTransform));
        scrollViewGo.transform.SetParent(box.transform, false);
        RectTransform scrollRt = scrollViewGo.GetComponent<RectTransform>();
        scrollRt.anchorMin = Vector2.zero;
        scrollRt.anchorMax = Vector2.one;
        scrollRt.offsetMin = new Vector2(30f, 100f); // Leave space at bottom for close button
        scrollRt.offsetMax = new Vector2(-30f, -80f); // Leave space at top for title

        ScrollRect scrollRect = scrollViewGo.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;

        // Create Viewport
        GameObject viewportGo = new GameObject("Viewport", typeof(RectTransform));
        viewportGo.transform.SetParent(scrollViewGo.transform, false);
        RectTransform viewRt = viewportGo.GetComponent<RectTransform>();
        viewRt.anchorMin = Vector2.zero;
        viewRt.anchorMax = Vector2.one;
        viewRt.sizeDelta = Vector2.zero;
        
        viewportGo.AddComponent<Image>().color = new Color(0, 0, 0, 0.1f); // Subtle background
        viewportGo.AddComponent<Mask>().showMaskGraphic = false;

        // Create Content
        GameObject contentGo = new GameObject("Content", typeof(RectTransform));
        contentGo.transform.SetParent(viewportGo.transform, false);
        RectTransform contentRt = contentGo.GetComponent<RectTransform>();
        contentRt.anchorMin = new Vector2(0f, 1f);
        contentRt.anchorMax = new Vector2(1f, 1f);
        contentRt.pivot = new Vector2(0.5f, 1f);
        contentRt.anchoredPosition = Vector2.zero;
        contentRt.sizeDelta = new Vector2(0f, 320f); // Adjust height dynamically via size fitter

        ratesText = contentGo.AddComponent<TextMeshProUGUI>();
        if (gameFont != null) ratesText.font = gameFont;
        ratesText.fontSize = 18;
        ratesText.color = Color.white;
        ratesText.alignment = TextAlignmentOptions.TopLeft;

        // Add size fitter to Content so it updates height automatically based on text length
        ContentSizeFitter fitter = contentGo.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

        scrollRect.viewport = viewRt;
        scrollRect.content = contentRt;

        Sprite buttonSprite = null;
        var existingButton = FindAnyObjectByType<Button>();
        if (existingButton != null)
        {
            var img = existingButton.GetComponent<Image>();
            if (img != null) buttonSprite = img.sprite;
        }

        GameObject closeBtnGo = CreateUIButton("CloseButton", box.transform, "Close", new Vector2(0f, 40f), new Vector2(140f, 45f), buttonSprite);
        RectTransform closeRt = closeBtnGo.GetComponent<RectTransform>();
        closeRt.anchorMin = new Vector2(0.5f, 0f);
        closeRt.anchorMax = new Vector2(0.5f, 0f);
        closeRt.anchoredPosition = new Vector2(0f, 40f);

        ratesCloseButton = closeBtnGo.GetComponent<Button>();
        ratesCloseButton.onClick.AddListener(OnClickCloseRates);
        ratesCloseButton.GetComponent<Image>().color = new Color(0.6f, 0.2f, 0.2f);
    }

    void SetupResultsPopup()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null) canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null) return;

        resultsPanel = new GameObject("GachaResultsPanel", typeof(RectTransform));
        resultsPanel.transform.SetParent(canvas.transform, false);
        RectTransform rt = resultsPanel.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.sizeDelta = Vector2.zero;
        resultsPanel.SetActive(false);

        Image dim = resultsPanel.AddComponent<Image>();
        dim.color = new Color(0.05f, 0.05f, 0.08f, 0.98f);

        GameObject titleGo = new GameObject("Title", typeof(RectTransform));
        titleGo.transform.SetParent(resultsPanel.transform, false);
        RectTransform titleRt = titleGo.GetComponent<RectTransform>();
        titleRt.anchorMin = new Vector2(0.5f, 1f);
        titleRt.anchorMax = new Vector2(0.5f, 1f);
        titleRt.anchoredPosition = new Vector2(0f, -80f);
        titleRt.sizeDelta = new Vector2(800f, 60f);
        var titleText = titleGo.AddComponent<TextMeshProUGUI>();
        if (gameFont != null) titleText.font = gameFont;
        titleText.text = "RECRUITMENT RESULTS";
        titleText.fontSize = 42;
        titleText.color = Color.yellow;
        titleText.alignment = TextAlignmentOptions.Center;

        GameObject gridGo = new GameObject("ResultsGrid", typeof(RectTransform));
        gridGo.transform.SetParent(resultsPanel.transform, false);
        RectTransform gridRt = gridGo.GetComponent<RectTransform>();
        gridRt.anchorMin = new Vector2(0.5f, 0.5f);
        gridRt.anchorMax = new Vector2(0.5f, 0.5f);
        gridRt.anchoredPosition = new Vector2(0f, -30f);
        gridRt.sizeDelta = new Vector2(1400f, 600f);
        resultsContainer = gridGo.transform;

        var layout = gridGo.AddComponent<GridLayoutGroup>();
        layout.cellSize = new Vector2(220f, 270f);
        layout.spacing = new Vector2(25f, 30f);
        layout.childAlignment = TextAnchor.MiddleCenter;

        Sprite buttonSprite = null;
        var existingButton = FindAnyObjectByType<Button>();
        if (existingButton != null)
        {
            var img = existingButton.GetComponent<Image>();
            if (img != null) buttonSprite = img.sprite;
        }

        GameObject closeBtnGo = CreateUIButton("CloseButton", resultsPanel.transform, "Confirm", new Vector2(0f, 80f), new Vector2(220f, 60f), buttonSprite);
        RectTransform closeRt = closeBtnGo.GetComponent<RectTransform>();
        closeRt.anchorMin = new Vector2(0.5f, 0f);
        closeRt.anchorMax = new Vector2(0.5f, 0f);
        closeRt.anchoredPosition = new Vector2(0f, 80f);

        resultsCloseButton = closeBtnGo.GetComponent<Button>();
        resultsCloseButton.onClick.AddListener(OnClickCloseResults);
        resultsCloseButton.GetComponent<Image>().color = new Color(0.18f, 0.58f, 0.38f);
    }

    public void ShowErrorPopup(string message)
    {
        if (errorText != null) errorText.text = message;
        if (errorPanel != null) errorPanel.SetActive(true);
    }

    public void OnClickCloseError()
    {
        if (errorPanel != null) errorPanel.SetActive(false);
    }

    void SetupErrorPopup()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null) canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null) return;

        errorPanel = new GameObject("GachaErrorPanel", typeof(RectTransform));
        errorPanel.transform.SetParent(canvas.transform, false);
        RectTransform rt = errorPanel.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.sizeDelta = Vector2.zero;
        errorPanel.SetActive(false);

        Image dim = errorPanel.AddComponent<Image>();
        dim.color = new Color(0f, 0f, 0f, 0.8f);

        GameObject box = new GameObject("Box", typeof(RectTransform));
        box.transform.SetParent(errorPanel.transform, false);
        RectTransform boxRt = box.GetComponent<RectTransform>();
        boxRt.anchorMin = new Vector2(0.5f, 0.5f);
        boxRt.anchorMax = new Vector2(0.5f, 0.5f);
        boxRt.sizeDelta = new Vector2(500f, 300f);
        Image boxImg = box.AddComponent<Image>();
        boxImg.color = new Color(0.22f, 0.15f, 0.15f, 1f); // Dark red background tint

        GameObject titleGo = new GameObject("Title", typeof(RectTransform));
        titleGo.transform.SetParent(box.transform, false);
        RectTransform titleRt = titleGo.GetComponent<RectTransform>();
        titleRt.anchorMin = new Vector2(0.5f, 1f);
        titleRt.anchorMax = new Vector2(0.5f, 1f);
        titleRt.anchoredPosition = new Vector2(0f, -30f);
        titleRt.sizeDelta = new Vector2(400f, 40f);
        var titleText = titleGo.AddComponent<TextMeshProUGUI>();
        if (gameFont != null) titleText.font = gameFont;
        titleText.text = "NOTICE";
        titleText.fontSize = 24;
        titleText.color = Color.yellow;
        titleText.alignment = TextAlignmentOptions.Center;

        GameObject textGo = new GameObject("ContentText", typeof(RectTransform));
        textGo.transform.SetParent(box.transform, false);
        RectTransform textRt = textGo.GetComponent<RectTransform>();
        textRt.anchorMin = new Vector2(0.5f, 0.5f);
        textRt.anchorMax = new Vector2(0.5f, 0.5f);
        textRt.anchoredPosition = new Vector2(0f, -10f);
        textRt.sizeDelta = new Vector2(400f, 120f);
        errorText = textGo.AddComponent<TextMeshProUGUI>();
        if (gameFont != null) errorText.font = gameFont;
        errorText.fontSize = 18;
        errorText.color = Color.white;
        errorText.alignment = TextAlignmentOptions.Center;

        Sprite buttonSprite = null;
        var existingButton = FindAnyObjectByType<Button>();
        if (existingButton != null)
        {
            var img = existingButton.GetComponent<Image>();
            if (img != null) buttonSprite = img.sprite;
        }

        GameObject closeBtnGo = CreateUIButton("CloseButton", box.transform, "Close", new Vector2(0f, 40f), new Vector2(140f, 45f), buttonSprite);
        RectTransform closeRt = closeBtnGo.GetComponent<RectTransform>();
        closeRt.anchorMin = new Vector2(0.5f, 0f);
        closeRt.anchorMax = new Vector2(0.5f, 0f);
        closeRt.anchoredPosition = new Vector2(0f, 40f);

        errorCloseButton = closeBtnGo.GetComponent<Button>();
        errorCloseButton.onClick.AddListener(OnClickCloseError);
        errorCloseButton.GetComponent<Image>().color = new Color(0.6f, 0.2f, 0.2f);
    }
}
