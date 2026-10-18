using System;
using System.Collections;
using System.Collections.Concurrent;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

public class LobbyHubClient : MonoBehaviour
{
    public static LobbyHubClient Instance { get; private set; }

    [SerializeField] private BackendConfig config;

    public bool IsConnected => webSocket != null && webSocket.State == WebSocketState.Open;

    // Events dispatched on Unity Main Thread
    public event Action<MatchInviteData> OnMatchInviteReceived;
    public event Action<MatchReadyData> OnMatchReady;
    public event Action<FriendPresenceData> OnFriendPresenceChanged;
    public event Action<string> OnMatchInviteDeclined;
    public event Action<string> OnMatchInviteCancelled;
    public event Action<string> OnFriendRequestReceived;

    private ClientWebSocket webSocket;
    private CancellationTokenSource cts;
    private readonly ConcurrentQueue<Action> mainThreadQueue = new();

    private const char RecordSeparator = '\x1e';

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    void Update()
    {
        while (mainThreadQueue.TryDequeue(out var action))
        {
            try
            {
                action?.Invoke();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[LobbyHubClient] Error invoking main thread action: {ex}");
            }
        }
    }

    void OnDestroy()
    {
        Disconnect();
        if (Instance == this) Instance = null;
    }

    public void Connect()
    {
        if (IsConnected) return;
        if (!PlayerSession.IsLoggedIn)
        {
            Debug.LogWarning("[LobbyHubClient] Cannot connect — not logged in.");
            return;
        }

        cts = new CancellationTokenSource();
        Task.Run(() => ConnectAsync(cts.Token));
    }

    public void Disconnect()
    {
        cts?.Cancel();
        cts?.Dispose();
        cts = null;

        if (webSocket != null)
        {
            try
            {
                if (webSocket.State == WebSocketState.Open || webSocket.State == WebSocketState.Connecting)
                {
                    webSocket.Abort();
                }
                webSocket.Dispose();
            }
            catch { }
            webSocket = null;
        }
        Debug.Log("[LobbyHubClient] Disconnected from lobby hub.");
    }

    private async Task ConnectAsync(CancellationToken token)
    {
        try
        {
            string baseUrl = config.backendUrl.TrimEnd('/');
            string wsScheme = baseUrl.StartsWith("https", StringComparison.OrdinalIgnoreCase) ? "wss" : "ws";
            string rawHost = baseUrl.Substring(baseUrl.IndexOf("://", StringComparison.Ordinal) + 3);
            string wsUrl = $"{wsScheme}://{rawHost}/hubs/lobby?access_token={Uri.EscapeDataString(PlayerSession.Token)}";

            webSocket = new ClientWebSocket();
            webSocket.Options.SetRequestHeader("ngrok-skip-browser-warning", "true");

            Debug.Log($"[LobbyHubClient] Connecting to {wsUrl}...");
            await webSocket.ConnectAsync(new Uri(wsUrl), token);

            // SignalR Handshake
            string handshake = $"{{\"protocol\":\"json\",\"version\":1}}{RecordSeparator}";
            byte[] handshakeBytes = Encoding.UTF8.GetBytes(handshake);
            await webSocket.SendAsync(new ArraySegment<byte>(handshakeBytes), WebSocketMessageType.Text, true, token);

            Debug.Log("[LobbyHubClient] Connected and sent handshake.");

            _ = Task.Run(() => PingLoopAsync(token), token);
            await ReceiveLoopAsync(token);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            Debug.LogError($"[LobbyHubClient] Connection error: {ex.Message}");
        }
    }

    private async Task PingLoopAsync(CancellationToken token)
    {
        byte[] pingBytes = Encoding.UTF8.GetBytes($"{{\"type\":6}}{RecordSeparator}");
        while (!token.IsCancellationRequested && IsConnected)
        {
            try
            {
                await Task.Delay(15000, token);
                if (IsConnected)
                    await webSocket.SendAsync(new ArraySegment<byte>(pingBytes), WebSocketMessageType.Text, true, token);
            }
            catch { break; }
        }
    }

    private async Task ReceiveLoopAsync(CancellationToken token)
    {
        var buffer = new byte[8192];
        var ms = new MemoryStream();

        while (!token.IsCancellationRequested && IsConnected)
        {
            var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), token);
            if (result.MessageType == WebSocketMessageType.Close)
            {
                Debug.Log("[LobbyHubClient] Server closed connection.");
                break;
            }

            ms.Write(buffer, 0, result.Count);

            if (result.EndOfMessage)
            {
                string rawText = Encoding.UTF8.GetString(ms.ToArray());
                ms.SetLength(0);

                string[] messages = rawText.Split(RecordSeparator);
                foreach (var msg in messages)
                {
                    if (string.IsNullOrWhiteSpace(msg)) continue;
                    ProcessSignalRMessage(msg.Trim());
                }
            }
        }
    }

    private void ProcessSignalRMessage(string json)
    {
        // Ping response
        if (json.Contains("\"type\":6") || json == "{}") return;

        try
        {
            var hubMsg = JsonUtility.FromJson<SignalRInvocationMessage>(json);
            if (hubMsg == null || string.IsNullOrEmpty(hubMsg.target)) return;

            switch (hubMsg.target)
            {
                case "ReceiveMatchInvite":
                    int argsIdx = json.IndexOf("\"arguments\":[", StringComparison.Ordinal);
                    if (argsIdx >= 0)
                    {
                        string argObj = ExtractFirstJsonObject(json, argsIdx + 12);
                        if (!string.IsNullOrEmpty(argObj))
                        {
                            var invite = JsonUtility.FromJson<MatchInviteData>(argObj);
                            mainThreadQueue.Enqueue(() => OnMatchInviteReceived?.Invoke(invite));
                        }
                    }
                    break;

                case "MatchReady":
                    int readyIdx = json.IndexOf("\"arguments\":[", StringComparison.Ordinal);
                    if (readyIdx >= 0)
                    {
                        string argObj = ExtractFirstJsonObject(json, readyIdx + 12);
                        if (!string.IsNullOrEmpty(argObj))
                        {
                            var matchReady = JsonUtility.FromJson<MatchReadyData>(argObj);
                            mainThreadQueue.Enqueue(() => OnMatchReady?.Invoke(matchReady));
                        }
                    }
                    break;

                case "OnFriendPresenceChanged":
                    int presIdx = json.IndexOf("\"arguments\":[", StringComparison.Ordinal);
                    if (presIdx >= 0)
                    {
                        string argObj = ExtractFirstJsonObject(json, presIdx + 12);
                        if (!string.IsNullOrEmpty(argObj))
                        {
                            var pres = JsonUtility.FromJson<FriendPresenceData>(argObj);
                            mainThreadQueue.Enqueue(() => OnFriendPresenceChanged?.Invoke(pres));
                        }
                    }
                    break;

                case "MatchInviteDeclined":
                    mainThreadQueue.Enqueue(() => OnMatchInviteDeclined?.Invoke("declined"));
                    break;

                case "MatchInviteCancelled":
                    mainThreadQueue.Enqueue(() => OnMatchInviteCancelled?.Invoke("cancelled"));
                    break;

                case "ReceiveFriendRequest":
                    mainThreadQueue.Enqueue(() => OnFriendRequestReceived?.Invoke("new_request"));
                    break;
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[LobbyHubClient] Failed to parse SignalR frame: {ex.Message}\nRaw: {json}");
        }
    }

    private string ExtractFirstJsonObject(string text, int startIndex)
    {
        int start = text.IndexOf('{', startIndex);
        if (start < 0) return string.Empty;

        int depth = 0;
        for (int i = start; i < text.Length; i++)
        {
            if (text[i] == '{') depth++;
            else if (text[i] == '}')
            {
                depth--;
                if (depth == 0) return text.Substring(start, i - start + 1);
            }
        }
        return string.Empty;
    }

    // -------------------------------------------------------
    // REST API Helpers
    // -------------------------------------------------------

    public IEnumerator SendMatchInvite(string targetPlayerId, Action<string> onSuccess, Action<string> onError)
    {
        string json = $"{{\"targetPlayerId\":\"{targetPlayerId}\"}}";
        using var req = new UnityWebRequest($"{config.backendUrl}/api/friends/match/invite", "POST");
        req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
        req.downloadHandler = new DownloadHandlerBuffer();
        config.SetHeaders(req, PlayerSession.Token);

        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
            onError?.Invoke(req.error);
        else
        {
            var res = JsonUtility.FromJson<InviteResponse>(req.downloadHandler.text);
            onSuccess?.Invoke(res.inviteId);
        }
    }

    public IEnumerator AcceptMatchInvite(string inviteId, Action<MatchReadyData> onSuccess, Action<string> onError)
    {
        using var req = new UnityWebRequest($"{config.backendUrl}/api/friends/match/invite/{inviteId}/accept", "POST");
        req.uploadHandler = new UploadHandlerRaw(new byte[0]);
        req.downloadHandler = new DownloadHandlerBuffer();
        config.SetHeaders(req, PlayerSession.Token);

        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
            onError?.Invoke(req.error);
        else
        {
            var joinInfo = JsonUtility.FromJson<MatchReadyData>(req.downloadHandler.text);
            onSuccess?.Invoke(joinInfo);
        }
    }

    public IEnumerator DeclineMatchInvite(string inviteId, Action onDone = null)
    {
        using var req = new UnityWebRequest($"{config.backendUrl}/api/friends/match/invite/{inviteId}/decline", "POST");
        req.uploadHandler = new UploadHandlerRaw(new byte[0]);
        req.downloadHandler = new DownloadHandlerBuffer();
        config.SetHeaders(req, PlayerSession.Token);

        yield return req.SendWebRequest();
        onDone?.Invoke();
    }

    public IEnumerator CancelMatchInvite(string inviteId, Action onDone = null)
    {
        using var req = new UnityWebRequest($"{config.backendUrl}/api/friends/match/invite/{inviteId}/cancel", "POST");
        req.uploadHandler = new UploadHandlerRaw(new byte[0]);
        req.downloadHandler = new DownloadHandlerBuffer();
        config.SetHeaders(req, PlayerSession.Token);

        yield return req.SendWebRequest();
        onDone?.Invoke();
    }

    // -------------------------------------------------------
    // DTOs
    // -------------------------------------------------------

    [Serializable]
    private class SignalRInvocationMessage
    {
        public int type;
        public string target;
    }

    [Serializable]
    private class InviteResponse
    {
        public string inviteId;
    }

    [Serializable]
    public class MatchInviteData
    {
        public string inviteId;
        public string senderPlayerId;
        public string senderUsername;
        public int expiresInSeconds;
    }

    [Serializable]
    public class MatchReadyData
    {
        public string matchId;
        public string serverIp;
        public int serverPort;
        public string mode;
        public string player1Name;
        public string player2Name;
    }

    [Serializable]
    public class FriendPresenceData
    {
        public string playerId;
        public bool isOnline;
        public string status; // "idle", "in_match", "offline"
    }
}
