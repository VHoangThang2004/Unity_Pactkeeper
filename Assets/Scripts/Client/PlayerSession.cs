/// <summary>
/// Stores the current player's session data after login.
/// Accessible from anywhere. Cleared on logout or disconnect.
/// </summary>
public static class PlayerSession
{
    public static string Token    { get; set; } = string.Empty;
    public static string Username { get; set; } = string.Empty;
    public static string PlayerId { get; set; } = string.Empty;
    public static string Role     { get; set; } = string.Empty;  // "Player" | "Admin"
    public static string MatchId  { get; set; } = string.Empty;
    public static string ServerIp { get; set; } = string.Empty;
    public static int    ServerPort { get; set; } = 7777;

    public static bool IsLoggedIn => !string.IsNullOrEmpty(Token);
    public static bool IsAdmin    => Role.Equals("Admin", System.StringComparison.OrdinalIgnoreCase);
    public static string SelectedOwnedUnitId { get; set; } = string.Empty;

    // ── Story Mode & Replay Session Flags ────────────────────────────────────
    /// <summary>
    /// True ONLY when the player is playing a Chapter they have already completed
    /// (targetChapterId <= MaxCompletedChapterId). Controls visibility of "Skip Scene" & "Exit to Map".
    /// </summary>
    public static bool CanSkipOrExitStory { get; set; } = false;

    /// <summary>
    /// False whenever a player manually selects a Chapter/Scene from 10_StoryProgress.
    /// Forces BattleTutorialStarter to request a clean board reset from Instant 0.
    /// </summary>
    public static bool IsAutoResumed { get; set; } = false;

    public static int SelectedStoryChapterId { get; set; } = 0;
    public static int SelectedStorySceneId   { get; set; } = 1;

    public static void Clear()
    {
        Token    = string.Empty;
        Username = string.Empty;
        PlayerId = string.Empty;
        Role     = string.Empty;
        MatchId  = string.Empty;
        SelectedOwnedUnitId = string.Empty;
        CanSkipOrExitStory  = false;
        IsAutoResumed       = false;
        SelectedStoryChapterId = 0;
        SelectedStorySceneId   = 1;
    }
}
