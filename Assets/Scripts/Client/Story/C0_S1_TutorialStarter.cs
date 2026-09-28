using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Wires up the tutorial for C0_S1 (tutorial match scene).
/// Uses StartPlotWithTriggers so each plot starts at the right game moment.
/// Add to C0_S1 scene alongside TutorialTrigger.
/// </summary>
public class C0_S1_TutorialStarter : MonoBehaviour
{
    [SerializeField] private ClientController controller;
    [SerializeField] private PlotSequencer sequencer;

    [Tooltip("List of plots and their trigger conditions. " +
             "Leave startTrigger empty to start immediately on match ready.")]
    [SerializeField] private List<PlotSequencer.PlotEntry> plots;


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
        Debug.Log("[C0_S1_TutorialStarter] Match ready — starting trigger-based tutorial.");
        sequencer.StartPlotWithTriggers(plots);
    }
}