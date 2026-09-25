using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Chapter 0 Scene 2 — Main menu tutorial (Dialogue type).
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

        // Load 3_MainMenu additively — player sees the real menu underneath
        yield return SceneManager.LoadSceneAsync(additiveSceneName, LoadSceneMode.Additive);

        // Wait a frame for all Awake/Start/OnEnable calls in 3_MainMenu to complete
        yield return null;
        yield return null;

        // Start the tutorial plot
        bool done = false;
        sequencer.StartPlot(() => done = true);
        yield return new WaitUntil(() => done);

        // Plot complete — mark scene done on backend then return to main menu.
        // Main menu will resume to next scene (e.g. C0_S3) automatically
        // via the normal story progress check flow.
        yield return StartCoroutine(StoryClient.CompleteAndAdvance(
            config, this,
            next =>
            {
                TutorialContext.Reset();
                SceneManager.LoadScene("3_MainMenu");
            },
            () =>
            {
                Debug.LogWarning("[C0_S2] Failed to complete story progress.");
                TutorialContext.Reset();
                SceneManager.LoadScene("3_MainMenu");
            }
        ));
    }
}