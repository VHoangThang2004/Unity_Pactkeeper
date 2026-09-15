using UnityEngine;
using Unity.Netcode;
using System.Collections;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

public class LobbyManager : MonoBehaviour
{
    [SerializeField] int requiredPlayers = 2;

    [SerializeField] private SceneConfig sceneConfig;
    [SerializeField] float checkInterval = 1f;
    [SerializeField] float lobbyTimeout = 120f;

    private ServerBackendClient backendClient;
    private bool matchStarted = false;
    private float elapsed = 0f;
    public Dictionary<ulong, string> clientPlayerMap = new Dictionary<ulong, string>();

    private string ExtractPlayerIdFromToken(string token)
    {
        try
        {
            string[] parts = token.Split('.');
            if (parts.Length != 3) return string.Empty;

            string payload = parts[1];
            // Add padding if needed
            int mod = payload.Length % 4;
            if (mod > 0) payload += new string('=', 4 - mod);

            string json = System.Text.Encoding.UTF8.GetString(System.Convert.FromBase64String(payload));
            // Parse PlayerId claim
            int idx = json.IndexOf("\"PlayerId\":\"");
            if (idx < 0) return string.Empty;
            int start = idx + 12;
            int end = json.IndexOf("\"", start);
            return json.Substring(start, end - start);
        }
        catch
        {
            return string.Empty;
        }
    }

    void Start()
    {
        if (!IsServer())
        {
            enabled = false;
            return;
        }

        backendClient = FindAnyObjectByType<ServerBackendClient>();
        if (backendClient == null)
            Debug.LogError("[Lobby] ServerBackendClient not found!");

        NetworkManager.Singleton.ConnectionApprovalCallback = ApprovalCheck;

        Debug.Log("[Lobby] Server lobby running");
        StartCoroutine(LobbyStartSequence());
    }

    IEnumerator LobbyStartSequence()
    {
        yield return new WaitUntil(() => backendClient.IsReady);
        Debug.Log($"[Lobby] Backend ready — MatchId={backendClient.MatchId} Mode={backendClient.Mode}");
        InvokeRepeating(nameof(CheckStartGame), 1f, checkInterval);
    }

    void CheckStartGame()
    {
        if (matchStarted) return;

        elapsed += checkInterval;

        int playerCount = NetworkManager.Singleton.ConnectedClientsIds.Count;
        Debug.Log($"[Lobby] Players: {playerCount}/{requiredPlayers}");

        if (playerCount >= requiredPlayers)
        {
            matchStarted = true;
            Debug.Log("[Lobby] Starting game");
            // Store map in a persistent place for ServerMatchSession to read
            MatchIdentityRegistry.clientPlayerMap = clientPlayerMap;
            string targetScene = backendClient.Mode == "story" ? sceneConfig.storyScene : sceneConfig.pvpScene;
            NetworkManager.Singleton.SceneManager.LoadScene(targetScene, LoadSceneMode.Single);
            return;
        }

        if (elapsed >= lobbyTimeout)
        {
            Debug.LogError("[Lobby] Timeout — not enough players connected.");
            matchStarted = true;
            StartCoroutine(TimeoutSequence());
        }
    }

    IEnumerator TimeoutSequence()
{
    NetworkManager.Singleton.Shutdown();
    yield return StartCoroutine(backendClient.ReportCancelled());
}
    bool IsServer()
    {
        return NetworkManager.Singleton != null &&
               NetworkManager.Singleton.IsServer;
    }

    private void ApprovalCheck(
    NetworkManager.ConnectionApprovalRequest request,
    NetworkManager.ConnectionApprovalResponse response)
    {
        string token = System.Text.Encoding.UTF8.GetString(request.Payload);
        string playerId = ExtractPlayerIdFromToken(token);

        Debug.Log($"[Lobby] Connection request — clientId={request.ClientNetworkId} playerId={playerId}");

        if (string.IsNullOrEmpty(playerId))
        {
            Debug.LogWarning("[Lobby] Rejected — invalid token.");
            response.Approved = false;
            return;
        }

        // Check playerId is in this match
        string matchPlayer1 = backendClient.LoadoutResponse?.player1Id ?? string.Empty;
        string matchPlayer2 = backendClient.LoadoutResponse?.player2Id ?? string.Empty;

        if (playerId != matchPlayer1 && playerId != matchPlayer2)
        {
            Debug.LogWarning($"[Lobby] Rejected — playerId {playerId} not in this match.");
            response.Approved = false;
            return;
        }

        // Store mapping
        clientPlayerMap[request.ClientNetworkId] = playerId;
        Debug.Log($"[Lobby] Approved — clientId={request.ClientNetworkId} → playerId={playerId}");

        response.Approved = true;
        response.CreatePlayerObject = false;
    }
}