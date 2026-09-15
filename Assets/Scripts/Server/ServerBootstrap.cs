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
    [SerializeField] private SceneConfig sceneConfig;
    [Header("Client Config")]
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

        ushort port = GetPortFromArgs();
        transport.SetConnectionData("0.0.0.0", port);

        bool success = NetworkManager.Singleton.StartServer();

        if (!success)
        {
            Debug.LogError("❌ Failed to start server!");
            return;
        }

        Debug.Log($"[Bootstrap] Server started on port {port}");

        NetworkManager.Singleton.SceneManager.LoadScene(
            sceneConfig.lobbyScene,
            LoadSceneMode.Single
        );
    }

    void StartClientFlow()
    {
        Debug.Log($"[Bootstrap] CLIENT MODE (devMode={devMode})");
        SceneManager.LoadScene(devMode ? sceneConfig.devConnectScene : sceneConfig.loginScene);
    }

    ushort GetPortFromArgs()
    {
        string[] args = System.Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
            if (args[i] == "-port" && ushort.TryParse(args[i + 1], out ushort p))
                return p;
        Debug.LogWarning("[Bootstrap] No -port arg found — defaulting to 7777.");
        return 7777;
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