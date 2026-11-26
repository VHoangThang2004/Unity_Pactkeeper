using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Netcode;

/// <summary>
/// Manages the conditional "Skip Scene" and "Exit to Map" buttons during Story replays.
/// Strictly hidden when:
/// - The player has not yet completed the current chapter (!PlayerSession.CanSkipOrExitStory) or is auto-resumed,
/// - The PvE / additive scene is still loading or connecting ("Connecting to server..." / LoadingScreen active),
/// - The battle has ended (Victory / Defeat overlay active) or a scene transition is in progress.
/// Instantiates Assets/Prefabs/UI/ReplayBarPrefab.prefab as its single UI source of truth.
/// </summary>
public class TutorialReplayUIController : MonoBehaviour
{
    [Header("Replay Bar Prefab")]
    [Tooltip("Prefab UI Canvas containing Skip Scene & Exit to Map buttons (Assets/Prefabs/UI/ReplayBarPrefab.prefab).")]
    [SerializeField] private StoryReplayBarUI replayBarPrefab;

    [Header("Dependencies")]
    [SerializeField] private BackendConfig backendConfig;
    [SerializeField] private PlotSequencer sequencer;
    [SerializeField] private string storyProgressSceneName = "10_StoryProgress";

    [Header("Seamless Skip")]
    [Tooltip("When ON and the next scene is also a battle of the same chapter, Skip Scene keeps the current " +
             "Dedicated Server + 5_ClientMatch alive and swaps only the overlay scene. The next " +
             "BattleTutorialStarter then calls AdvanceTutorialPhaseServerRpc(sceneId, forceCleanReset: true) " +
             "so the server reloads the StoryEncounterConfig in place (no new headless process). " +
             "Turn OFF to always spawn a fresh server per scene.")]
    [SerializeField] private bool seamlessSkip = false;
    [SerializeField] private string battleSceneName = "5_ClientMatch";

    private StoryReplayBarUI _replayBarInstance;
    private LoadingScreen _cachedLoadingScreen;
    private ClientController _cachedController;

    private bool _isTransitioning = false;
    private bool _initialized = false;
    private bool _deferUntilReady = false;
    private bool _sceneReady = false;
    private bool _forceHidden = false;

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

    void Awake()
    {
        // Any scene driven by BattleTutorialStarter must wait until server connection & loading screen finish.
        if (GetComponent<BattleTutorialStarter>() != null)
            _deferUntilReady = true;
    }

    void Start()
    {
        InitializeUI();
    }

    void OnDestroy()
    {
        if (_replayBarInstance != null)
            Destroy(_replayBarInstance.gameObject);
    }

    void LateUpdate()
    {
        if (_replayBarInstance == null) return;
        RefreshVisibility();
    }

    /// <summary>
    /// Prepares the Replay Bar prefab while optionally keeping it hidden until NotifySceneReady() is called
    /// (used by PvE Battle scenes and additive UI tutorial scenes so buttons never float over loading screens).
    /// </summary>
    public void PrepareUI(bool deferUntilReady)
    {
        if (deferUntilReady)
        {
            _deferUntilReady = true;
            _sceneReady = false;
        }
        InitializeUI();
        RefreshVisibility();
    }

    public void NotifySceneReady()
    {
        _sceneReady = true;
        _forceHidden = false;
        InitializeUI();
        RefreshVisibility();
    }

    public void Hide()
    {
        _forceHidden = true;
        RefreshVisibility();
    }

    public void InitializeUI()
    {
        if (_initialized)
        {
            RefreshVisibility();
            return;
        }
        _initialized = true;

        if (!_deferUntilReady)
            _sceneReady = true;

        if (sequencer == null)
            sequencer = FindAnyObjectByType<PlotSequencer>();

        if (!ReplayControlsAllowed)
            return;

        if (replayBarPrefab == null)
        {
            Debug.LogWarning("[TutorialReplayUI] replayBarPrefab is not assigned! Assign Assets/Prefabs/UI/ReplayBarPrefab.prefab in Inspector.");
            return;
        }

        int chapterId = PlayerSession.SelectedStoryChapterId;
        int currentSceneId = PlayerSession.SelectedStorySceneId;
        TryParseActiveScene(ref chapterId, ref currentSceneId);
        bool hasNextBattleInChapter = HasNextBattleSceneInChapter(chapterId, currentSceneId);

        _replayBarInstance = Instantiate(replayBarPrefab);
        _replayBarInstance.Configure(hasNextBattleInChapter, OnSkipSceneClicked, OnExitToMapClicked);
        RefreshVisibility();
    }

    private void RefreshVisibility()
    {
        if (_replayBarInstance == null) return;

        bool shouldShow =
            ReplayControlsAllowed &&
            _sceneReady &&
            !_forceHidden &&
            !_isTransitioning &&
            !TutorialContext.HasBattleEnded &&
            !IsLoadingInProgress();

        _replayBarInstance.SetVisible(shouldShow);
    }

    private bool IsLoadingInProgress()
    {
        if (_cachedController == null && _deferUntilReady)
            _cachedController = FindAnyObjectByType<ClientController>();

        if (_cachedController != null)
        {
            if (_cachedController.IsConnecting || !_cachedController.IsMatchReady)
                return true;

            if (_cachedController.Scene != null &&
                _cachedController.Scene.loadingScreen != null &&
                _cachedController.Scene.loadingScreen.IsVisible)
            {
                return true;
            }
        }

        if (_cachedLoadingScreen == null)
            _cachedLoadingScreen = FindAnyObjectByType<LoadingScreen>();

        return _cachedLoadingScreen != null && _cachedLoadingScreen.IsVisible;
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
        RefreshVisibility();

        // Keep TutorialContext.IsActive = true while shutting down NetworkManager and terminating the old server
        // so ClientMenuReturner.OnDisconnected does not hijack the disconnect and load 6_MatchResult -> 3_MainMenu!
        TutorialContext.IsActive = true;

        if (sequencer != null)
            sequencer.StopAllCoroutines();
        PlotDirector.Clear();

        int chapterId = PlayerSession.SelectedStoryChapterId;
        int currentSceneId = PlayerSession.SelectedStorySceneId;
        TryParseActiveScene(ref chapterId, ref currentSceneId);

        int nextSceneId = currentSceneId + 1;
        string nextUnitySceneName = $"C{chapterId}_S{nextSceneId}";
        bool nextIsBattle = IsBattleSceneInChapter(chapterId, nextSceneId);

        // Only allow skipping to a Battle scene (never skip from C0_S3 Battle into C0_S4 Dialogue)
        if (nextIsBattle && Application.CanStreamedLevelBeLoaded(nextUnitySceneName))
        {
            bool currentIsBattle = IsBattleSceneInChapter(chapterId, currentSceneId);

            PlayerSession.SelectedStoryChapterId = chapterId;
            PlayerSession.SelectedStorySceneId = nextSceneId;
            PlayerSession.IsAutoResumed = false;

            string currentUnitySceneName = $"C{chapterId}_S{currentSceneId}";
            if (currentIsBattle && nextIsBattle && CanSwapOverlaySeamlessly(currentUnitySceneName))
            {
                Debug.Log($"[TutorialReplayUI] Seamless skip {currentUnitySceneName} -> {nextUnitySceneName} (same server).");
                OverlaySwapRunner.Begin(currentUnitySceneName, nextUnitySceneName, battleSceneName);
                yield break;
            }

            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            {
                Debug.Log("[TutorialReplayUI] Shutting down NetworkManager before transitioning to next scene...");
                NetworkManager.Singleton.Shutdown();
                yield return new WaitUntil(() => !NetworkManager.Singleton.IsListening);
            }

            if (nextIsBattle)
            {
                bool matchStarted = false;
                if (backendConfig != null)
                {
                    yield return StartCoroutine(StoryClient.StartSelectedStoryMatch(
                        backendConfig,
                        chapterId,
                        nextSceneId,
                        autoResume: false,
                        onSuccess: _ => matchStarted = true,
                        onFail: err => Debug.LogWarning($"[TutorialReplayUI] Could not start next battle scene C{chapterId}_S{nextSceneId}: {err}")));
                }

                if (!matchStarted && backendConfig != null)
                {
                    TutorialContext.Reset();
                    SceneManager.LoadScene(storyProgressSceneName);
                    yield break;
                }
            }
            else if (currentIsBattle)
            {
                if (backendConfig != null)
                    yield return StartCoroutine(StoryClient.TerminateStoryMatch(backendConfig));
            }

            Debug.Log($"[TutorialReplayUI] Skipping from C{chapterId}_S{currentSceneId} to {nextUnitySceneName}.");
            TutorialContext.Reset();
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
            SceneManager.SetActiveScene(battleScene);

            var currentScene = SceneManager.GetSceneByName(currentSceneName);
            if (currentScene.isLoaded)
                yield return SceneManager.UnloadSceneAsync(currentScene);

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
        RefreshVisibility();

        // Keep TutorialContext.IsActive = true while shutting down NetworkManager and terminating the server
        // so ClientMenuReturner.OnDisconnected does not hijack the disconnect and load 6_MatchResult -> 3_MainMenu!
        TutorialContext.IsActive = true;

        if (sequencer != null)
            sequencer.StopAllCoroutines();
        PlotDirector.Clear();

        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            Debug.Log("[TutorialReplayUI] Shutting down NetworkManager...");
            NetworkManager.Singleton.Shutdown();
            yield return new WaitUntil(() => !NetworkManager.Singleton.IsListening);
        }

        if (backendConfig != null)
        {
            yield return StartCoroutine(StoryClient.TerminateStoryMatch(backendConfig));
        }

        TutorialContext.Reset();
        SceneManager.LoadScene(storyProgressSceneName);
    }

    private void SetButtonsInteractable(bool interactable)
    {
        if (_replayBarInstance != null)
            _replayBarInstance.SetButtonsInteractable(interactable);
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

    private static bool HasNextBattleSceneInChapter(int chapterId, int currentSceneId)
    {
        int nextSceneId = currentSceneId + 1;
        string nextUnitySceneName = $"C{chapterId}_S{nextSceneId}";
        return IsBattleSceneInChapter(chapterId, nextSceneId) &&
               Application.CanStreamedLevelBeLoaded(nextUnitySceneName);
    }

    private static bool IsBattleSceneInChapter(int chapterId, int sceneId)
    {
        return chapterId switch
        {
            0 => sceneId >= 1 && sceneId <= 3,
            1 => sceneId >= 1 && sceneId <= 3,
            2 => sceneId >= 1 && sceneId <= 2,
            3 => sceneId >= 1 && sceneId <= 2,
            4 => sceneId == 1,
            _ => false
        };
    }
}
