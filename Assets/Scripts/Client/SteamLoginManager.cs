using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using Steamworks;
using System.Text;

public class SteamLoginManager : MonoBehaviour
{
    [Header("Config")]
    [SerializeField] private BackendConfig config;
    [SerializeField] private string mainMenuScene = "2_MainMenu";

    private bool steamInitialized = false;

    // -------------------------------------------------------
    // Init
    // -------------------------------------------------------

    void Awake()
    {
        if (!SteamAPI.Init())
        {
            Debug.LogError("[SteamLogin] SteamAPI.Init() failed! Is Steam running?");
            return;
        }

        steamInitialized = true;
        Debug.Log("[SteamLogin] Steam initialized.");
    }

    void OnDestroy()
    {
        if (steamInitialized)
            SteamAPI.Shutdown();
    }

    // -------------------------------------------------------
    // Login
    // -------------------------------------------------------

    public void OnClickSteamLogin()
    {
        if (!steamInitialized)
        {
            Debug.LogError("[SteamLogin] Steam not initialized!");
            return;
        }

        StartCoroutine(SteamLoginRoutine());
    }

    IEnumerator SteamLoginRoutine()
    {
        Debug.Log("[SteamLogin] Getting Steam session ticket...");

        // Get session ticket
        byte[] ticketBuffer = new byte[1024];
        uint ticketSize = 0;
        SteamNetworkingIdentity identity = new SteamNetworkingIdentity();
        identity.SetSteamID(SteamUser.GetSteamID());
        HAuthTicket handle = SteamUser.GetAuthSessionTicket(ticketBuffer, ticketBuffer.Length, out ticketSize, ref identity);

        if (handle == HAuthTicket.Invalid)
        {
            Debug.LogError("[SteamLogin] Failed to get auth ticket!");
            yield break;
        }

        // Wait for Steam to register the ticket
        yield return new WaitForSeconds(1f);
        SteamAPI.RunCallbacks();
        yield return new WaitForSeconds(0.5f);

        // Convert to hex string
        StringBuilder sb = new StringBuilder();
        for (int i = 0; i < ticketSize; i++)
            sb.AppendFormat("{0:x2}", ticketBuffer[i]);

        string ticket = sb.ToString();
        Debug.Log($"[SteamLogin] Got ticket ({ticketSize} bytes)");

        // Send to backend
        yield return StartCoroutine(SendTicketToBackend(ticket));
    }

    IEnumerator SendTicketToBackend(string ticket)
    {
        string json = $"{{\"steamTicket\":\"{ticket}\"}}";
        byte[] body = Encoding.UTF8.GetBytes(json);

        using var request = new UnityEngine.Networking.UnityWebRequest(
            $"{config.backendUrl}/api/Auth/steam", "POST");
        request.uploadHandler = new UnityEngine.Networking.UploadHandlerRaw(body);
        request.downloadHandler = new UnityEngine.Networking.DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");
        config.SetHeaders(request);
        
        yield return request.SendWebRequest();

        if (request.result != UnityEngine.Networking.UnityWebRequest.Result.Success)
        {
            Debug.LogError($"[SteamLogin] Backend request failed: {request.error}");
            yield break;
        }

        string response = request.downloadHandler.text;
        Debug.Log($"[SteamLogin] Backend response: {response}");

        // Parse JWT from response
        var authResponse = JsonUtility.FromJson<AuthResponse>(response);
        if (string.IsNullOrEmpty(authResponse.token))
        {
            Debug.LogError("[SteamLogin] No token in response!");
            yield break;
        }

        // Store JWT
        PlayerSession.Token = authResponse.token;
        PlayerSession.Username = authResponse.username;

        Debug.Log($"[SteamLogin] Logged in as {authResponse.username}");

        // Load main menu
        SceneManager.LoadScene(mainMenuScene);
    }

    [System.Serializable]
    private class AuthResponse
    {
        public string token;
        public string role;
        public string username;
    }
}