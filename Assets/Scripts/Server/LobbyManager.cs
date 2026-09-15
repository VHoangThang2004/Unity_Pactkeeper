using UnityEngine;
using Unity.Netcode;
using System.Collections;
using UnityEngine.SceneManagement;

public class LobbyManager : MonoBehaviour
{
    [SerializeField] int requiredPlayers = 2;
    [SerializeField] string pvpScene = "5_Match";
    [SerializeField] string storyScene = "5_Story";
    [SerializeField] float checkInterval = 1f;
    [SerializeField] float lobbyTimeout = 120f;

    private ServerBackendClient backendClient;
    private bool matchStarted = false;
    private float elapsed = 0f;

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
            string targetScene = backendClient.Mode == "story" ? storyScene : pvpScene;
            NetworkManager.Singleton.SceneManager.LoadScene(targetScene, LoadSceneMode.Single);
            return;
        }

        if (elapsed >= lobbyTimeout)
        {
            Debug.LogError("[Lobby] Timeout — no players connected.");
            matchStarted = true;
            StartCoroutine(TimeoutSequence());
        }
    }

    IEnumerator TimeoutSequence()
    {
        yield return StartCoroutine(backendClient.ReportCancelled());
        NetworkManager.Singleton.Shutdown();
    }

    bool IsServer()
    {
        return NetworkManager.Singleton != null &&
               NetworkManager.Singleton.IsServer;
    }
}