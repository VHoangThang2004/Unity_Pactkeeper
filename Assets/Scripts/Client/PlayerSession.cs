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

    public static void Clear()
    {
        Token    = string.Empty;
        Username = string.Empty;
        PlayerId = string.Empty;
        Role     = string.Empty;
        MatchId  = string.Empty;
        SelectedOwnedUnitId = string.Empty;
    }
}