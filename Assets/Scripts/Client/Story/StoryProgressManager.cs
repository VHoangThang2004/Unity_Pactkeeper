using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Scene manager for 10_StoryProgress.
/// Fetches chapter configs + player progress, renders a horizontal/vertical list
/// of ChapterCard prefabs (with Accordion Dropdowns), and supports launching any unlocked
/// scene with autoResume = false.
/// </summary>
public class StoryProgressManager : MonoBehaviour
{
    // ── Enums & data classes ──────────────────────────────────────────────────

    public enum ChapterStatus { NotStarted, InProgress, Completed, Locked }

    public class ChapterDisplayData
    {
        public int           chapterId;
        public ChapterStatus status;
        public Sprite        thumbnail;       // optional; assign via thumbnailSprites in Inspector
        public ChapterConfigDto  config;      // null if chapter has no config yet
        public StoryProgressDto  progress;    // null if player hasn't started
    }

    // ── DTOs ─────────────────────────────────────────────────────────────────

    [System.Serializable]
    public class SceneConfigDto
    {
        public int    sceneId;
        public string type;          // "Cutscene" | "Dialogue" | "Battle"
        public string description;
        public string mapId;
        public bool   autoResume;
        public bool   autoAdvance;
        public bool   autoNext;
    }

    [System.Serializable]
    public class ChapterConfigDto
    {
        public string          id;
        public int             chapterId;
        public string          title;
        public string          mapId;
        public SceneConfigDto[] scenes;
    }

    [System.Serializable]
    public class StoryProgressDto
    {
        public int    chapterId;
        public int    sceneId;
        public bool   isCompleted;
        public bool   isChapterCompleted;
    }

    [System.Serializable] private class ChapterConfigWrapper { public ChapterConfigDto[] items; }
    [System.Serializable] private class ProgressWrapper      { public StoryProgressDto[] items; }

    // ── Inspector ─────────────────────────────────────────────────────────────

    [Header("Config")]
    [SerializeField] private BackendConfig config;
    [SerializeField] private SceneConfig   sceneConfig;

    [Header("Horizontal/Vertical Scroll - Chapter Cards")]
    [SerializeField] private Transform  cardContainer;     // LayoutGroup Content
    [SerializeField] private GameObject chapterCardPrefab;
    [SerializeField] private GameObject loadingIndicator;
    [SerializeField] private GameObject emptyState;        // "No chapters available"

    [Tooltip("Optional thumbnail sprites indexed by ChapterId (index 0 = Chapter 0, etc.)")]
    [SerializeField] private Sprite[] thumbnailSprites;

    [Header("Back Button")]
    [SerializeField] private Button backButton;

    // ── Detail Popup ──────────────────────────────────────────────────────────

    [Header("Detail Popup")]
    [SerializeField] private GameObject popupPanel;
    [SerializeField] private Button     popupCloseButton;

    [SerializeField] private TMP_Text   popupChapterTitle;
    [SerializeField] private TMP_Text   popupStatusText;
    [SerializeField] private Image      popupStatusIcon;    // optional colored dot/badge

    [SerializeField] private Slider     popupProgressBar;
    [SerializeField] private TMP_Text   popupProgressText;  // "3 / 5 scenes"

    [SerializeField] private TMP_Text   popupCurrentSceneLabel;  // "Scene 2 - Tutorial battle"
    [SerializeField] private TMP_Text   popupSceneListText;       // all scenes breakdown

    [Header("Popup Interactive Scene List (Optional)")]
    [SerializeField] private RectTransform popupSceneDropdownContainer;
    [SerializeField] private GameObject    sceneDropdownItemPrefab;

    [Header("Status Colors (Popup)")]
    [SerializeField] private Color popupColorCompleted  = new Color(0.18f, 0.72f, 0.35f);
    [SerializeField] private Color popupColorInProgress = new Color(0.93f, 0.65f, 0.10f);
    [SerializeField] private Color popupColorNotStarted = new Color(0.65f, 0.65f, 0.65f);

    // ── Runtime ───────────────────────────────────────────────────────────────

    private List<ChapterDisplayData> chapters = new();
    private int  _maxCompletedChapterId = -1;
    private bool _isStartingMatch = false;
    private readonly List<GameObject> _popupSpawnedSceneItems = new();

    public bool HasPopupPanel => popupPanel != null;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    void Awake()
    {
        if (backButton       != null) backButton.onClick.AddListener(OnBackClick);
        if (popupCloseButton != null) popupCloseButton.onClick.AddListener(ClosePopup);

        HidePopup();
        if (loadingIndicator != null) loadingIndicator.SetActive(true);
        if (emptyState       != null) emptyState.SetActive(false);
    }

    void OnDestroy()
    {
        if (backButton       != null) backButton.onClick.RemoveListener(OnBackClick);
        if (popupCloseButton != null) popupCloseButton.onClick.RemoveListener(ClosePopup);
    }

    void Start()
    {
        if (!PlayerSession.IsLoggedIn)
        {
            SceneManager.LoadScene(sceneConfig.mainMenuScene);
            return;
        }
        StartCoroutine(LoadDataCoroutine());
    }

    // ── Data loading ──────────────────────────────────────────────────────────

    IEnumerator LoadDataCoroutine()
    {
        // Fetch both APIs in parallel
        ChapterConfigDto[]  configs   = null;
        StoryProgressDto[]  progresses = null;

        bool configDone   = false;
        bool progressDone = false;

        StartCoroutine(FetchChapterConfigs(  result => { configs    = result; configDone   = true; }));
        StartCoroutine(FetchStoryProgress(   result => { progresses = result; progressDone = true; }));

        yield return new WaitUntil(() => configDone && progressDone);

        if (loadingIndicator != null) loadingIndicator.SetActive(false);

        if (configs == null || configs.Length == 0)
        {
            if (emptyState != null) emptyState.SetActive(true);
            yield break;
        }

        BuildDisplayData(configs, progresses ?? new StoryProgressDto[0]);
        RenderCards();
    }

    IEnumerator FetchChapterConfigs(System.Action<ChapterConfigDto[]> onDone)
    {
        using var req = UnityWebRequest.Get($"{config.backendUrl}/api/chapterconfig");
        config.SetHeaders(req, PlayerSession.Token);
        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError($"[StoryProgress] Failed to load chapter configs: {req.error}");
            onDone(null);
            yield break;
        }

        var wrapper = JsonUtility.FromJson<ChapterConfigWrapper>(
            "{\"items\":" + req.downloadHandler.text + "}");
        onDone(wrapper?.items);
    }

    IEnumerator FetchStoryProgress(System.Action<StoryProgressDto[]> onDone)
    {
        using var req = UnityWebRequest.Get($"{config.backendUrl}/api/story/progress/all");
        config.SetHeaders(req, PlayerSession.Token);
        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            Debug.LogWarning($"[StoryProgress] Failed to load progress: {req.error}");
            onDone(null);
            yield break;
        }

        var wrapper = JsonUtility.FromJson<ProgressWrapper>(
            "{\"items\":" + req.downloadHandler.text + "}");
        onDone(wrapper?.items);
    }

    // ── Data joining ──────────────────────────────────────────────────────────

    void BuildDisplayData(ChapterConfigDto[] configs, StoryProgressDto[] progresses)
    {
        chapters.Clear();
        _maxCompletedChapterId = -1;

        // Build lookup maps
        var progressMap = new Dictionary<int, StoryProgressDto>();
        foreach (var p in progresses)
        {
            progressMap[p.chapterId] = p;
            if (p.isChapterCompleted && p.chapterId > _maxCompletedChapterId)
                _maxCompletedChapterId = p.chapterId;
        }

        var configMap = new Dictionary<int, ChapterConfigDto>();
        foreach (var cfg in configs)
        {
            if (cfg.scenes != null)
            {
                // Filter out non-PvE scenes (e.g. Cutscene 0.0 and Dialogue UI tutorials 0.4, 0.5, 0.6)
                cfg.scenes = System.Array.FindAll(cfg.scenes, s =>
                    s != null && string.Equals(s.type, "Battle", System.StringComparison.OrdinalIgnoreCase));
            }
            configMap[cfg.chapterId] = cfg;
        }

        // Determine how many chapters to show: max(backend configs, thumbnail sprites)
        int maxChapterToShow = 0;
        if (configs != null && configs.Length > 0)
        {
            System.Array.Sort(configs, (a, b) => a.chapterId.CompareTo(b.chapterId));
            maxChapterToShow = configs[configs.Length - 1].chapterId + 1; // chapterId 0-based
        }
        if (thumbnailSprites != null && thumbnailSprites.Length > maxChapterToShow)
            maxChapterToShow = thumbnailSprites.Length;

        bool previousCompleted = true; // Chapter 0 is always unlocked

        for (int chapterId = 0; chapterId < maxChapterToShow; chapterId++)
        {
            configMap.TryGetValue(chapterId, out var cfg);
            progressMap.TryGetValue(chapterId, out var prog);

            ChapterStatus status;
            if (!previousCompleted)
            {
                status = ChapterStatus.Locked;
            }
            else if (cfg == null)
            {
                // No backend config = coming soon chapter (always locked)
                status = ChapterStatus.Locked;
                previousCompleted = false; // Lock all subsequent chapters
            }
            else if (prog == null)
            {
                status = ChapterStatus.NotStarted;
            }
            else if (prog.isChapterCompleted)
            {
                status = ChapterStatus.Completed;
            }
            else
            {
                status = ChapterStatus.InProgress;
            }

            // Next chapter is only unlocked if this one is completed
            if (status == ChapterStatus.Completed)
                previousCompleted = true;
            else
                previousCompleted = false;

            Sprite thumb = null;
            if (thumbnailSprites != null && chapterId < thumbnailSprites.Length)
                thumb = thumbnailSprites[chapterId];

            chapters.Add(new ChapterDisplayData
            {
                chapterId  = chapterId,
                status     = status,
                thumbnail  = thumb,
                config     = cfg,      // null for coming soon chapters
                progress   = prog,     // null for not-started or coming soon
            });
        }
    }

    // ── Rendering ─────────────────────────────────────────────────────────────

    void RenderCards()
    {
        if (cardContainer == null || chapterCardPrefab == null) return;

        // Clear existing cards
        foreach (Transform child in cardContainer)
            Destroy(child.gameObject);

        if (chapters.Count == 0)
        {
            if (emptyState != null) emptyState.SetActive(true);
            return;
        }

        if (emptyState != null) emptyState.SetActive(false);

        foreach (var ch in chapters)
        {
            var obj  = Instantiate(chapterCardPrefab, cardContainer);
            var card = obj.GetComponent<ChapterCard>();
            card?.Setup(ch, this);
        }
    }

    // ── Called by ChapterCard on click ────────────────────────────────────────

    public void OnChapterCardClicked(ChapterDisplayData data)
    {
        // Don't open popup for coming soon or locked chapters
        if (data.config == null || data.status == ChapterStatus.Locked)
            return;

        ShowPopup(data);
    }

    // ── Called by SceneDropdownItemUI when a scene row is clicked ─────────────

    public void OnSceneSelectedFromDropdown(int targetChapterId, int targetSceneId)
    {
        if (_isStartingMatch) return;
        StartCoroutine(StartSelectedSceneRoutine(targetChapterId, targetSceneId));
    }

    private IEnumerator StartSelectedSceneRoutine(int targetChapterId, int targetSceneId)
    {
        _isStartingMatch = true;
        if (loadingIndicator != null) loadingIndicator.SetActive(true);

        bool isChapterAlreadyCompleted = targetChapterId <= _maxCompletedChapterId;
        PlayerSession.CanSkipOrExitStory = isChapterAlreadyCompleted;

        // Enforce technical requirement: manual chapter/scene selection ALWAYS sets autoResume = false
        PlayerSession.IsAutoResumed = false;
        PlayerSession.SelectedStoryChapterId = targetChapterId;
        PlayerSession.SelectedStorySceneId   = targetSceneId;

        Debug.Log($"[StoryProgressManager] Launching C{targetChapterId}_S{targetSceneId} " +
                  $"(autoResume=false, CanSkipOrExit={PlayerSession.CanSkipOrExitStory})");

        bool requestSuccess = false;
        StoryMatchSelectData matchData = null;

        yield return StartCoroutine(StoryClient.StartSelectedStoryMatch(
            config,
            targetChapterId,
            targetSceneId,
            autoResume: false,
            onSuccess: data =>
            {
                matchData = data;
                requestSuccess = true;
            },
            onFail: err =>
            {
                Debug.LogError($"[StoryProgressManager] Failed to start selected scene: {err}");
            }));

        if (!requestSuccess || matchData == null)
        {
            _isStartingMatch = false;
            if (loadingIndicator != null) loadingIndicator.SetActive(false);
            yield break;
        }

        if (Unity.Netcode.NetworkManager.Singleton != null &&
            Unity.Netcode.NetworkManager.Singleton.IsListening)
        {
            Debug.Log("[StoryProgressManager] Shutting down previous NetworkManager session before loading selected scene...");
            Unity.Netcode.NetworkManager.Singleton.Shutdown();
            yield return new WaitUntil(() => !Unity.Netcode.NetworkManager.Singleton.IsListening);
        }

        string targetUnityScene = $"C{targetChapterId}_S{targetSceneId}";

        if (!Application.CanStreamedLevelBeLoaded(targetUnityScene))
        {
            string fallback = sceneConfig != null && !string.IsNullOrEmpty(sceneConfig.clientMatch)
                ? sceneConfig.clientMatch
                : "5_ClientMatch";
            Debug.LogWarning($"[StoryProgressManager] Scene '{targetUnityScene}' not in Build Settings - falling back to '{fallback}'.");
            targetUnityScene = fallback;
        }

        SceneManager.LoadScene(targetUnityScene);
    }

    // ── Detail Popup ──────────────────────────────────────────────────────────

    void ShowPopup(ChapterDisplayData data)
    {
        if (popupPanel != null) popupPanel.SetActive(true);

        var cfg  = data.config;
        var prog = data.progress;

        // Title
        if (popupChapterTitle != null)
            popupChapterTitle.text = cfg != null
                ? cfg.title
                : $"Chapter {data.chapterId}";

        // Status
        string statusLabel;
        Color  statusColor;
        switch (data.status)
        {
            case ChapterStatus.Completed:
                statusLabel = "Completed";
                statusColor = popupColorCompleted;
                break;
            case ChapterStatus.InProgress:
                statusLabel = "In Progress";
                statusColor = popupColorInProgress;
                break;
            case ChapterStatus.Locked:
                statusLabel = "Locked";
                statusColor = popupColorNotStarted;
                break;
            default:
                statusLabel = "Not Started";
                statusColor = popupColorNotStarted;
                break;
        }

        if (popupStatusText != null)
        {
            popupStatusText.text  = statusLabel;
            popupStatusText.color = statusColor;
        }

        if (popupStatusIcon != null)
            popupStatusIcon.color = statusColor;

        // Progress bar
        int totalScenes     = cfg?.scenes?.Length ?? 0;
        int completedScenes = CalcCompletedScenes(prog, cfg?.scenes);

        if (popupProgressBar != null)
        {
            popupProgressBar.minValue = 0;
            popupProgressBar.maxValue = Mathf.Max(totalScenes, 1);
            popupProgressBar.value    = completedScenes;
        }

        if (popupProgressText != null)
            popupProgressText.text = totalScenes > 0
                ? $"{completedScenes} / {totalScenes} scenes"
                : "No scenes";

        // Current scene label
        if (popupCurrentSceneLabel != null)
        {
            if (prog != null && (prog.isChapterCompleted || (totalScenes > 0 && completedScenes >= totalScenes)))
            {
                popupCurrentSceneLabel.text = "All scenes completed!";
            }
            else if (prog != null && !prog.isChapterCompleted && cfg?.scenes != null)
            {
                var currentScene = System.Array.Find(cfg.scenes, s => s.sceneId == prog.sceneId)
                                ?? System.Array.Find(cfg.scenes, s => s.sceneId >= prog.sceneId);
                if (currentScene != null)
                {
                    string desc = currentScene.description;
                    string typeLabel = FormatSceneType(currentScene.type);
                    popupCurrentSceneLabel.text = string.IsNullOrEmpty(desc)
                        ? $"Scene {currentScene.sceneId} - {typeLabel}"
                        : $"Scene {currentScene.sceneId} - {desc}";
                }
                else
                {
                    popupCurrentSceneLabel.text = "Select a scene below to begin.";
                }
            }
            else
            {
                popupCurrentSceneLabel.text = "Select a scene below to begin.";
            }
        }

        // Scene list breakdown (text fallback)
        if (popupSceneListText != null && cfg?.scenes != null)
        {
            var sb = new System.Text.StringBuilder();
            foreach (var scene in cfg.scenes)
            {
                bool done = prog != null &&
                            (prog.isChapterCompleted ||
                             prog.sceneId > scene.sceneId ||
                             (prog.sceneId == scene.sceneId && prog.isCompleted));

                string tick      = done ? "✓" : "○";
                string typeLabel = FormatSceneType(scene.type);
                string desc      = string.IsNullOrEmpty(scene.description)
                    ? typeLabel
                    : scene.description;

                sb.AppendLine($"{tick}  {desc}  [{typeLabel}]");
            }
            popupSceneListText.text = sb.ToString().TrimEnd();
        }

        // Interactive scene buttons in popup (if container + prefab assigned)
        BuildPopupInteractiveSceneList(data);
    }

    private void BuildPopupInteractiveSceneList(ChapterDisplayData data)
    {
        foreach (var go in _popupSpawnedSceneItems)
        {
            if (go != null) Destroy(go);
        }
        _popupSpawnedSceneItems.Clear();

        if (popupSceneDropdownContainer == null || sceneDropdownItemPrefab == null || data?.config?.scenes == null)
            return;

        if (popupSceneListText != null)
            popupSceneListText.gameObject.SetActive(false);

        var vlg = popupSceneDropdownContainer.GetComponent<VerticalLayoutGroup>();
        if (vlg == null)
        {
            vlg = popupSceneDropdownContainer.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 10f;
            vlg.padding = new RectOffset(8, 8, 8, 8);
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
        }

        var csf = popupSceneDropdownContainer.GetComponent<ContentSizeFitter>();
        if (csf == null)
        {
            csf = popupSceneDropdownContainer.gameObject.AddComponent<ContentSizeFitter>();
            csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        bool isChapterCompleted = data.status == ChapterStatus.Completed ||
                                  (data.progress != null && data.progress.isChapterCompleted);
        bool isChapterUnlocked = data.status != ChapterStatus.Locked;

        int firstSceneId = data.config.scenes.Length > 0 ? data.config.scenes[0].sceneId : 0;
        int unlockedSceneHighWaterMark = firstSceneId;

        if (data.progress != null)
        {
            int rawMark = data.progress.isCompleted
                ? data.progress.sceneId + 1
                : data.progress.sceneId;
            unlockedSceneHighWaterMark = Mathf.Max(firstSceneId, rawMark);
        }

        foreach (var scene in data.config.scenes)
        {
            var itemObj = Instantiate(sceneDropdownItemPrefab, popupSceneDropdownContainer);
            _popupSpawnedSceneItems.Add(itemObj);

            var itemUI = itemObj.GetComponent<SceneDropdownItemUI>() ?? itemObj.AddComponent<SceneDropdownItemUI>();
            string englishLabel = !string.IsNullOrEmpty(scene.description)
                ? scene.description
                : $"{data.chapterId}.{scene.sceneId} - {scene.type}";

            bool sceneCompleted = isChapterCompleted ||
                                  (data.progress != null &&
                                   (data.progress.sceneId > scene.sceneId ||
                                    (data.progress.sceneId == scene.sceneId && data.progress.isCompleted)));

            bool sceneUnlocked = isChapterUnlocked &&
                                 (isChapterCompleted || scene.sceneId <= unlockedSceneHighWaterMark);

            int capturedSceneId = scene.sceneId;
            itemUI.Setup(
                data.chapterId,
                capturedSceneId,
                englishLabel,
                sceneCompleted,
                sceneUnlocked,
                (chId, scId) => OnSceneSelectedFromDropdown(chId, scId));
        }
    }

    void ClosePopup()
    {
        HidePopup();
    }

    void HidePopup()
    {
        if (popupPanel != null) popupPanel.SetActive(false);
    }

    // ── Navigation ────────────────────────────────────────────────────────────

    void OnBackClick()
    {
        SceneManager.LoadScene(sceneConfig.mainMenuScene);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    static int CalcCompletedScenes(StoryProgressDto prog, SceneConfigDto[] scenes)
    {
        if (prog == null || scenes == null || scenes.Length == 0) return 0;
        if (prog.isChapterCompleted) return scenes.Length;

        int completed = 0;
        foreach (var s in scenes)
        {
            if (prog.sceneId > s.sceneId || (prog.sceneId == s.sceneId && prog.isCompleted))
                completed++;
        }
        return completed;
    }

    static string FormatSceneType(string type)
    {
        return type switch
        {
            "Battle"   => "Battle",
            "Dialogue" => "Dialogue",
            "Cutscene" => "Cutscene",
            _          => type ?? "Unknown"
        };
    }
}
