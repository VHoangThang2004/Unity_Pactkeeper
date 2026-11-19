/// <summary>
/// Static context set by a story scene before loading an additive subscene.
/// Allows additively-loaded scenes (e.g. 3_MainMenu, 5_ClientMatch) to
/// know how the current story/tutorial scene is operating.
/// </summary>
public static class TutorialContext
{
    // True while PlotSequencer actively restricts or guides UI/battle interaction.
    public static bool IsActive { get; set; } = false;

    // True once a Hybrid or Challenge stage releases control to free tactical play against AI.
    public static bool IsFreePlayBattle { get; set; } = false;

    // True once the dedicated server has reported the outcome of the story match.
    public static bool HasBattleEnded { get; set; } = false;

    // Optional: set by the story scene before loading additive subscene
    // if the tutorial needs to show fake data instead of real backend data.
    public static bool UseFakeData { get; set; } = false;

    public static void ReleaseToFreePlay()
    {
        IsActive = false;
        IsFreePlayBattle = true;
    }

    public static void Reset()
    {
        IsActive = false;
        IsFreePlayBattle = false;
        HasBattleEnded = false;
        UseFakeData = false;
    }
}
