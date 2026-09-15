using UnityEngine;

[CreateAssetMenu(fileName = "BackendConfig", menuName = "SRPG/Backend Config")]
public class BackendConfig : ScriptableObject
{
    [Header("Backend")]
    public string backendUrl = "http://localhost:5276";

    [Header("Game Server")]
    public string gameServerIp = "127.0.0.1";
    public int gameServerPort = 7777;

    public void SetHeaders(UnityEngine.Networking.UnityWebRequest request, string token = "")
    {
        request.SetRequestHeader("ngrok-skip-browser-warning", "true");
        if (!string.IsNullOrEmpty(token))
            request.SetRequestHeader("Authorization", $"Bearer {token}");
    }
}