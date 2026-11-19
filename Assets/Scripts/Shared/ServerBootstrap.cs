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
            // Never quit: report the match cancelled and let the backend stop this process
            Debug.LogError($"[Bootstrap] Failed to start server on port {port} — cancelling the match.");
            IMatchEnder backendClient = null;
            foreach (var behaviour in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
                if (behaviour is IMatchEnder ender) { backendClient = ender; break; }
            if (backendClient != null) backendClient.EndMatch($"failed to start server on port {port}");
            else Debug.LogError("[Bootstrap] No ServerBackendClient in scene — can't report; the backend's 60-min limit will clean up.");
            return;
        }

        Debug.Log($"[Bootstrap] Server started on port {port}");

        // Route to the correct lobby based on -mode CLI arg.
        // Mode is known immediately (CLI arg) — no need to wait for backend fetch.
        string mode = GetModeFromArgs();
        string targetLobby = mode == "story"
            ? sceneConfig.singleLobbyScene
            : sceneConfig.lobbyScene;

        Debug.Log($"[Bootstrap] Mode={mode} — loading lobby '{targetLobby}'");

        SceneManager.LoadScene(targetLobby, LoadSceneMode.Single);
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

    string GetModeFromArgs()
    {
        string[] args = System.Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
            if (args[i] == "-mode")
                return args[i + 1];

        // Fallback: If backend failed to pass "-mode story", but passed "-chapterId", it must be a story match.
        foreach (var arg in args)
        {
            if (arg == "-chapterId")
                return "story";
        }

        Debug.LogWarning("[Bootstrap] No -mode arg found — defaulting to 'pvp'.");
        return "pvp";
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