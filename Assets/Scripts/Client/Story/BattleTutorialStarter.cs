using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Wires up the tutorial for any tutorial match scene.
/// Uses StartPlotWithTriggers so each plot starts at the right game moment.
/// Add to tutorial scene alongside TutorialTrigger.
/// </summary>
public class BattleTutorialStarter : MonoBehaviour
{
    [SerializeField] private ClientController controller;
    [SerializeField] private PlotSequencer sequencer;

    [Tooltip("List of plots and their trigger conditions. " +
             "Leave startTrigger empty to start immediately on match ready.")]
    [SerializeField] private List<PlotSequencer.PlotEntry> plots;


    [SerializeField] private BackendConfig backendConfig;

    void Start()
    {
        controller.OnMatchReady += StartTutorial;
    }

    void OnDestroy()
    {
        if (controller != null)
            controller.OnMatchReady -= StartTutorial;
        PlotDirector.Clear();
    }

    void StartTutorial()
    {
        Debug.Log("[BattleTutorialStarter] Match ready — preparing tutorial.");

        // Tell the server which phase we are on so it can spawn any missing units
        string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        if (sceneName.StartsWith("C0_S") && int.TryParse(sceneName.Substring(4), out int sceneId))
        {
            if (controller.bridge != null && sceneId > 1)
            {
                controller.bridge.AdvanceTutorialPhaseServerRpc(sceneId);
            }
        }

        StartCoroutine(WaitForCountingInstantThenStart());
    }

    private System.Collections.IEnumerator WaitForCountingInstantThenStart()
    {
        // Wait until timeline finishes counting instant (timeline is paused, player turn, and ready units exist)
        // Exactly like C0S1
        yield return new WaitUntil(() =>
            controller != null &&
            controller.session != null &&
            controller.session.Timeline.isPaused &&
            controller.session.IsMyTurn() &&
            controller.session.ownedReadyUnitIds != null &&
            controller.session.ownedReadyUnitIds.Count > 0);

        Debug.Log("[BattleTutorialStarter] Counting instant finished — starting plot sequencer.");

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
            StartCoroutine(StoryClient.CompleteAndAdvance(backendConfig, this,
                nextProgress => StoryClient.RouteToStoryScene(nextProgress),
                () => Debug.LogError("[BattleTutorialStarter] Failed to advance story")));
        });
    }
}