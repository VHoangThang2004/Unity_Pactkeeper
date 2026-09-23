using UnityEngine;
using Unity.Netcode;
using System.Collections;

/// <summary>
/// Story-mode lobby. Separate from LobbyManager (PvP) — waits for exactly
/// 1 real player to connect (the AI does not connect via NGO), then routes
/// to the correct C{chapterId}_S{sceneId}_Server scene.
///
/// Lives on its own scene (e.g. C0_S1_Lobby) — never shared with PvP lobby.
/// </summary>
public class StoryLobbyManager : MonoBehaviour
{
    [SerializeField] private int requiredPlayers = 1; // story mode is always 1 real player
    [SerializeField] private SceneConfig sceneConfig;
    [SerializeField] private float checkInterval = 1f;
    [SerializeField] private float lobbyTimeout = 120f;

    private ServerBackendClient backendClient;
    private bool matchStarted = false;
    private float elapsed = 0f;

    void Start()
    {
        if (!IsServer()) { enabled = false; return; }

        backendClient = FindAnyObjectByType<ServerBackendClient>();
        if (backendClient == null)
            Debug.LogError("[StoryLobby] ServerBackendClient not found!");

        Debug.Log("[StoryLobby] Server story lobby running");
        StartCoroutine(LobbyStartSequence());
    }

    IEnumerator LobbyStartSequence()
    {
        yield return new WaitUntil(() => backendClient.IsReady);
        Debug.Log($"[StoryLobby] Backend ready — MatchId={backendClient.MatchId} " +
                  $"Chapter={backendClient.ChapterId} Scene={backendClient.SceneId}");

        if (backendClient.Mode != "story")
        {
            Debug.LogError($"[StoryLobby] Mode mismatch — expected 'story', got '{backendClient.Mode}'. " +
                           "This scene should only be loaded for story matches.");
        }

        InvokeRepeating(nameof(CheckStartGame), 1f, checkInterval);
    }

    void CheckStartGame()
    {
        if (matchStarted) return;
        elapsed += checkInterval;

        int playerCount = 0;
        foreach (var id in NetworkManager.Singleton.ConnectedClientsIds)
            if (id != NetworkManager.Singleton.LocalClientId) playerCount++;

        Debug.Log($"[StoryLobby] Players: {playerCount}/{requiredPlayers}");

        if (playerCount >= requiredPlayers)
        {
            matchStarted = true;
            Debug.Log("[StoryLobby] Starting story battle");

            // Convention: C{chapterId}_S{sceneId}_Server
            string targetScene = $"C{backendClient.ChapterId}_S{backendClient.SceneId}_Server";
            UnityEngine.SceneManagement.SceneManager.LoadScene(targetScene);
            return;
        }

        if (elapsed >= lobbyTimeout)
        {
            Debug.LogError($"[StoryLobby] Timeout — only {playerCount}/{requiredPlayers} connected.");
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