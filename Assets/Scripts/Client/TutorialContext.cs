/// <summary>
/// Static context set by a story scene before loading an additive subscene.
/// Allows additively-loaded scenes (e.g. 3_MainMenu loaded under C0_S2) to
/// know they're running inside a tutorial and behave accordingly:
/// - Skip story progress check in MainMenuManager
/// - Use local fake data instead of fetching from backend
/// - Disable interaction that isn't part of the tutorial
///
/// Reset to defaults when the tutorial scene is done.
/// </summary>
public static class TutorialContext
{
    // True when a story scene has loaded a subscene additively.
    // MainMenuManager checks this to skip the story progress check
    // and avoid the infinite reload loop.
    public static bool IsActive { get; set; } = false;

    // Optional: set by the story scene before loading additive subscene
    // if the tutorial needs to show fake data instead of real backend data.
    // Currently unused for C0_S2 (text-only), used later for C0_S3 (unit list).
    public static bool UseFakeData { get; set; } = false;

    public static void Reset()
    {
        IsActive = false;
        UseFakeData = false;
    }
}