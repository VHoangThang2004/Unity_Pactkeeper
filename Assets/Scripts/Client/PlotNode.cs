using UnityEngine;

public enum PlotNodeType
{
    TextBox,        // Show text panel, player clicks Next to continue
    GuidedClick,    // Highlight a real button, player MUST click it to advance
    AutoAdvance,    // No input — auto-moves to next node after a delay
    CompleteAndExit // Final button — marks story progress complete and reloads main menu
}

[System.Serializable]
public class PlotNode
{
    [Header("Type")]
    public PlotNodeType type;

    [Header("TextBox / GuidedClick")]
    [TextArea(2, 6)]
    public string text;                 // Dialogue or hint text shown to player

    [Header("GuidedClick or CompleteAndExit only")]
    public string targetButtonId;       // Matches TutorialTarget.targetId in the additive scene
    public string highlightHint;        // Optional secondary text, e.g. "Click here to continue"

    [Header("AutoAdvance only")]
    public float delay = 1.5f;          // Seconds before auto-advancing
}