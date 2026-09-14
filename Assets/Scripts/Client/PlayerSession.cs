/// <summary>
/// Stores the current player's session data after login.
/// Accessible from anywhere. Cleared on logout or disconnect.
/// </summary>
public static class PlayerSession
{
    public static string Token { get; set; } = string.Empty;
    public static string Username { get; set; } = string.Empty;

    public static bool IsLoggedIn => !string.IsNullOrEmpty(Token);

    public static void Clear()
    {
        Token = string.Empty;
        Username = string.Empty;
    }
}