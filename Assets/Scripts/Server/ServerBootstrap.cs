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
    [SerializeField] private string lobbyScene = "4_Lobby";

    [Header("Client Config")]
    [SerializeField] private string loginScene = "1_Login";
    [SerializeField] private string devConnectScene = "2_DevConnect";
    [SerializeField] private bool devMode = true;

    IEnumerator Start()
    {
        if (!IsServerMode())
        {
            StartClientFlow();
            yield break;
        }

        Debug.Log("[Bootstrap] SERVER MODE");

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

        transport.SetConnectionData("0.0.0.0", port);

        bool success = NetworkManager.Singleton.StartServer();

        if (!success)
        {
            Debug.LogError("❌ Failed to start server!");
            return;
        }

        Debug.Log($"[Bootstrap] Server started on port {port}");

        NetworkManager.Singleton.SceneManager.LoadScene(
            lobbyScene,
            LoadSceneMode.Single
        );
    }

    void StartClientFlow()
    {
        Debug.Log($"[Bootstrap] CLIENT MODE (devMode={devMode})");
        SceneManager.LoadScene(devMode ? devConnectScene : loginScene);
    }

    bool IsServerMode()
    {
#if UNITY_EDITOR
        return IsEditorServerInstance();
#else
        return System.Environment.CommandLine.Contains("-server");
#endif
    }

    [Header("Editor Config")]
    [SerializeField] private bool editorIsServer = false;

    bool IsEditorServerInstance()
    {
#if UNITY_EDITOR
        if (!editorIsServer) return false;
        return CurrentPlayer.IsMainEditor;
#else
    return false;
#endif
    }
}