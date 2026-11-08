using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using Unity.Netcode;
using TMPro;

/// <summary>
/// Manages the conditional "Skip Scene" and "Exit to Map" buttons during Story Battle tutorials.
/// Strictly hidden when the player has not yet completed the current chapter (!PlayerSession.CanSkipOrExitStory).
/// Automatically builds its UI bar on TutorialCanvas if replayBarRoot is not manually assigned in Inspector.
/// </summary>
public class TutorialReplayUIController : MonoBehaviour
{
    [Header("Replay Bar UI References")]
    [SerializeField] private GameObject replayBarRoot;
    [SerializeField] private Button     skipSceneButton;
    [SerializeField] private Button     exitToMapButton;

    [Header("Dependencies")]
    [SerializeField] private BackendConfig  backendConfig;
    [SerializeField] private PlotSequencer  sequencer;
    [SerializeField] private string         storyProgressSceneName = "10_StoryProgress";

    [Header("Seamless Skip")]
    [Tooltip("When ON and the next scene is also a battle of the same chapter, Skip Scene keeps the current " +
             "Dedicated Server + 5_ClientMatch alive and swaps only the overlay scene. The next " +
             "BattleTutorialStarter then calls AdvanceTutorialPhaseServerRpc(sceneId, forceCleanReset: true) " +
             "so the server reloads the StoryEncounterConfig in place (no new headless process). " +
             "Turn OFF to always spawn a fresh server per scene.")]
    [SerializeField] private bool   seamlessSkip = true;
    [SerializeField] private string battleSceneName = "5_ClientMatch";

    private bool _isTransitioning = false;
    private bool _initialized = false;

    /// <summary>
    /// Replay controls are only legal when the player started this scene manually from the Story Map
    /// on an already completed chapter. An auto-resumed (main menu "Continue") run never shows them,
    /// even if CanSkipOrExitStory is stale from an earlier replay.
    /// </summary>
    private static bool ReplayControlsAllowed =>
        PlayerSession.CanSkipOrExitStory && !PlayerSession.IsAutoResumed;

    public void ConfigureDependencies(BackendConfig config, PlotSequencer plotSequencer)
    {
        if (config != null && backendConfig == null)
            backendConfig = config;
        if (plotSequencer != null && sequencer == null)
            sequencer = plotSequencer;
    }

    void Start()
    {
        InitializeUI();
    }

    public void InitializeUI()
    {
        if (_initialized) return;
        _initialized = true;

        if (sequencer == null)
            sequencer = FindAnyObjectByType<PlotSequencer>();

        bool canShow = ReplayControlsAllowed;

        if (!canShow)
        {
            if (replayBarRoot != null)
                replayBarRoot.SetActive(false);
            return;
        }

        if (replayBarRoot == null)
            BuildRuntimeReplayBar();

        if (replayBarRoot != null)
            replayBarRoot.SetActive(true);

        if (skipSceneButton != null)
        {
            skipSceneButton.onClick.RemoveAllListeners();
            skipSceneButton.onClick.AddListener(OnSkipSceneClicked);
        }

        if (exitToMapButton != null)
        {
            exitToMapButton.onClick.RemoveAllListeners();
            exitToMapButton.onClick.AddListener(OnExitToMapClicked);
        }
    }

    private void BuildRuntimeReplayBar()
    {
        Transform parentTransform = null;
        if (sequencer != null && sequencer.TextBoxPanel != null)
            parentTransform = sequencer.TextBoxPanel.transform.parent;

        if (parentTransform == null)
        {
            var tutorialCanvasObj = GameObject.Find("TutorialCanvas");
            if (tutorialCanvasObj != null)
                parentTransform = tutorialCanvasObj.transform;
        }

        if (parentTransform == null)
            return;

        replayBarRoot = new GameObject("ReplayBarRoot", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Canvas), typeof(GraphicRaycaster), typeof(HorizontalLayoutGroup));
        replayBarRoot.transform.SetParent(parentTransform, false);

        var rect = replayBarRoot.GetComponent<RectTransform>();
        // Top-right corner of the HUD
        rect.anchorMin = new Vector2(1f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 1f);
        rect.anchoredPosition = new Vector2(-24f, -24f);
        rect.sizeDelta = new Vector2(380f, 60f);

        var bg = replayBarRoot.GetComponent<Image>();
        bg.color = new Color(0.08f, 0.08f, 0.08f, 0.82f);

        var subCanvas = replayBarRoot.GetComponent<Canvas>();
        subCanvas.overrideSorting = true;
        subCanvas.sortingOrder = 1020;

        var hlg = replayBarRoot.GetComponent<HorizontalLayoutGroup>();
        hlg.padding = new RectOffset(10, 10, 8, 8);
        hlg.spacing = 12f;
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.childControlWidth = true;
        hlg.childControlHeight = true;
        hlg.childForceExpandWidth = true;
        hlg.childForceExpandHeight = true;

        skipSceneButton = CreateActionButton(replayBarRoot.transform, "SkipSceneButton", "Skip Scene", new Color(0.28f, 0.24f, 0.14f, 0.96f));
        exitToMapButton = CreateActionButton(replayBarRoot.transform, "ExitToMapButton", "Exit to Map", new Color(0.42f, 0.16f, 0.14f, 0.96f));
    }

    private static Button CreateActionButton(Transform parent, string objectName, string labelText, Color bgColor)
    {
        var btnObj = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        btnObj.transform.SetParent(parent, false);

        var img = btnObj.GetComponent<Image>();
        img.color = bgColor;

        var btn = btnObj.GetComponent<Button>();
        btn.targetGraphic = img;

        var textObj = new GameObject("Text (TMP)", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textObj.transform.SetParent(btnObj.transform, false);

        var textRect = textObj.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(6f, 4f);
        textRect.offsetMax = new Vector2(-6f, -4f);

        var tmp = textObj.GetComponent<TextMeshProUGUI>();
        tmp.text = labelText;
        tmp.fontSize = 20f;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        tmp.raycastTarget = false;

        return btn;
    }

    public void OnSkipSceneClicked()
    {
        if (_isTransitioning || !ReplayControlsAllowed) return;
        StartCoroutine(SkipSceneRoutine());
    }

    public void OnExitToMapClicked()
    {
        if (_isTransitioning || !ReplayControlsAllowed) return;
        StartCoroutine(ExitToStoryMapRoutine());
    }

    private IEnumerator SkipSceneRoutine()
    {
        _isTransitioning = true;
        SetButtonsInteractable(false);

        if (sequencer != null)
            sequencer.StopAllCoroutines();
        PlotDirector.Clear();
        TutorialContext.Reset();

        int chapterId = PlayerSession.SelectedStoryChapterId;
        int currentSceneId = PlayerSession.SelectedStorySceneId;
        TryParseActiveScene(ref chapterId, ref currentSceneId);

        int nextSceneId = currentSceneId + 1;
        string nextUnitySceneName = $"C{chapterId}_S{nextSceneId}";

        if (Application.CanStreamedLevelBeLoaded(nextUnitySceneName))
        {
            bool nextIsBattle = IsBattleSceneInChapter(chapterId, nextSceneId);
            bool currentIsBattle = IsBattleSceneInChapter(chapterId, currentSceneId);

            PlayerSession.SelectedStoryChapterId = chapterId;
            PlayerSession.SelectedStorySceneId = nextSceneId;
            PlayerSession.IsAutoResumed = false;

            // Seamless path: battle -> battle inside the same chapter. Keep the Dedicated Server and the
            // additive 5_ClientMatch alive; only the overlay scene is swapped. The next BattleTutorialStarter
            // sends AdvanceTutorialPhaseServerRpc(nextSceneId, forceCleanReset: true) and the server reloads
            // the StoryEncounterConfig in place.
            string currentUnitySceneName = $"C{chapterId}_S{currentSceneId}";
            if (currentIsBattle && nextIsBattle && CanSwapOverlaySeamlessly(currentUnitySceneName))
            {
                Debug.Log($"[TutorialReplayUI] Seamless skip {currentUnitySceneName} -> {nextUnitySceneName} (same server).");
                OverlaySwapRunner.Begin(currentUnitySceneName, nextUnitySceneName, battleSceneName);
                yield break;
            }

            if (nextIsBattle)
            {
                if (string.IsNullOrEmpty(PlayerSession.MatchId) && backendConfig != null)
                {
                    yield return StartCoroutine(StoryClient.StartSelectedStoryMatch(
                        backendConfig,
                        chapterId,
                        nextSceneId,
                        autoResume: false,
                        onSuccess: _ => { }));
                }

                if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
                {
                    Debug.Log("[TutorialReplayUI] Shutting down NetworkManager before reconnecting in next battle scene...");
                    NetworkManager.Singleton.Shutdown();
                    yield return new WaitUntil(() => !NetworkManager.Singleton.IsListening);
                }
            }
            else if (currentIsBattle)
            {
                if (backendConfig != null)
                    yield return StartCoroutine(StoryClient.TerminateStoryMatch(backendConfig));

                if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
                {
                    NetworkManager.Singleton.Shutdown();
                    yield return new WaitUntil(() => !NetworkManager.Singleton.IsListening);
                }
            }

            Debug.Log($"[TutorialReplayUI] Skipping from C{chapterId}_S{currentSceneId} to {nextUnitySceneName}.");
            SceneManager.LoadScene(nextUnitySceneName);
            yield break;
        }

        Debug.Log($"[TutorialReplayUI] Scene C{chapterId}_S{currentSceneId} is the final scene of Chapter {chapterId}. Exiting to Story Map.");
        yield return StartCoroutine(ExitToStoryMapRoutine());
    }

    private bool CanSwapOverlaySeamlessly(string currentUnitySceneName)
    {
        if (!seamlessSkip) return false;

        var nm = NetworkManager.Singleton;
        if (nm == null || !nm.IsConnectedClient) return false;

        var battleScene = SceneManager.GetSceneByName(battleSceneName);
        var currentScene = SceneManager.GetSceneByName(currentUnitySceneName);
        // Only valid when the story scene is an additive overlay on top of the shared battle scene.
        if (!battleScene.isLoaded || !currentScene.isLoaded) return false;

        var controller = FindAnyObjectByType<ClientController>();
        return controller != null && controller.IsMatchReady && controller.bridge != null;
    }

    /// <summary>
    /// Lives in the always-loaded battle scene so the swap survives unloading the old overlay
    /// (the TutorialReplayUIController itself is destroyed together with that overlay).
    /// </summary>
    private sealed class OverlaySwapRunner : MonoBehaviour
    {
        public static void Begin(string currentSceneName, string nextSceneName, string battleSceneName)
        {
            var battleScene = SceneManager.GetSceneByName(battleSceneName);
            var go = new GameObject("OverlaySwapRunner");
            SceneManager.MoveGameObjectToScene(go, battleScene);
            var runner = go.AddComponent<OverlaySwapRunner>();
            runner.StartCoroutine(runner.Swap(currentSceneName, nextSceneName, battleScene));
        }

        private IEnumerator Swap(string currentSceneName, string nextSceneName, Scene battleScene)
        {
            // An active scene must always exist: park it on the battle scene while the overlay is replaced.
            SceneManager.SetActiveScene(battleScene);

            var currentScene = SceneManager.GetSceneByName(currentSceneName);
            if (currentScene.isLoaded)
                yield return SceneManager.UnloadSceneAsync(currentScene);

            // sceneLoaded fires after Awake/OnEnable but before Start, so BattleTutorialStarter.Start sees the
            // correct active scene name when it prefixes TutorialTarget ids (e.g. "C1S3_WSKILL").
            UnityEngine.Events.UnityAction<Scene, LoadSceneMode> onLoaded = (s, m) =>
            {
                if (s.name == nextSceneName) SceneManager.SetActiveScene(s);
            };
            SceneManager.sceneLoaded += onLoaded;
            yield return SceneManager.LoadSceneAsync(nextSceneName, LoadSceneMode.Additive);
            SceneManager.sceneLoaded -= onLoaded;

            Destroy(gameObject);
        }
    }

    private IEnumerator ExitToStoryMapRoutine()
    {
        _isTransitioning = true;
        SetButtonsInteractable(false);

        if (sequencer != null)
            sequencer.StopAllCoroutines();
        PlotDirector.Clear();
        TutorialContext.Reset();

        if (backendConfig != null)
        {
            yield return StartCoroutine(StoryClient.TerminateStoryMatch(backendConfig));
        }

        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            Debug.Log("[TutorialReplayUI] Shutting down NetworkManager...");
            NetworkManager.Singleton.Shutdown();
            yield return new WaitUntil(() => !NetworkManager.Singleton.IsListening);
        }

        SceneManager.LoadScene(storyProgressSceneName);
    }

    private void SetButtonsInteractable(bool interactable)
    {
        if (skipSceneButton != null) skipSceneButton.interactable = interactable;
        if (exitToMapButton != null) exitToMapButton.interactable = interactable;
    }

    private static void TryParseActiveScene(ref int chapterId, ref int sceneId)
    {
        string activeName = SceneManager.GetActiveScene().name;
        if (!activeName.StartsWith("C", System.StringComparison.OrdinalIgnoreCase)) return;

        int underscoreIdx = activeName.IndexOf("_S", System.StringComparison.OrdinalIgnoreCase);
        if (underscoreIdx <= 1) return;

        if (int.TryParse(activeName.Substring(1, underscoreIdx - 1), out int parsedCh) &&
            int.TryParse(activeName.Substring(underscoreIdx + 2), out int parsedSc))
        {
            chapterId = parsedCh;
            sceneId = parsedSc;
        }
    }

    private static bool IsBattleSceneInChapter(int chapterId, int sceneId)
    {
        return chapterId switch
        {
            0 => sceneId >= 1 && sceneId <= 3,
            1 => sceneId >= 1 && sceneId <= 3,
            2 => sceneId >= 1 && sceneId <= 2,
            3 => sceneId >= 1 && sceneId <= 2,
            _ => false
        };
    }
}
