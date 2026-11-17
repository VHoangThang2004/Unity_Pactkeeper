using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Chapter 0 Scene 6 - Main menu tutorial (Dialogue type).
/// Loads 3_MainMenu additively underneath, runs the plot sequence,
/// then completes story progress and unloads.
///
/// Uses TutorialContext.IsActive = true before additive load so
/// MainMenuManager skips its story-check loop.
/// </summary>
public class StorySceneWithAdditiveManager : MonoBehaviour
{
    [Header("Config")]
    [SerializeField] private BackendConfig config;
    [SerializeField] private string additiveSceneName = "3_MainMenu";

    [Header("Refs")]
    [SerializeField] private PlotSequencer sequencer;

    void Start()
    {
        StartCoroutine(Run());
    }

    IEnumerator Run()
    {
        // Signal to MainMenuManager: skip story check, you're a subscene
        TutorialContext.IsActive = true;

        var replayUI = GetComponent<TutorialReplayUIController>() ?? FindAnyObjectByType<TutorialReplayUIController>();
        if (replayUI == null)
            replayUI = gameObject.AddComponent<TutorialReplayUIController>();
        replayUI.ConfigureDependencies(config, sequencer);
        replayUI.InitializeUI();

        // Load 3_MainMenu additively - player sees the real menu underneath
        yield return SceneManager.LoadSceneAsync(additiveSceneName, LoadSceneMode.Additive);

        // Wait a frame for all Awake/Start/OnEnable calls in 3_MainMenu to complete
        yield return null;
        yield return null;

        // Start the tutorial plot
        bool done = false;
        sequencer.StartPlot(() => done = true);
        yield return new WaitUntil(() => done);

        // Plot complete - mark scene done on backend then return to main menu or story map.
        int chapter = sequencer != null && sequencer.Plot != null ? sequencer.Plot.chapterId : 0;
        int scene = sequencer != null && sequencer.Plot != null ? sequencer.Plot.sceneId : 4;

        yield return StartCoroutine(StoryClient.CompleteAndAdvance(
            config, this,
            next =>
            {
                TutorialContext.Reset();
                StoryClient.RouteToStoryScene(next, config, this);
            },
            () =>
            {
                Debug.LogWarning($"[StorySceneWithAdditive] Failed to complete story progress for C{chapter}_S{scene}.");
                TutorialContext.Reset();
                SceneManager.LoadScene("3_MainMenu");
            },
            chapterId: chapter,
            sceneId: scene
        ));
    }
}
