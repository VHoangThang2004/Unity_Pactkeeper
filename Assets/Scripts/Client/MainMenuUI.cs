using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using TMPro;

public class MainMenuUI : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] TMP_InputField ipInput;
    [SerializeField] TMP_Text statusText;

    [Header("Config")]
    [SerializeField] ushort port = 7777;

    UnityTransport transport;

    void Start()
    {
        if (NetworkManager.Singleton == null)
        {
            Debug.LogError("❌ NetworkManager missing!");
            return;
        }

        transport = NetworkManager.Singleton.GetComponent<UnityTransport>();

        if (transport == null)
        {
            Debug.LogError("❌ UnityTransport missing!");
            return;
        }

        // Default IP
        if (ipInput != null)
            ipInput.text = "127.0.0.1";

        statusText.text = "";

        // Listen for connection events
        NetworkManager.Singleton.OnClientConnectedCallback += OnConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback += OnDisconnected;
    }

    void OnDestroy()
    {
        if (NetworkManager.Singleton == null) return;

        NetworkManager.Singleton.OnClientConnectedCallback -= OnConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback -= OnDisconnected;
    }

    // 🔥 Called by JOIN button
    public void OnClickJoin()
    {
        string ip = ipInput.text;

        if (string.IsNullOrEmpty(ip))
        {
            statusText.text = "IP is empty!";
            return;
        }

        transport.SetConnectionData(ip, port);

        bool started = NetworkManager.Singleton.StartClient();

        if (!started)
        {
            statusText.text = "Failed to start client!";
            return;
        }

        statusText.text = "Connecting...";
    }

    void OnConnected(ulong clientId)
    {
        if (clientId != NetworkManager.Singleton.LocalClientId)
            return;

        Debug.Log("✅ Connected to server");

        statusText.text = "Connected!";

        // Server will move us to Lobby scene automatically
    }

    void OnDisconnected(ulong clientId)
    {
        if (clientId != NetworkManager.Singleton.LocalClientId)
            return;

        Debug.Log("❌ Disconnected");

        statusText.text = "Disconnected!";
    }
}