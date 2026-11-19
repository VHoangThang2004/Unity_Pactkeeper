using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Wires up the tutorial / hybrid / challenge overlay for any Story PvE battle scene.
/// Loads 5_ClientMatch additively, binds TutorialTarget IDs, drives PlotSequencer,
/// and handles Server Victory / Defeat / Retry outcomes cleanly.
/// </summary>
public class BattleTutorialStarter : MonoBehaviour
{
    [SerializeField] private ClientController controller;
    [SerializeField] private PlotSequencer sequencer;
    [UnityEngine.Serialization.FormerlySerializedAs("trigger")]
    [SerializeField] private TutorialTrigger tutorialTrigger;
    [SerializeField] private string additiveBattleSceneName = "5_ClientMatch";

    [Tooltip("List of plots and their trigger conditions. Leave empty for pure Challenge stages.")]
    [SerializeField] private List<PlotSequencer.PlotEntry> plots;

    [SerializeField] private BackendConfig backendConfig;

    private bool _tutorialStarted = false;
    private bool _outcomeHandled = false;
    private bool _isTransitioning = false;
    private bool _serverConfirmedScriptedVictory = false;

    private int _chapterId = -1;
    private int _sceneId = -1;
    private bool _hasPlotSteps = false;
    private bool _usesServerVictoryOutcome = false;

    private GameObject _resultOverlayRoot;

    void Start()
    {
        TutorialContext.Reset();
        _hasPlotSteps = HasAnyPlotNodes();
        _usesServerVictoryOutcome = !_hasPlotSteps || HasReleaseToFreePlayNode() || !HasCompleteAndExitNode();

        // Only mark TutorialContext.IsActive when scripted tutorial nodes are present;
        // pure Challenge stages enter free play immediately.
        TutorialContext.IsActive = _hasPlotSteps;
        TutorialContext.IsFreePlayBattle = !_hasPlotSteps;

        if (sequencer == null)
            sequencer = GetComponent<PlotSequencer>() ?? FindAnyObjectByType<PlotSequencer>();
        if (tutorialTrigger == null)
            tutorialTrigger = GetComponent<TutorialTrigger>() ?? FindAnyObjectByType<TutorialTrigger>();

        if (sequencer != null)
            sequencer.SetAllowSkip(false);

        var replayUI = GetComponent<TutorialReplayUIController>() ?? FindAnyObjectByType<TutorialReplayUIController>();
        if (replayUI == null)
            replayUI = gameObject.AddComponent<TutorialReplayUIController>();
        replayUI.ConfigureDependencies(backendConfig, sequencer);
        replayUI.InitializeUI();

        StartCoroutine(InitBattleOverlayRoutine());
    }

    void OnDestroy()
    {
        if (controller != null)
        {
            controller.OnMatchReady -= StartTutorial;
            controller.OnStoryMatchEndedEvent -= HandleStoryMatchEnded;
        }
        PlotDirector.Clear();
        TutorialContext.Reset();
    }

    private bool HasAnyPlotNodes()
    {
        if (plots == null || plots.Count == 0) return false;
        for (int i = 0; i < plots.Count; i++)
        {
            if (plots[i]?.plot?.nodes != null && plots[i].plot.nodes.Count > 0)
                return true;
        }
        return false;
    }

    private bool HasReleaseToFreePlayNode()
    {
        if (plots == null) return false;
        for (int i = 0; i < plots.Count; i++)
        {
            var nodes = plots[i]?.plot?.nodes;
            if (nodes == null) continue;
            for (int n = 0; n < nodes.Count; n++)
            {
                if (nodes[n].type == PlotNodeType.ReleaseToFreePlay)
                    return true;
            }
        }
        return false;
    }

    private bool HasCompleteAndExitNode()
    {
        if (plots == null) return false;
        for (int i = 0; i < plots.Count; i++)
        {
            var nodes = plots[i]?.plot?.nodes;
            if (nodes == null) continue;
            for (int n = 0; n < nodes.Count; n++)
            {
                if (nodes[n].type == PlotNodeType.CompleteAndExit)
                    return true;
            }
        }
        return false;
    }

    private IEnumerator InitBattleOverlayRoutine()
    {
        if (controller == null)
            controller = FindAnyObjectByType<ClientController>();

        // Load the shared battle scene additively if not already in scene
        if (controller == null && !string.IsNullOrEmpty(additiveBattleSceneName))
        {
            var existingBattleScene = SceneManager.GetSceneByName(additiveBattleSceneName);
            if (!existingBattleScene.isLoaded)
            {
                Debug.Log($"[BattleTutorialStarter] Loading shared battle scene '{additiveBattleSceneName}' additively...");
                yield return SceneManager.LoadSceneAsync(additiveBattleSceneName, LoadSceneMode.Additive);
            }

            yield return null;
            controller = FindAnyObjectByType<ClientController>();
        }

        if (controller == null)
        {
            Debug.LogError("[BattleTutorialStarter] ClientController could not be found even after additive load!");
            yield break;
        }

        controller.OnStoryMatchEndedEvent -= HandleStoryMatchEnded;
        controller.OnStoryMatchEndedEvent += HandleStoryMatchEnded;

        ClientScene scene = controller.Scene != null ? controller.Scene : FindAnyObjectByType<ClientScene>();
        WireBattleReferences(scene);

        if (controller.IsMatchReady)
            StartTutorial();
        else
            controller.OnMatchReady += StartTutorial;
    }

    private void WireBattleReferences(ClientScene scene)
    {
        if (scene == null)
        {
            Debug.LogWarning("[BattleTutorialStarter] ClientScene not found when wiring battle references.");
            return;
        }

        if (sequencer != null)
            sequencer.BindBattleReferences(scene.cam, scene.clientInteractionSystem, scene.input);

        if (tutorialTrigger != null)
            tutorialTrigger.BindBattleReferences(scene.session, scene.clientInteractionSystem);

        BindBattleTutorialTargets(scene);
    }

    private void BindBattleTutorialTargets(ClientScene scene)
    {
        string sceneName = SceneManager.GetActiveScene().name;
        string scenePrefix = sceneName.Replace("_", "");

        int plotCh = ResolveChapterId();
        int plotSc = ResolveSceneId();
        string plotPrefix = (plotCh >= 0 && plotSc >= 0) ? $"C{plotCh}S{plotSc}" : null;

        var vc = scene.visualController;
        if (vc != null)
        {
            if (vc.ConfirmButtonCanvasGroup != null)
                EnsureTarget(vc.ConfirmButtonCanvasGroup.gameObject, "ConfirmButton", scenePrefix, plotPrefix);
            if (vc.myInstantStatus != null)
                EnsureTarget(vc.myInstantStatus.gameObject, "YourEye", scenePrefix, plotPrefix);
            if (vc.enemyInstantStatus != null)
                EnsureTarget(vc.enemyInstantStatus.gameObject, "EnemyEye", scenePrefix, plotPrefix);
            if (vc.cancelButtonLayer != null)
                EnsureTarget(vc.cancelButtonLayer, "CancelButton", scenePrefix, plotPrefix);
            if (vc.waitButtonLayer != null)
                EnsureTarget(vc.waitButtonLayer, "WaitButton", scenePrefix, plotPrefix);
            if (vc.InstantCounterText != null)
                EnsureTarget(vc.InstantCounterText.gameObject, "1", scenePrefix, plotPrefix);

            if (vc.CurrentUnitInfo != null && vc.CurrentUnitInfo.transform.parent != null)
                EnsureTarget(vc.CurrentUnitInfo.transform.parent.gameObject, "CharStat", scenePrefix, plotPrefix);

            if (vc.OwnedReadyUnitCanvasGroups != null)
            {
                for (int i = 0; i < vc.OwnedReadyUnitCanvasGroups.Length; i++)
                {
                    if (vc.OwnedReadyUnitCanvasGroups[i] != null)
                        EnsureTarget(vc.OwnedReadyUnitCanvasGroups[i].gameObject, $"Unit{i + 1}", scenePrefix, plotPrefix);
                }
                if (vc.OwnedReadyUnitCanvasGroups.Length > 0 &&
                    vc.OwnedReadyUnitCanvasGroups[0] != null &&
                    vc.OwnedReadyUnitCanvasGroups[0].transform.parent != null)
                {
                    EnsureTarget(vc.OwnedReadyUnitCanvasGroups[0].transform.parent.gameObject, "OwnedReadyBar", scenePrefix, plotPrefix);
                }
            }

            if (vc.EnemyReadyUnitCanvasGroups != null &&
                vc.EnemyReadyUnitCanvasGroups.Length > 0 &&
                vc.EnemyReadyUnitCanvasGroups[0] != null &&
                vc.EnemyReadyUnitCanvasGroups[0].transform.parent != null)
            {
                EnsureTarget(vc.EnemyReadyUnitCanvasGroups[0].transform.parent.gameObject, "3", scenePrefix, plotPrefix);
            }

            if (vc.LeftCommandLogAnimator != null && vc.LeftCommandLogAnimator.transform.parent != null)
            {
                var scrollBtn = vc.LeftCommandLogAnimator.transform.parent.Find("ScrollButton");
                if (scrollBtn != null)
                    EnsureTarget(scrollBtn.gameObject, "X", scenePrefix, plotPrefix);
            }
            if (TutorialTargetRegistry.Get("X") == null)
            {
                var scrollBtn = GameObject.Find("ScrollButton");
                if (scrollBtn != null)
                    EnsureTarget(scrollBtn, "X", scenePrefix, plotPrefix);
            }

            if (vc.ActionMenuUI != null)
            {
                var skillPanel = vc.ActionMenuUI.transform.Find("SkillPanel");
                if (skillPanel != null)
                    EnsureTarget(skillPanel.gameObject, "Skills", scenePrefix, plotPrefix);
            }
        }

        if (scene.InspectPanel != null)
            EnsureTarget(scene.InspectPanel, "InspectPanel", scenePrefix, plotPrefix);
        if (scene.EffectType != null)
            EnsureTarget(scene.EffectType.gameObject, "STYPE", scenePrefix, plotPrefix);
        if (scene.mainWeaponSkillSlot != null)
        {
            EnsureTarget(scene.mainWeaponSkillSlot.gameObject, "WSKILL", scenePrefix, plotPrefix);
            if (scene.mainWeaponSkillSlot.transform.parent != null &&
                scene.mainWeaponSkillSlot.transform.parent.parent != null)
            {
                EnsureTarget(scene.mainWeaponSkillSlot.transform.parent.parent.gameObject, "Skills", scenePrefix, plotPrefix);
            }
        }
        if (scene.classSkillSlot != null)
            EnsureTarget(scene.classSkillSlot.gameObject, "CSKILL", scenePrefix, plotPrefix);
        if (scene.trinketSkillSlot != null)
            EnsureTarget(scene.trinketSkillSlot.gameObject, "TSKILL", scenePrefix, plotPrefix);
        if (scene.uniquePassiveSlot != null)
            EnsureTarget(scene.uniquePassiveSlot.gameObject, "PSKILL", scenePrefix, plotPrefix);

        if (sequencer != null && sequencer.TextBoxPanel != null)
            EnsureTarget(sequencer.TextBoxPanel, "END", scenePrefix, plotPrefix);
    }

    private static void EnsureTarget(GameObject go, string canonicalId, string scenePrefix, string plotPrefix)
    {
        if (go == null || string.IsNullOrEmpty(canonicalId)) return;
        var target = go.GetComponent<TutorialTarget>();
        if (target == null)
            target = go.AddComponent<TutorialTarget>();

        string prefixedScene = !string.IsNullOrEmpty(scenePrefix) ? $"{scenePrefix}_{canonicalId}" : null;
        string prefixedPlot = !string.IsNullOrEmpty(plotPrefix) ? $"{plotPrefix}_{canonicalId}" : null;

        target.Configure(
            canonicalId,
            prefixedScene,
            prefixedPlot,
            $"C0S1_{canonicalId}",
            $"C0S2_{canonicalId}",
            $"C0S3_{canonicalId}");
    }

    void StartTutorial()
    {
        if (_tutorialStarted) return;
        _tutorialStarted = true;

        _chapterId = ResolveChapterId();
        _sceneId = ResolveSceneId();

        Debug.Log($"[BattleTutorialStarter] Match ready (ch={_chapterId}, sc={_sceneId}, hasPlots={_hasPlotSteps}, serverOutcome={_usesServerVictoryOutcome}).");

        if (_hasPlotSteps && sequencer != null)
            sequencer.SetBlockerOnly(true);

        bool forceCleanReset = true;
        bool waitForReset = false;
        int tokenBeforeReset = controller?.session != null ? controller.session.CurrentToken : -1;

        if (_sceneId >= 1 && controller != null && controller.bridge != null)
        {
            waitForReset = IsClientSnapshotDirtyForScene(_chapterId, _sceneId);
            controller.bridge.AdvanceTutorialPhaseServerRpc(_sceneId, forceCleanReset);
        }

        StartCoroutine(WaitForCountingInstantThenStart(waitForReset, tokenBeforeReset, _chapterId, _sceneId));
    }

    private int ResolveChapterId()
    {
        if (plots != null && plots.Count > 0 && plots[0].plot != null)
            return plots[0].plot.chapterId;

        string sceneName = SceneManager.GetActiveScene().name;
        int sIdx = sceneName.IndexOf("_S", System.StringComparison.OrdinalIgnoreCase);
        if (sceneName.StartsWith("C", System.StringComparison.OrdinalIgnoreCase) && sIdx > 1 &&
            int.TryParse(sceneName.Substring(1, sIdx - 1), out int parsedCh))
        {
            return parsedCh;
        }

        return PlayerSession.SelectedStoryChapterId;
    }

    private int ResolveSceneId()
    {
        if (plots != null && plots.Count > 0 && plots[0].plot != null && plots[0].plot.sceneId > 0)
            return plots[0].plot.sceneId;

        string sceneName = SceneManager.GetActiveScene().name;
        int sIdx = sceneName.IndexOf("_S", System.StringComparison.OrdinalIgnoreCase);
        if (sIdx >= 0 && int.TryParse(sceneName.Substring(sIdx + 2), out int parsed))
            return parsed;

        return PlayerSession.SelectedStorySceneId;
    }

    private bool IsClientSnapshotDirtyForScene(int chapterId, int sceneId)
    {
        var sess = controller?.session;
        if (sess == null) return false;

        if (sess.Timeline.currentInstant > 0) return true;

        if (chapterId == 0)
        {
            var ownedTeam = sess.GetOwnedTeamData();
            if (ownedTeam != null)
            {
                if (sceneId == 1 && ownedTeam.unitIds.Count != 1) return true;
                if ((sceneId == 2 || sceneId == 3) && ownedTeam.unitIds.Count != 2) return true;
            }
        }

        if (sess.units != null && sess.MapAsset != null && sess.teams != null)
        {
            for (int t = 0; t < sess.teams.Count; t++)
            {
                var team = sess.teams[t];
                if (team?.unitIds == null) continue;
                for (int i = 0; i < team.unitIds.Count; i++)
                {
                    var unit = sess.GetUnitDataById(team.unitIds[i]);
                    if (unit == null) continue;
                    Vector2Int spawnPt = sess.MapAsset.GetSpawn(team.teamId, i);
                    if (unit.CurrentCell.x != spawnPt.x || unit.CurrentCell.y != spawnPt.y)
                        return true;
                }
            }
        }

        return false;
    }

    private IEnumerator WaitForCountingInstantThenStart(bool waitForReset, int tokenBeforeReset, int chapterId, int sceneId)
    {
        if (waitForReset)
        {
            Debug.Log($"[BattleTutorialStarter] Waiting for server phase reset snapshot (token > {tokenBeforeReset})...");
            yield return new WaitUntil(() =>
                controller != null &&
                controller.session != null &&
                controller.session.CurrentToken > tokenBeforeReset &&
                controller.session.SyncState == SyncStateValue.Idle);
        }

        yield return new WaitUntil(() =>
            controller != null &&
            controller.session != null &&
            controller.session.SyncState == SyncStateValue.Idle &&
            controller.session.Timeline.isPaused &&
            controller.session.IsMyTurn() &&
            controller.session.ownedReadyUnitIds != null &&
            controller.session.ownedReadyUnitIds.Count > 0);

        if (!_hasPlotSteps || sequencer == null)
        {
            Debug.Log("[BattleTutorialStarter] Challenge mode (no blocking plots) — entering free tactical combat.");
            TutorialContext.ReleaseToFreePlay();
            yield break;
        }

        Debug.Log("[BattleTutorialStarter] Counting instant finished - starting plot sequencer.");

        foreach (var entry in plots)
        {
            if (entry.startTrigger == "PlayerTurn")
                entry.startTrigger = string.Empty;
        }

        sequencer.StartPlotWithTriggers(
            plots,
            onComplete: () =>
            {
                if (_outcomeHandled) return;
                _outcomeHandled = true;
                Debug.Log("[BattleTutorialStarter] Scripted tutorial plot complete - advancing to next scene.");
                AdvanceAfterVictory();
            },
            onReleasedToFreePlay: () =>
            {
                Debug.Log("[BattleTutorialStarter] Plot released to free tactical combat — awaiting Server match result.");
            });
    }

    // -------------------------------------------------------
    // Server Win / Loss Outcome & Unexpected Disconnect Handling
    // -------------------------------------------------------

    private void HandleStoryMatchEnded(bool playerWon, string failReason, int instantsUsed, int unitsLost)
    {
        if (_outcomeHandled) return;

        // Safeguard 1: If still running a guided PlotSequencer flow (not yet released to Free Play)
        // and the player won, do NOT cut off the final CompleteAndExit dialogue node — let PlotSequencer advance!
        if (playerWon && TutorialContext.IsActive && !TutorialContext.IsFreePlayBattle && HasCompleteAndExitNode())
        {
            _serverConfirmedScriptedVictory = true;
            TutorialContext.HasBattleEnded = true;
            Debug.Log("[BattleTutorialStarter] Server confirmed scripted tutorial victory — letting PlotSequencer finish final CompleteAndExit node.");
            return;
        }

        _outcomeHandled = true;
        TutorialContext.HasBattleEnded = true;

        if (sequencer != null)
            sequencer.StopAllCoroutines();

        StartCoroutine(WaitForVisualsAndPresentOutcome(playerWon, failReason, instantsUsed, unitsLost));
    }

    private IEnumerator WaitForVisualsAndPresentOutcome(bool playerWon, string failReason, int instantsUsed, int unitsLost)
    {
        // Let killing blow / death animations and resolve replay settle on the client before cutting away or showing overlay
        float settleStart = Time.realtimeSinceStartup;
        const float minSettleSeconds = 2.0f;
        const float maxWaitSeconds = 5.0f;

        while (Time.realtimeSinceStartup - settleStart < minSettleSeconds)
            yield return null;

        var sess = controller != null ? controller.session : null;
        while (sess != null &&
               (sess.SyncState != SyncStateValue.Idle || sess.IsSceneSyncing) &&
               Time.realtimeSinceStartup - settleStart < maxWaitSeconds)
        {
            yield return null;
        }

        if (playerWon)
        {
            Debug.Log($"[BattleTutorialStarter] Victory settled! (Instants={instantsUsed}, Lost={unitsLost})");
            AdvanceAfterVictory();
        }
        else
        {
            Debug.Log($"[BattleTutorialStarter] Defeat settled: {failReason}");
            ShowDefeatOverlay(failReason, instantsUsed, unitsLost);
        }
    }

    /// <summary>
    /// Called by ClientMenuReturner if the dedicated server disconnects unexpectedly
    /// (for example if the player loses or times out before RPC arrives, or after scripted EndMatch).
    /// </summary>
    public void HandleUnexpectedDisconnect()
    {
        if (_outcomeHandled || _isTransitioning) return;

        // If server already reported scripted victory and PlotSequencer is still showing the final CompleteAndExit text,
        // do nothing — the player's click on CompleteAndExit will perform HTTP CompleteAndAdvance.
        if (_serverConfirmedScriptedVictory && TutorialContext.IsActive && !TutorialContext.IsFreePlayBattle)
            return;

        var sess = controller != null ? controller.session : null;
        var ownedTeam = sess?.GetOwnedTeamData();
        var enemyTeam = sess?.GetEnemyTeamData();
        bool appearsWon = enemyTeam != null && enemyTeam.unitIds.Count == 0 && ownedTeam != null && ownedTeam.unitIds.Count > 0;

        if (appearsWon)
        {
            HandleStoryMatchEnded(true, string.Empty, sess != null ? sess.Timeline.currentInstant : 0, 0);
        }
        else if (TutorialContext.IsFreePlayBattle || _usesServerVictoryOutcome)
        {
            HandleStoryMatchEnded(false, "Connection to battle server ended.", sess != null ? sess.Timeline.currentInstant : 0, 0);
        }
    }

    private void AdvanceAfterVictory()
    {
        if (_isTransitioning) return;
        _isTransitioning = true;
        StartCoroutine(AdvanceAfterVictoryRoutine());
    }

    private IEnumerator AdvanceAfterVictoryRoutine()
    {
        // If this match was settled by Server EndMatch (Hybrid/Challenge or final scripted victory),
        // tear down the dying client transport before transitioning so the next stage spawns/connects cleanly.
        if (_usesServerVictoryOutcome || TutorialContext.HasBattleEnded)
            yield return StartCoroutine(ShutdownNetworkIfListening());

        int ch = _chapterId >= 0 ? _chapterId : ResolveChapterId();
        int sc = _sceneId > 0 ? _sceneId : ResolveSceneId();

        yield return StartCoroutine(StoryClient.CompleteAndAdvance(
            backendConfig,
            this,
            nextProgress =>
            {
                TutorialContext.Reset();
                StoryClient.RouteToStoryScene(nextProgress, backendConfig, this);
            },
            () =>
            {
                Debug.LogError("[BattleTutorialStarter] Failed to advance story after victory.");
                TutorialContext.Reset();
                string fallback = Application.CanStreamedLevelBeLoaded("10_StoryProgress") ? "10_StoryProgress" : "3_MainMenu";
                SceneManager.LoadScene(fallback);
            },
            ch,
            sc));
    }

    // -------------------------------------------------------
    // Runtime Defeat / Retry / Exit Overlay
    // -------------------------------------------------------

    private void ShowDefeatOverlay(string failReason, int instantsUsed, int unitsLost)
    {
        if (_resultOverlayRoot != null)
        {
            _resultOverlayRoot.SetActive(true);
            return;
        }

        Canvas targetCanvas = FindAnyObjectByType<Canvas>();
        if (targetCanvas == null)
        {
            var canvasGo = new GameObject("StoryOutcomeCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            targetCanvas = canvasGo.GetComponent<Canvas>();
            targetCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            targetCanvas.sortingOrder = 2000;
        }

        TMP_FontAsset sharedFont = null;
        var anyTmp = FindAnyObjectByType<TMP_Text>();
        if (anyTmp != null) sharedFont = anyTmp.font;

        _resultOverlayRoot = new GameObject("StoryDefeatOverlay", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster), typeof(Image));
        _resultOverlayRoot.transform.SetParent(targetCanvas.transform, false);
        var rootCanvas = _resultOverlayRoot.GetComponent<Canvas>();
        rootCanvas.overrideSorting = true;
        rootCanvas.sortingOrder = 2000;

        var rootRt = _resultOverlayRoot.GetComponent<RectTransform>();
        rootRt.anchorMin = Vector2.zero;
        rootRt.anchorMax = Vector2.one;
        rootRt.offsetMin = Vector2.zero;
        rootRt.offsetMax = Vector2.zero;

        var bgImg = _resultOverlayRoot.GetComponent<Image>();
        bgImg.color = new Color(0.03f, 0.03f, 0.06f, 0.82f);

        // Card container
        var cardGo = new GameObject("DefeatCard", typeof(RectTransform), typeof(Image));
        cardGo.transform.SetParent(_resultOverlayRoot.transform, false);
        var cardRt = cardGo.GetComponent<RectTransform>();
        cardRt.anchorMin = new Vector2(0.5f, 0.5f);
        cardRt.anchorMax = new Vector2(0.5f, 0.5f);
        cardRt.sizeDelta = new Vector2(680f, 380f);
        cardGo.GetComponent<Image>().color = new Color(0.11f, 0.09f, 0.14f, 0.98f);

        CreateLabel(cardGo.transform, "Title", "DEFEAT", sharedFont, 46f, FontStyles.Bold,
            new Color(0.95f, 0.35f, 0.35f, 1f), new Vector2(0f, 115f), new Vector2(600f, 70f));

        string reasonDisplay = string.IsNullOrEmpty(failReason)
            ? "Your squad could not overcome the enemy."
            : failReason;
        CreateLabel(cardGo.transform, "Reason", reasonDisplay, sharedFont, 24f, FontStyles.Normal,
            new Color(0.92f, 0.92f, 0.95f, 1f), new Vector2(0f, 35f), new Vector2(600f, 80f));

        CreateLabel(cardGo.transform, "Stats", $"Instants Elapsed: {instantsUsed}   |   Allies Fallen: {unitsLost}",
            sharedFont, 20f, FontStyles.Normal, new Color(0.76f, 0.72f, 0.60f, 1f),
            new Vector2(0f, -35f), new Vector2(600f, 40f));

        CreateActionButton(cardGo.transform, "RetryButton", "RETRY", sharedFont,
            new Color(0.24f, 0.48f, 0.28f, 1f), new Vector2(-145f, -120f), OnRetryClicked);

        CreateActionButton(cardGo.transform, "ExitButton", "EXIT TO MAP", sharedFont,
            new Color(0.42f, 0.18f, 0.18f, 1f), new Vector2(145f, -120f), OnExitToMapClicked);
    }

    private static TMP_Text CreateLabel(
        Transform parent, string name, string text, TMP_FontAsset font,
        float fontSize, FontStyles style, Color color, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;

        var tmp = go.GetComponent<TextMeshProUGUI>();
        if (font != null) tmp.font = font;
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.fontStyle = style;
        tmp.color = color;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.textWrappingMode = TextWrappingModes.Normal;
        tmp.raycastTarget = false;
        return tmp;
    }

    private static Button CreateActionButton(
        Transform parent, string name, string label, TMP_FontAsset font,
        Color bgColor, Vector2 pos, UnityEngine.Events.UnityAction onClick)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(230f, 64f);

        go.GetComponent<Image>().color = bgColor;
        var btn = go.GetComponent<Button>();
        btn.onClick.AddListener(onClick);

        CreateLabel(go.transform, "Label", label, font, 24f, FontStyles.Bold,
            Color.white, Vector2.zero, new Vector2(210f, 54f));
        return btn;
    }

    private void OnRetryClicked()
    {
        if (_isTransitioning) return;
        _isTransitioning = true;
        StartCoroutine(RetryBattleRoutine());
    }

    private void OnExitToMapClicked()
    {
        if (_isTransitioning) return;
        _isTransitioning = true;
        StartCoroutine(ExitToMapRoutine());
    }

    private IEnumerator ShutdownNetworkIfListening()
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            NetworkManager.Singleton.Shutdown();
            float wait = 0f;
            while (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening && wait < 2f)
            {
                wait += Time.unscaledDeltaTime;
                yield return null;
            }
        }
    }

    private IEnumerator RetryBattleRoutine()
    {
        yield return StartCoroutine(ShutdownNetworkIfListening());

        int ch = _chapterId >= 0 ? _chapterId : ResolveChapterId();
        int sc = _sceneId > 0 ? _sceneId : ResolveSceneId();
        bool isReplay = PlayerSession.IsStoryReplay;
        bool wasAutoResumed = PlayerSession.IsAutoResumed;

        bool matchReady = false;
        if (isReplay)
        {
            yield return StartCoroutine(StoryClient.StartSelectedStoryMatch(
                backendConfig, ch, sc, autoResume: false,
                data =>
                {
                    PlayerSession.IsStoryReplay = true;
                    PlayerSession.IsAutoResumed = wasAutoResumed;
                    matchReady = !string.IsNullOrEmpty(data.matchId);
                },
                err => Debug.LogError($"[BattleTutorialStarter] Retry replay match failed: {err}")));
        }
        else
        {
            yield return StartCoroutine(StoryClient.StartStoryMatch(
                backendConfig,
                match =>
                {
                    PlayerSession.MatchId = match.matchId;
                    PlayerSession.ServerIp = match.serverIp;
                    PlayerSession.ServerPort = match.serverPort;
                    matchReady = true;
                },
                () => Debug.LogError("[BattleTutorialStarter] Retry story match failed.")));
        }

        TutorialContext.Reset();
        if (matchReady)
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }
        else
        {
            string fallback = Application.CanStreamedLevelBeLoaded("10_StoryProgress") ? "10_StoryProgress" : "3_MainMenu";
            SceneManager.LoadScene(fallback);
        }
    }

    private IEnumerator ExitToMapRoutine()
    {
        yield return StartCoroutine(ShutdownNetworkIfListening());
        if (backendConfig != null)
            yield return StartCoroutine(StoryClient.TerminateStoryMatch(backendConfig));

        PlayerSession.IsStoryReplay = false;
        PlayerSession.IsAutoResumed = false;
        TutorialContext.Reset();
        string dest = Application.CanStreamedLevelBeLoaded("10_StoryProgress") ? "10_StoryProgress" : "3_MainMenu";
        SceneManager.LoadScene(dest);
    }
}
