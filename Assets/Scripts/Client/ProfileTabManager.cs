using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Overlay tab for player profile on the Main Menu.
/// Wire UI in the Inspector; panel starts hidden until Open() is called.
/// </summary>
public class ProfileTabManager : MonoBehaviour
{
    [Header("Config")]
    [SerializeField] private BackendConfig config;
    [SerializeField] private UnitPrefabRegistry unitPrefabRegistry;
    [SerializeField] private ClassDefinitionRegistry classLibrary;

    [Header("Panel")]
    [SerializeField] private GameObject panel;
    [SerializeField] private Button closeButton;

    // ── Identity ──────────────────────────────────────────────────────────────

    [Header("Identity")]
    [SerializeField] private Image    avatarImage;
    [SerializeField] private TMP_Text usernameText;
    [SerializeField] private TMP_Text uidText;         // "UID: abc123"
    [SerializeField] private TMP_Text levelText;       // "Lv.X"
    [SerializeField] private TMP_Text statusText;      // loading / error indicator

    [Header("Experience")]
    [SerializeField] private Slider   experienceSlider; // interactable = false
    [SerializeField] private TMP_Text experienceText;   // "X/100"
    [Tooltip("Max XP per level — set here if BE does not expose it.")]
    [SerializeField] private int      maxExperiencePerLevel = 100;

    [Header("Signature (optional — BE not yet implemented)")]
    [SerializeField] private TMP_InputField signatureInputField;
    [SerializeField] private Button         editProfileButton;

    // ── Collection counts ─────────────────────────────────────────────────────

    [Header("Collection counts (numbers only — labels are fixed on scene)")]
    [SerializeField] private TMP_Text gemsText;
    [SerializeField] private TMP_Text unitsCountText;
    [SerializeField] private TMP_Text weaponsCountText;
    [SerializeField] private TMP_Text trinketsCountText;

    [Header("Story (optional)")]
    [SerializeField] private TMP_Text storyProgressText;

    // ── Rank / Stats ──────────────────────────────────────────────────────────

    [Header("Rank")]
    [Tooltip("Sprites indexed by rankTier (0 = Unranked/Beginner, 1 = Master, ...). " +
             "Add entries here; index matches rankTier value from API.")]
    [SerializeField] private Sprite[] rankTierSprites;
    [SerializeField] private Image    rankTierImage;   // hiển thị sprite tương ứng
    [SerializeField] private TMP_Text ratingText;      // điểm rating

    [Header("Match Stats")]
    [SerializeField] private TMP_Text matchCountText;  // "000000 Matches"
    [SerializeField] private TMP_Text winrateText;     // "Winrate: 36%"

    // ── Formation preview ─────────────────────────────────────────────────────

    [Header("Formation preview (optional, max 5)")]
    [SerializeField] private List<Image>    formationPortraits;
    [SerializeField] private List<Image>    formationFrames;
    [SerializeField] private List<Image>    formationHalfFrames;
    [SerializeField] private List<TMP_Text> formationNames;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    void Awake()
    {
        if (closeButton != null)
            closeButton.onClick.AddListener(Close);

        // editProfileButton — placeholder, wire khi BE implement
        // if (editProfileButton != null)
        //     editProfileButton.onClick.AddListener(OnEditProfileClicked);

        HidePanel();
        ClearStatus();
    }

    void OnDestroy()
    {
        if (closeButton != null)
            closeButton.onClick.RemoveListener(Close);
    }

    // ── Public API ────────────────────────────────────────────────────────────

    public void Open()
    {
        this.OpenPanel(panel);
        ClearStatus();
        StartCoroutine(LoadProfileTab());
    }

    public void Close()
    {
        this.ClosePanel(panel);
    }

    // ── Data loading ──────────────────────────────────────────────────────────

    IEnumerator LoadProfileTab()
    {
        if (config == null) { SetStatus("Failed to load profile"); yield break; }

        SetStatus("Loading...");

        PlayerProfileResponse profile = null;
        yield return StartCoroutine(FetchProfile(p => profile = p));

        if (profile == null) { SetStatus("Failed to load profile"); yield break; }

        BindProfile(profile);

        List<int> formationUIds = new();
        yield return StartCoroutine(FetchFormation(formationUIds));
        BindFormation(profile, formationUIds);

        yield return StartCoroutine(FetchStoryProgress());

        ClearStatus();
    }

    IEnumerator FetchProfile(System.Action<PlayerProfileResponse> onSuccess)
    {
        using var request = UnityWebRequest.Get($"{config.backendUrl}/api/PlayerProfile");
        config.SetHeaders(request, PlayerSession.Token);
        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError($"[ProfileTab] FetchProfile failed: {request.error}");
            yield break;
        }

        var profile = JsonUtility.FromJson<PlayerProfileResponse>(request.downloadHandler.text);
        onSuccess?.Invoke(profile);
    }

    IEnumerator FetchFormation(List<int> dest)
    {
        using var request = UnityWebRequest.Get($"{config.backendUrl}/api/TeamLoadout");
        config.SetHeaders(request, PlayerSession.Token);
        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            Debug.LogWarning($"[ProfileTab] FetchFormation failed: {request.error}");
            yield break;
        }

        var wrapper = JsonUtility.FromJson<IntArrayWrapper>("{\"items\":" + request.downloadHandler.text + "}");
        if (wrapper?.items == null) yield break;

        dest.Clear();
        dest.AddRange(wrapper.items);
    }

    IEnumerator FetchStoryProgress()
    {
        if (storyProgressText == null) yield break;

        bool loaded = false;
        yield return StartCoroutine(StoryClient.GetCurrent(config,
            progress => { loaded = true; BindStory(progress); },
            ()       => { storyProgressText.text = "Story unavailable"; }));

        if (!loaded && storyProgressText != null && string.IsNullOrEmpty(storyProgressText.text))
            storyProgressText.text = "Story unavailable";
    }

    // ── Bind ──────────────────────────────────────────────────────────────────

    void BindProfile(PlayerProfileResponse profile)
    {
        // Username
        if (usernameText != null)
            usernameText.text = profile.username;

        // UID — hash playerId thành 10 chữ số cho gọn
        if (uidText != null)
            uidText.text = $"UID: {HashToUid(profile.playerId)}";

        // Level
        if (levelText != null)
            levelText.text = $"Lv.{profile.level}";

        // Experience — slider + text "X/100"
        if (experienceSlider != null)
        {
            experienceSlider.minValue = 0;
            experienceSlider.maxValue = maxExperiencePerLevel;
            experienceSlider.value    = profile.experience;
        }
        if (experienceText != null)
            experienceText.text = $"{profile.experience}/{maxExperiencePerLevel}";

        // Collection counts — numbers only
        if (gemsText != null)
            gemsText.text = profile.gems.ToString();

        int unitCount    = profile.ownedUnits    != null ? profile.ownedUnits.Length    : 0;
        int weaponCount  = profile.ownedWeapons  != null ? profile.ownedWeapons.Length  : 0;
        int trinketCount = profile.ownedTrinkets != null ? profile.ownedTrinkets.Length : 0;

        if (unitsCountText   != null) unitsCountText.text   = unitCount.ToString();
        if (weaponsCountText != null) weaponsCountText.text = weaponCount.ToString();
        if (trinketsCountText!= null) trinketsCountText.text= trinketCount.ToString();

        // Rating
        if (ratingText != null)
            ratingText.text = profile.rating.ToString();

        // Rank tier sprite
        if (rankTierImage != null)
        {
            int tier   = profile.rankTier;
            Sprite spr = (rankTierSprites != null && tier >= 0 && tier < rankTierSprites.Length)
                ? rankTierSprites[tier]
                : null;

            rankTierImage.sprite  = spr;
            rankTierImage.enabled = spr != null;
        }

        // Match stats
        int totalMatches = profile.rankWins + profile.rankLosses;
        int winratePct   = totalMatches > 0
            ? Mathf.RoundToInt((float)profile.rankWins / totalMatches * 100f)
            : 0;

        if (matchCountText != null)
            matchCountText.text = $"{totalMatches:D1} Matches";

        if (winrateText != null)
            winrateText.text = $"{winratePct}%";

        // Signature — placeholder, populate when BE adds the field
        // if (signatureInputField != null)
        //     signatureInputField.text = profile.signature ?? string.Empty;
    }

    void BindStory(StoryProgressData progress)
    {
        if (storyProgressText == null || progress == null) return;

        storyProgressText.text = progress.isChapterCompleted
            ? $"Chapter {progress.chapterId} Completed"
            : $"Chapter {progress.chapterId} - Scene {progress.sceneId}";
    }

    void BindFormation(PlayerProfileResponse profile, List<int> formationUIds)
    {
        if (formationPortraits == null || formationPortraits.Count == 0) return;

        if (unitPrefabRegistry != null) unitPrefabRegistry.Init();
        if (classLibrary       != null) classLibrary.Init();

        var owned = profile.ownedUnits != null
            ? new List<OwnedUnitDto>(profile.ownedUnits)
            : new List<OwnedUnitDto>();

        for (int i = 0; i < formationPortraits.Count; i++)
        {
            var image         = formationPortraits[i];
            if (image == null) continue;

            var frameImage    = (formationFrames     != null && i < formationFrames.Count)     ? formationFrames[i]     : null;
            var halfFrameImg  = (formationHalfFrames != null && i < formationHalfFrames.Count) ? formationHalfFrames[i] : null;
            var nameText      = (formationNames      != null && i < formationNames.Count)      ? formationNames[i]      : null;

            bool hasUnit = i < formationUIds.Count && formationUIds[i] > 0;
            if (!hasUnit)
            {
                SetPortraitEmpty(image);
                if (frameImage   != null) SetPortraitEmpty(frameImage);
                if (halfFrameImg != null) SetPortraitEmpty(halfFrameImg);
                if (nameText     != null) nameText.text = string.Empty;
                continue;
            }

            int uId  = formationUIds[i];
            var unit = owned.Find(u => u.unitDefinitionUId == uId);

            if (unit == null)
            {
                SetPortraitEmpty(image);
                if (frameImage   != null) SetPortraitEmpty(frameImage);
                if (halfFrameImg != null) SetPortraitEmpty(halfFrameImg);
                if (nameText     != null) nameText.text = string.Empty;
                continue;
            }

            Sprite portrait   = null;
            Sprite fullFrame  = null;
            Sprite halfFrame  = null;
            string unitName   = string.Empty;

            if (unitPrefabRegistry != null)
            {
                var prefab     = unitPrefabRegistry.Get(uId);
                var clientUnit = prefab?.GetComponent<ClientUnit>();
                portrait       = clientUnit?.unitImg;
                unitName       = unitPrefabRegistry.GetName(uId);
            }

            if (classLibrary != null)
            {
                fullFrame = unit.unlockedClassIds?.Length > 0 ? classLibrary.GetFullFrame(unit.unlockedClassIds[0]) : null;
                halfFrame = unit.unlockedClassIds?.Length > 1 ? classLibrary.GetHalfFrame(unit.unlockedClassIds[1]) : null;
            }

            image.sprite  = portrait;
            image.enabled = portrait != null;
            image.color   = portrait != null ? Color.white : new Color(1f, 1f, 1f, 0.15f);

            if (frameImage != null)
            {
                frameImage.sprite  = fullFrame;
                frameImage.enabled = fullFrame != null;
                frameImage.color   = fullFrame != null ? Color.white : new Color(1f, 1f, 1f, 0.15f);
            }

            if (halfFrameImg != null)
            {
                halfFrameImg.sprite  = halfFrame;
                halfFrameImg.enabled = halfFrame != null;
                halfFrameImg.color   = halfFrame != null ? Color.white : new Color(1f, 1f, 1f, 0.15f);
            }

            if (nameText != null) nameText.text = unitName;
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    static void SetPortraitEmpty(Image image)
    {
        image.sprite = null;
        image.color  = new Color(1f, 1f, 1f, 0.15f);
    }

    /// <summary>
    /// Chuyển playerId (MongoDB ObjectId string) thành UID 10 chữ số.
    /// Deterministic — cùng playerId luôn ra cùng UID.
    /// </summary>
    static string HashToUid(string playerId)
    {
        if (string.IsNullOrEmpty(playerId)) return "0000000000";

        // Dùng FNV-1a 64-bit để tránh collision hơn GetHashCode()
        ulong hash = 14695981039346656037UL;
        foreach (char c in playerId)
        {
            hash ^= (ulong)c;
            hash *= 1099511628211UL;
        }

        // Lấy 10 chữ số cuối (mod 10^10)
        ulong uid = hash % 10_000_000_000UL;
        return uid.ToString("D10"); // luôn đủ 10 chữ số, thêm leading zero nếu thiếu
    }

    void HidePanel()     { this.HidePanelImmediate(panel); }
    void SetStatus(string msg) { if (statusText != null) statusText.text = msg ?? string.Empty; }
    void ClearStatus()   { SetStatus(string.Empty); }

    [System.Serializable] private class IntArrayWrapper { public int[] items; }
}
