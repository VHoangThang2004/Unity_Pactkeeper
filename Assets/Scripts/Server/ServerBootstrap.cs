#if UNITY_EDITOR
using Unity.Multiplayer.PlayMode;
#endif
using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine.SceneManagement;
using System.Collections;



public class ServerBootstrap : MonoBehaviour
{
    [Header("Server Config")]
    [SerializeField] private ushort port = 7777;
    [SerializeField] private string lobbyScene = "2_Lobby";

    [Header("Client Config")]
    [SerializeField] private string clientStartScene = "1_Menu";

    IEnumerator Start()
    {
        // Decide mode based on command line
        if (!IsServerMode())
        {
            StartClientFlow();
            yield break;
        }

        Debug.Log("[Bootstrap] SERVER MODE");

        // 🔥 Critical: wait until NetworkManager is fully initialized
        yield return new WaitUntil(() => NetworkManager.Singleton != null);
        yield return null;

        StartDedicatedServer();
    }

    void StartDedicatedServer()
    {
        if (NetworkManager.Singleton == null)
        {
            Debug.LogError("❌ NetworkManager not found!");
            return;
        }

        var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        if (transport == null)
        {
            Debug.LogError("❌ UnityTransport missing!");
            return;
        }

        // Listen on all interfaces
        transport.SetConnectionData("0.0.0.0", port);

        bool success = NetworkManager.Singleton.StartServer();

        if (!success)
        {
            Debug.LogError("❌ Failed to start server!");
            return;
        }

        Debug.Log($"[Bootstrap] Server started on port {port}");

        // Load lobby for all clients
        NetworkManager.Singleton.SceneManager.LoadScene(
            lobbyScene,
            LoadSceneMode.Single
        );
    }

    void StartClientFlow()
    {
        Debug.Log("[Bootstrap] CLIENT MODE");

        // Just go to menu, no networking yet
        SceneManager.LoadScene(clientStartScene);
    }

    bool IsServerMode()
    {
#if UNITY_EDITOR
        return IsEditorServerInstance();
#else
    return System.Environment.CommandLine.Contains("-server");
#endif
    }


    bool IsEditorServerInstance()
    {
#if UNITY_EDITOR
        return CurrentPlayer.IsMainEditor; // Main editor instance is server, others are clients
#else
    return false;
#endif
    }
}