using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Chapter 0 Scene 5 — Unit List tutorial (Dialogue type).
/// Loads 7_UnitList additively underneath, runs the plot sequence,
/// then completes story progress and advances to C0_S6.
///
/// Uses TutorialContext.IsActive = true before additive load so
/// UnitListManager skips its own init flow.
/// </summary>
public class C0_S5_Manager : MonoBehaviour
{
    [Header("Config")]
    [SerializeField] private BackendConfig config;
    [SerializeField] private string additiveSceneName = "7_UnitList";

    [Header("Refs")]
    [SerializeField] private PlotSequencer sequencer;

    void Start()
    {
        StartCoroutine(Run());
    }

    IEnumerator Run()
    {
        // Signal to UnitListManager: skip normal flow, you're a subscene
        TutorialContext.IsActive = true;

        // Load 7_UnitList additively — player sees the real unit list underneath
        yield return SceneManager.LoadSceneAsync(additiveSceneName, LoadSceneMode.Additive);

        // Wait a frame for all Awake/Start/OnEnable calls in 7_UnitList to complete
        yield return null;
        yield return null;

        // Start the tutorial plot
        bool done = false;
        sequencer.StartPlot(() => done = true);
        yield return new WaitUntil(() => done);

        // Plot complete — mark scene done on backend then advance.
        yield return StartCoroutine(StoryClient.CompleteAndAdvance(
            config, this,
            next =>
            {
                TutorialContext.Reset();
                StoryClient.RouteToStoryScene(next);
            },
            () =>
            {
                Debug.LogWarning("[C0_S5] Failed to complete story progress — returning to main menu.");
                TutorialContext.Reset();
                SceneManager.LoadScene("3_MainMenu");
            }
        ));
    }
}
