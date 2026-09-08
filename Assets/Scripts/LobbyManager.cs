using UnityEngine;
using Unity.Netcode;
using UnityEngine.SceneManagement;

public class LobbyManager : MonoBehaviour
{
    [SerializeField] int requiredPlayers = 2;
    [SerializeField] string gameScene = "3_Game";
    [SerializeField] float checkInterval = 1f;

    bool matchStarted = false;

    void Start()
    {
        // Only run on server
        if (!IsServer())
        {
            enabled = false;
            return;
        }

        Debug.Log("[Lobby] Server lobby running");

        InvokeRepeating(nameof(CheckStartGame), 1f, checkInterval);
    }

    void CheckStartGame()
    {
        if (matchStarted) return;

        int playerCount = NetworkManager.Singleton.ConnectedClientsIds.Count;

        Debug.Log($"[Lobby] Players: {playerCount}/{requiredPlayers}");

        if (playerCount >= requiredPlayers)
        {
            matchStarted = true;

            Debug.Log("[Lobby] Starting game");

            NetworkManager.Singleton.SceneManager.LoadScene(
                gameScene,
                LoadSceneMode.Single
            );
        }
    }

    bool IsServer()
    {
        return NetworkManager.Singleton != null &&
               NetworkManager.Singleton.IsServer;
    }
}