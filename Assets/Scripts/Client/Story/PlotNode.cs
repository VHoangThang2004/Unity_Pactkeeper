using UnityEngine;

public enum PlotNodeType
{
    TextBox,            // Show text panel, player clicks Next to continue
    GuidedClick,        // Highlight a real UI button, player MUST click it to advance
    GuidedCellClick,    // Highlight a world-space grid cell, player MUST click it to advance
    AutoAdvance,        // No input — auto-moves to next node after a delay
    CompleteAndExit     // Final button — marks story progress complete and reloads main menu
}

[System.Serializable]
public class PlotNode
{
    [Header("Type")]
    public PlotNodeType type;

    [Header("TextBox / GuidedClick")]
    [TextArea(2, 6)]
    public string text;

    [Header("GuidedClick / CompleteAndExit")]
    public string tutorialTargetId;       // Matches TutorialTarget.targetId in the additive scene

    [Header("GuidedCellClick")]
    public Vector2Int targetCell;       // World-space grid cell (x, y)

    [Header("AdvanceDelay")]
    public float delay = 0.5f;

    [Header("Timeout")]
    [Tooltip("Seconds to wait for target to register before reloading scene.")]
    public float targetWaitTimeout = 10f;
}