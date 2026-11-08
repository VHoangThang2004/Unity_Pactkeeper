using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Wires up the tutorial overlay for any Story PvE battle scene.
/// Supports running as a lightweight overlay scene that loads 5_ClientMatch additively
/// (just like C0_S4..C0_S6 load 3_MainMenu/7_UnitList/8_UnitConfig), dynamically resolving
/// all battle references and registering TutorialTarget IDs onto the shared battle UI.
/// </summary>
public class BattleTutorialStarter : MonoBehaviour
{
    [SerializeField] private ClientController controller;
    [SerializeField] private PlotSequencer sequencer;
    [UnityEngine.Serialization.FormerlySerializedAs("trigger")]
    [SerializeField] private TutorialTrigger tutorialTrigger;
    [SerializeField] private string additiveBattleSceneName = "5_ClientMatch";

    [Tooltip("List of plots and their trigger conditions. " +
             "Leave startTrigger empty to start immediately on match ready.")]
    [SerializeField] private List<PlotSequencer.PlotEntry> plots;

    [SerializeField] private BackendConfig backendConfig;

    private bool _tutorialStarted = false;

    void Start()
    {
        TutorialContext.IsActive = true;

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
            controller.OnMatchReady -= StartTutorial;
        PlotDirector.Clear();
        TutorialContext.Reset();
    }

    private IEnumerator InitBattleOverlayRoutine()
    {
        if (controller == null)
            controller = FindAnyObjectByType<ClientController>();

        // If ClientController is not in the current scene, load the shared battle scene additively
        if (controller == null && !string.IsNullOrEmpty(additiveBattleSceneName))
        {
            var existingBattleScene = SceneManager.GetSceneByName(additiveBattleSceneName);
            if (!existingBattleScene.isLoaded)
            {
                Debug.Log($"[BattleTutorialStarter] Loading shared battle scene '{additiveBattleSceneName}' additively...");
                yield return SceneManager.LoadSceneAsync(additiveBattleSceneName, LoadSceneMode.Additive);
            }

            // Wait a frame for Awake/OnEnable in the additive battle scene to finish
            yield return null;
            controller = FindAnyObjectByType<ClientController>();
        }

        if (controller == null)
        {
            Debug.LogError("[BattleTutorialStarter] ClientController could not be found even after additive load!");
            yield break;
        }

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
        string sceneName = SceneManager.GetActiveScene().name; // e.g. "C0_S1", "C1_S2"
        string scenePrefix = sceneName.Replace("_", "");       // e.g. "C0S1", "C1S2"

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

        Debug.Log("[BattleTutorialStarter] Match ready - preparing tutorial.");

        // Keep input blocked while timeline counts down to the first decision window
        if (sequencer != null)
            sequencer.SetBlockerOnly(true);

        int chapterId = ResolveChapterId();
        int sceneId = ResolveSceneId();
        bool forceCleanReset = !PlayerSession.IsAutoResumed;
        bool waitForReset = false;
        int tokenBeforeReset = controller?.session != null ? controller.session.CurrentToken : -1;

        if (sceneId >= 1 && controller != null && controller.bridge != null)
        {
            waitForReset = forceCleanReset || IsClientSnapshotDirtyForScene(chapterId, sceneId);
            controller.bridge.AdvanceTutorialPhaseServerRpc(sceneId, forceCleanReset);
        }

        StartCoroutine(WaitForCountingInstantThenStart(waitForReset, tokenBeforeReset, chapterId, sceneId));
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

        return -1;
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

        // Wait until timeline finishes counting instant (timeline is paused, player turn, and ready units exist)
        yield return new WaitUntil(() =>
            controller != null &&
            controller.session != null &&
            controller.session.SyncState == SyncStateValue.Idle &&
            controller.session.Timeline.isPaused &&
            controller.session.IsMyTurn() &&
            controller.session.ownedReadyUnitIds != null &&
            controller.session.ownedReadyUnitIds.Count > 0);

        Debug.Log("[BattleTutorialStarter] Counting instant finished - starting plot sequencer.");

        if (plots != null)
        {
            foreach (var entry in plots)
            {
                if (entry.startTrigger == "PlayerTurn")
                    entry.startTrigger = string.Empty;
            }
        }

        sequencer.StartPlotWithTriggers(plots, () =>
        {
            Debug.Log("[BattleTutorialStarter] Tutorial plot complete - advancing to the next scene.");
            StartCoroutine(StoryClient.CompleteAndAdvance(
                backendConfig,
                this,
                nextProgress =>
                {
                    TutorialContext.Reset();
                    StoryClient.RouteToStoryScene(nextProgress, backendConfig, this);
                },
                () =>
                {
                    Debug.LogError("[BattleTutorialStarter] Failed to advance story");
                    TutorialContext.Reset();
                    SceneManager.LoadScene("3_MainMenu");
                },
                chapterId,
                sceneId));
        });
    }
}
