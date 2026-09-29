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

    [Header("Identity")]
    [SerializeField] private Image avatarImage;
    [SerializeField] private TMP_Text usernameText;
    [SerializeField] private TMP_Text levelText;
    [SerializeField] private TMP_Text experienceText;
    [SerializeField] private TMP_Text gemsText;
    [SerializeField] private TMP_Text statusText;

    [Header("Collection counts")]
    [SerializeField] private TMP_Text unitsCountText;
    [SerializeField] private TMP_Text weaponsCountText;
    [SerializeField] private TMP_Text trinketsCountText;

    [Header("Story (optional)")]
    [SerializeField] private TMP_Text storyProgressText;

    [Header("Formation preview (optional, max 5)")]
    [SerializeField] private List<Image> formationPortraits;
    [SerializeField] private List<Image> formationFrames;
    [SerializeField] private List<Image> formationHalfFrames;
    [SerializeField] private List<TMP_Text> formationNames;

    void Awake()
    {
        if (closeButton != null)
            closeButton.onClick.AddListener(Close);

        HidePanel();
        ClearStatus();
    }

    void OnDestroy()
    {
        if (closeButton != null)
            closeButton.onClick.RemoveListener(Close);
    }

    public void Open()
    {
        Debug.Log("[ProfileTab] Open called");
        if (panel != null)
        {
            panel.SetActive(true);
            Debug.Log("[ProfileTab] Panel set active");
        }
        else
        {
            Debug.LogError("[ProfileTab] Panel is null!");
        }

        ClearStatus();
        StartCoroutine(LoadProfileTab());
    }

    public void Close()
    {
        HidePanel();
    }

    IEnumerator LoadProfileTab()
    {
        if (config == null)
        {
            SetStatus("Failed to load profile");
            yield break;
        }

        SetStatus("Loading...");

        PlayerProfileResponse profile = null;
        yield return StartCoroutine(FetchProfile(p => profile = p));

        if (profile == null)
        {
            SetStatus("Failed to load profile");
            yield break;
        }

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
        if (wrapper.items == null) yield break;

        dest.Clear();
        dest.AddRange(wrapper.items);
    }

    IEnumerator FetchStoryProgress()
    {
        if (storyProgressText == null)
            yield break;

        bool loaded = false;
        yield return StartCoroutine(StoryClient.GetCurrent(config,
            progress =>
            {
                loaded = true;
                BindStory(progress);
            },
            () =>
            {
                storyProgressText.text = "Story unavailable";
            }));

        if (!loaded && storyProgressText != null &&
            string.IsNullOrEmpty(storyProgressText.text))
        {
            storyProgressText.text = "Story unavailable";
        }
    }

    void BindProfile(PlayerProfileResponse profile)
    {
        if (usernameText != null)
            usernameText.text = profile.username;

        if (levelText != null)
            levelText.text = $"Level: {profile.level}";

        if (experienceText != null)
            experienceText.text = $"XP: {profile.experience}";

        if (gemsText != null)
            gemsText.text = $"Gems: {profile.gems}";

        int unitCount = profile.ownedUnits != null ? profile.ownedUnits.Length : 0;
        int weaponCount = profile.ownedWeapons != null ? profile.ownedWeapons.Length : 0;
        int trinketCount = profile.ownedTrinkets != null ? profile.ownedTrinkets.Length : 0;

        if (unitsCountText != null)
            unitsCountText.text = $"Units: {unitCount}";

        if (weaponsCountText != null)
            weaponsCountText.text = $"Weapons: {weaponCount}";

        if (trinketsCountText != null)
            trinketsCountText.text = $"Trinkets: {trinketCount}";
    }

    void BindStory(StoryProgressData progress)
    {
        if (storyProgressText == null || progress == null)
            return;

        if (progress.isChapterCompleted)
        {
            storyProgressText.text = $"Chapter {progress.chapterId} Completed";
            return;
        }

        storyProgressText.text = $"Chapter {progress.chapterId} - Scene {progress.sceneId}";
    }

    void BindFormation(PlayerProfileResponse profile, List<int> formationUIds)
    {
        if (formationPortraits == null || formationPortraits.Count == 0)
            return;

        if (unitPrefabRegistry != null)
            unitPrefabRegistry.Init();

        if (classLibrary != null)
            classLibrary.Init();

        var owned = profile.ownedUnits != null
            ? new List<OwnedUnitDto>(profile.ownedUnits)
            : new List<OwnedUnitDto>();

        Debug.Log($"[ProfileTab] Formation UIds: {string.Join(", ", formationUIds)}");
        Debug.Log($"[ProfileTab] Owned Units count: {owned.Count}");

        for (int i = 0; i < formationPortraits.Count; i++)
        {
            var image = formationPortraits[i];
            if (image == null)
                continue;

            var frameImage = formationFrames != null && i < formationFrames.Count ? formationFrames[i] : null;
            var halfFrameImage = formationHalfFrames != null && i < formationHalfFrames.Count ? formationHalfFrames[i] : null;
            var nameText = formationNames != null && i < formationNames.Count ? formationNames[i] : null;

            bool hasUnit = i < formationUIds.Count && formationUIds[i] > 0;
            if (!hasUnit)
            {
                Debug.Log($"[ProfileTab] Slot {i}: Empty (no unit)");
                SetPortraitEmpty(image);
                if (frameImage != null)
                    SetPortraitEmpty(frameImage);
                if (halfFrameImage != null)
                    SetPortraitEmpty(halfFrameImage);
                if (nameText != null)
                    nameText.text = string.Empty;
                continue;
            }

            int uId = formationUIds[i];
            Debug.Log($"[ProfileTab] Slot {i}: uId = {uId}");

            var unit = owned.Find(u => u.unitDefinitionUId == uId);
            if (unit == null)
            {
                Debug.LogWarning($"[ProfileTab] Slot {i}: Unit uId {uId} not found in owned units");
                SetPortraitEmpty(image);
                if (frameImage != null)
                    SetPortraitEmpty(frameImage);
                if (halfFrameImage != null)
                    SetPortraitEmpty(halfFrameImage);
                if (nameText != null)
                    nameText.text = string.Empty;
                continue;
            }

            Sprite portrait = null;
            Sprite fullFrame = null;
            Sprite halfFrame = null;
            string unitName = string.Empty;

            if (unitPrefabRegistry != null)
            {
                var prefab = unitPrefabRegistry.Get(uId);
                var clientUnit = prefab != null ? prefab.GetComponent<ClientUnit>() : null;
                portrait = clientUnit?.unitImg;
                unitName = unitPrefabRegistry.GetName(uId);
                Debug.Log($"[ProfileTab] Slot {i}: Prefab found = {prefab != null}, Portrait = {portrait != null}");
            }
            else
            {
                Debug.LogError("[ProfileTab] UnitPrefabRegistry is null!");
            }

            if (classLibrary != null && unit != null)
            {
                fullFrame = unit.unlockedClassIds != null && unit.unlockedClassIds.Length > 0
                    ? classLibrary.GetFullFrame(unit.unlockedClassIds[0])
                    : null;
                halfFrame = unit.unlockedClassIds != null && unit.unlockedClassIds.Length > 1
                    ? classLibrary.GetHalfFrame(unit.unlockedClassIds[1])
                    : null;
            }

            image.sprite = portrait;
            image.enabled = portrait != null;
            image.color = portrait != null ? Color.white : new Color(1f, 1f, 1f, 0.15f);

            if (frameImage != null)
            {
                frameImage.sprite = fullFrame;
                frameImage.enabled = fullFrame != null;
                frameImage.color = fullFrame != null ? Color.white : new Color(1f, 1f, 1f, 0.15f);
            }

            if (halfFrameImage != null)
            {
                halfFrameImage.sprite = halfFrame;
                halfFrameImage.enabled = halfFrame != null;
                halfFrameImage.color = halfFrame != null ? Color.white : new Color(1f, 1f, 1f, 0.15f);
            }

            if (nameText != null)
            {
                nameText.text = unitName;
            }
        }
    }

    static void SetPortraitEmpty(Image image)
    {
        image.sprite = null;
        image.color = new Color(1f, 1f, 1f, 0.15f);
    }

    void HidePanel()
    {
        if (panel != null)
            panel.SetActive(false);
    }

    void SetStatus(string message)
    {
        if (statusText != null)
            statusText.text = message ?? string.Empty;
    }

    void ClearStatus()
    {
        SetStatus(string.Empty);
    }

    [System.Serializable]
    private class IntArrayWrapper
    {
        public int[] items;
    }
}
