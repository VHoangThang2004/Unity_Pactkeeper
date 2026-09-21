using UnityEngine;
using Unity.Netcode;
using System.Collections;

public class LobbyManager : MonoBehaviour
{
    [SerializeField] int requiredPlayers = 2;
    [SerializeField] private SceneConfig sceneConfig;
    [SerializeField] float checkInterval = 1f;
    [SerializeField] float lobbyTimeout = 120f;

    private ServerBackendClient backendClient;
    private bool matchStarted = false;
    private float elapsed = 0f;

    void Start()
    {
        if (!IsServer()) { enabled = false; return; }

        backendClient = FindAnyObjectByType<ServerBackendClient>();
        if (backendClient == null)
            Debug.LogError("[Lobby] ServerBackendClient not found!");

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

        int playerCount = 0;
        foreach (var id in NetworkManager.Singleton.ConnectedClientsIds)
            if (id != NetworkManager.Singleton.LocalClientId) playerCount++;
        Debug.Log($"[Lobby] Players: {playerCount}/{requiredPlayers}");

        if (playerCount >= requiredPlayers)
        {
            matchStarted = true;
            Debug.Log("[Lobby] Starting game");
            string targetScene = backendClient.Mode == "story" ? sceneConfig.storyScene : sceneConfig.serverMatch;
            UnityEngine.SceneManagement.SceneManager.LoadScene(targetScene);
            return;
        }

        if (elapsed >= lobbyTimeout)
        {
            Debug.LogError($"[Lobby] Timeout — only {playerCount}/{requiredPlayers} connected.");
            matchStarted = true;
            StartCoroutine(TimeoutSequence());
        }
    }

    IEnumerator TimeoutSequence()
    {
        NetworkManager.Singleton.Shutdown();
        yield return StartCoroutine(backendClient.ReportCancelled());
    }

    bool IsServer() =>
        NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer;
}