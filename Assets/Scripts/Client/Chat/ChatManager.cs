using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Scene manager cho 14_Chat.
///
/// Player role  : 1 conversation duy nhất với "Keeper of Records" (admin).
///   - LEFT  : 1 ChatListItem entry "Keeper of Records" + preview tin nhắn gần nhất
///   - RIGHT : toàn bộ lịch sử tin nhắn, InputField + Send button
///
/// Admin role   : danh sách tất cả player đã từng chat.
///   - LEFT  : danh sách ChatListItem — mỗi player 1 entry
///   - RIGHT : history của player đang chọn, InputField + Send
///
/// Realtime     : SignalR via WebSocket tương tự LobbyHubClient.
///   - Kết nối /hubs/support?access_token=...
///   - Event: ReceiveMessage(playerId, SupportMessageDto)
///
/// API:
///   Player:
///     GET  /api/support/chat                     — lịch sử của bản thân
///     POST /api/support/chat                     — gửi tin cho admin
///   Admin:
///     GET  /api/support/admin/players            — danh sách players đang chat
///     GET  /api/support/admin/chat/{playerId}    — history của player
///     POST /api/support/admin/chat/{playerId}    — reply player
/// </summary>
public class ChatManager : MonoBehaviour
{
    // ── DTOs ─────────────────────────────────────────────────────────────────

    [Serializable]
    public class SupportMessageDto
    {
        public string id;
        public string sender;       // "user" | "admin"
        public string senderName;
        public string text;
        public string attachmentUrl;
        public string createdAt;
    }

    [Serializable]
    public class ActiveChatPlayerDto
    {
        public string playerId;
        public string playerName;
        public string latestMessageText;
        public string latestMessageTime;
    }

    [Serializable] private class MessageListWrapper  { public SupportMessageDto[]     items; }
    [Serializable] private class PlayerListWrapper   { public ActiveChatPlayerDto[]   items; }
    [Serializable] private class SendMessageRequest  { public string text; public string attachmentUrl; }

    // ── Inspector ─────────────────────────────────────────────────────────────

    [Header("Config")]
    [SerializeField] private BackendConfig config;
    [SerializeField] private SceneConfig   sceneConfig;

    [Header("Left Panel — Conversation List")]
    [SerializeField] private Transform  listContainer;      // VerticalLayoutGroup content
    [SerializeField] private GameObject chatListItemPrefab;
    [SerializeField] private GameObject loadingIndicator;

    [Header("Right Panel — Message View")]
    [SerializeField] private Transform  messageContainer;   // VerticalLayoutGroup content (inside ScrollRect)
    [SerializeField] private ScrollRect messageScrollRect;
    [SerializeField] private GameObject chatMessageItemPrefab;
    [SerializeField] private TMP_InputField inputField;
    [SerializeField] private Button     sendButton;
    [SerializeField] private Button     backButton;
    [SerializeField] private GameObject emptyState;         // "Chọn một cuộc hội thoại"

    // ── Runtime ───────────────────────────────────────────────────────────────

    private bool   _isAdmin;
    private string _selectedPlayerId; // null = chưa chọn (admin) | chính mình (player)

    private ChatListItem                  _selectedListItem;
    private readonly List<GameObject>     _messageObjects = new();

    // SignalR
    private ClientWebSocket               _ws;
    private CancellationTokenSource      _cts;
    private readonly ConcurrentQueue<Action> _mainThread = new();
    private const char RS = '\x1e'; // Record Separator

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    void Awake()
    {
        if (sendButton != null) sendButton.onClick.AddListener(OnSendClicked);
        if (backButton != null) backButton.onClick.AddListener(OnBackClicked);

        // Allow Enter key to send
        if (inputField != null)
            inputField.onSubmit.AddListener(_ => OnSendClicked());

        if (emptyState != null) emptyState.SetActive(true);
    }

    void Start()
    {
        if (!PlayerSession.IsLoggedIn)
        {
            SceneManager.LoadScene(sceneConfig.mainMenuScene);
            return;
        }

        _isAdmin = PlayerSession.Role?.Equals("Admin", StringComparison.OrdinalIgnoreCase) ?? false;

        ConnectSignalR();
        StartCoroutine(LoadLeftPanel());
    }

    void Update()
    {
        while (_mainThread.TryDequeue(out var action))
        {
            try { action?.Invoke(); }
            catch (Exception ex) { Debug.LogError($"[Chat] MainThread action error: {ex}"); }
        }
    }

    void OnDestroy()
    {
        if (sendButton != null) sendButton.onClick.RemoveListener(OnSendClicked);
        if (backButton != null) backButton.onClick.RemoveListener(OnBackClicked);
        DisconnectSignalR();
    }

    // ── Left Panel ────────────────────────────────────────────────────────────

    IEnumerator LoadLeftPanel()
    {
        if (loadingIndicator != null) loadingIndicator.SetActive(true);

        if (_isAdmin)
            yield return StartCoroutine(LoadAdminPlayerList());
        else
            yield return StartCoroutine(LoadPlayerSelfEntry());

        if (loadingIndicator != null) loadingIndicator.SetActive(false);
    }

    /// <summary>Player — 1 entry duy nhất "Keeper of Records".</summary>
    IEnumerator LoadPlayerSelfEntry()
    {
        SupportMessageDto[] history = null;
        yield return StartCoroutine(FetchPlayerHistory(msgs => history = msgs));

        ClearList();

        string preview = history != null && history.Length > 0
            ? history[history.Length - 1].text
            : "No messages yet";

        var listItem = SpawnListItem();
        if (listItem == null)
        {
            Debug.LogError("[Chat] SpawnListItem returned null — kiểm tra chatListItemPrefab và listContainer đã được gán trong Inspector chưa.");
        }
        else
        {
            listItem.SetupAsSupport(preview, () =>
            {
                _selectedPlayerId = PlayerSession.PlayerId;
                StartCoroutine(OpenConversation(null)); // fetch fresh
            });
            SelectListItem(listItem);
        }

        // Auto-open conversation ngay khi vào scene
        _selectedPlayerId = PlayerSession.PlayerId;
        StartCoroutine(OpenConversation(history));
    }

    /// <summary>Admin — danh sách tất cả players đã chat.</summary>
    IEnumerator LoadAdminPlayerList()
    {
        ActiveChatPlayerDto[] players = null;
        yield return StartCoroutine(FetchActivePlayers(p => players = p));

        ClearList();

        if (players == null || players.Length == 0)
        {
            if (emptyState != null) emptyState.SetActive(true);
            yield break;
        }

        foreach (var player in players)
        {
            var p = player; // capture
            var item = SpawnListItem();
            item.SetupAsPlayer(p, () =>
            {
                SelectListItem(item);
                _selectedPlayerId = p.playerId;
                StartCoroutine(LoadAdminConversation(p.playerId));
            });
        }
    }

    // ── Right Panel ───────────────────────────────────────────────────────────

    IEnumerator OpenConversation(SupportMessageDto[] cachedHistory)
    {
        if (emptyState != null) emptyState.SetActive(false);

        SupportMessageDto[] history = cachedHistory;

        if (history == null)
            yield return StartCoroutine(FetchPlayerHistory(msgs => history = msgs));

        RenderMessages(history ?? Array.Empty<SupportMessageDto>());
    }

    IEnumerator LoadAdminConversation(string playerId)
    {
        if (emptyState != null) emptyState.SetActive(false);
        ClearMessages();

        SupportMessageDto[] history = null;
        yield return StartCoroutine(FetchAdminHistory(playerId, msgs => history = msgs));

        RenderMessages(history ?? Array.Empty<SupportMessageDto>());
    }

    void RenderMessages(SupportMessageDto[] messages)
    {
        ClearMessages();

        foreach (var msg in messages)
            SpawnMessageBubble(msg);

        ScrollToBottom();
    }

    void SpawnMessageBubble(SupportMessageDto msg)
    {
        if (chatMessageItemPrefab == null || messageContainer == null) return;

        var obj  = Instantiate(chatMessageItemPrefab, messageContainer);
        var item = obj.GetComponent<ChatMessageItem>();
        item?.Setup(msg);
        _messageObjects.Add(obj);
    }

    void ScrollToBottom()
    {
        // Delay 1 frame để layout rebuild xong trước khi scroll
        StartCoroutine(ScrollToBottomNextFrame());
    }

    IEnumerator ScrollToBottomNextFrame()
    {
        yield return null; // wait 1 frame
        if (messageScrollRect != null)
            messageScrollRect.verticalNormalizedPosition = 0f;
    }

    // ── Send Message ──────────────────────────────────────────────────────────

    void OnSendClicked()
    {
        if (inputField == null) return;

        string text = inputField.text.Trim();
        if (string.IsNullOrEmpty(text)) return;
        if (string.IsNullOrEmpty(_selectedPlayerId)) return;

        inputField.text = string.Empty;
        inputField.ActivateInputField(); // giữ focus

        StartCoroutine(SendMessage(text));
    }

    IEnumerator SendMessage(string text)
    {
        if (sendButton != null) sendButton.interactable = false;

        string url  = _isAdmin
            ? $"{config.backendUrl}/api/support/admin/chat/{_selectedPlayerId}"
            : $"{config.backendUrl}/api/support/chat";

        string body = JsonUtility.ToJson(new SendMessageRequest { text = text, attachmentUrl = null });

        using var req = new UnityWebRequest(url, "POST");
        req.uploadHandler   = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");
        config.SetHeaders(req, PlayerSession.Token);

        yield return req.SendWebRequest();

        if (sendButton != null) sendButton.interactable = true;

        if (req.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError($"[Chat] Send failed ({req.responseCode}): {req.downloadHandler?.text ?? req.error}");
            yield break;
        }

        // Luôn spawn bubble từ response — không phụ thuộc SignalR
        // SignalR sẽ echo cho phía bên kia (admin/player), không echo lại cho sender
        var dto = JsonUtility.FromJson<SupportMessageDto>(req.downloadHandler.text);
        if (dto != null)
        {
            SpawnMessageBubble(dto);
            ScrollToBottom();
        }
    }

    // ── API Fetch Helpers ─────────────────────────────────────────────────────

    IEnumerator FetchPlayerHistory(Action<SupportMessageDto[]> onDone)
    {
        using var req = UnityWebRequest.Get($"{config.backendUrl}/api/support/chat");
        config.SetHeaders(req, PlayerSession.Token);
        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError($"[Chat] FetchPlayerHistory failed: {req.error}");
            onDone(null);
            yield break;
        }

        string json    = "{\"items\":" + req.downloadHandler.text + "}";
        var    wrapper = JsonUtility.FromJson<MessageListWrapper>(json);
        onDone(wrapper?.items);
    }

    IEnumerator FetchActivePlayers(Action<ActiveChatPlayerDto[]> onDone)
    {
        using var req = UnityWebRequest.Get($"{config.backendUrl}/api/support/admin/players");
        config.SetHeaders(req, PlayerSession.Token);
        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError($"[Chat] FetchActivePlayers failed: {req.error}");
            onDone(null);
            yield break;
        }

        string json    = "{\"items\":" + req.downloadHandler.text + "}";
        var    wrapper = JsonUtility.FromJson<PlayerListWrapper>(json);
        onDone(wrapper?.items);
    }

    IEnumerator FetchAdminHistory(string playerId, Action<SupportMessageDto[]> onDone)
    {
        using var req = UnityWebRequest.Get(
            $"{config.backendUrl}/api/support/admin/chat/{playerId}");
        config.SetHeaders(req, PlayerSession.Token);
        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError($"[Chat] FetchAdminHistory failed: {req.error}");
            onDone(null);
            yield break;
        }

        string json    = "{\"items\":" + req.downloadHandler.text + "}";
        var    wrapper = JsonUtility.FromJson<MessageListWrapper>(json);
        onDone(wrapper?.items);
    }

    // ── SignalR ───────────────────────────────────────────────────────────────

    void ConnectSignalR()
    {
        if (!PlayerSession.IsLoggedIn) return;
        _cts = new CancellationTokenSource();
        Task.Run(() => SignalRConnectAsync(_cts.Token));
        Debug.Log("[Chat] SignalR connecting...");
    }

    void DisconnectSignalR()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;

        if (_ws != null)
        {
            try
            {
                if (_ws.State == WebSocketState.Open || _ws.State == WebSocketState.Connecting)
                    _ws.Abort();
                _ws.Dispose();
            }
            catch { /* ignore */ }
            _ws = null;
        }
    }

    async Task SignalRConnectAsync(CancellationToken token)
    {
        try
        {
            string baseUrl  = config.backendUrl.TrimEnd('/');
            string wsScheme = baseUrl.StartsWith("https", StringComparison.OrdinalIgnoreCase) ? "wss" : "ws";
            string rawHost  = baseUrl.Substring(baseUrl.IndexOf("://", StringComparison.Ordinal) + 3);
            string wsUrl    = $"{wsScheme}://{rawHost}/hubs/support?access_token={Uri.EscapeDataString(PlayerSession.Token)}";

            _ws = new ClientWebSocket();
            _ws.Options.SetRequestHeader("ngrok-skip-browser-warning", "true");

            await _ws.ConnectAsync(new Uri(wsUrl), token);

            // SignalR handshake
            string handshake = $"{{\"protocol\":\"json\",\"version\":1}}{RS}";
            await _ws.SendAsync(
                new ArraySegment<byte>(Encoding.UTF8.GetBytes(handshake)),
                WebSocketMessageType.Text, true, token);

            Debug.Log("[Chat] SignalR connected.");
            _ = Task.Run(() => SignalRPingLoopAsync(token), token);
            await SignalRReceiveLoopAsync(token);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            Debug.LogError($"[Chat] SignalR error: {ex.Message}");
        }
    }

    async Task SignalRPingLoopAsync(CancellationToken token)
    {
        byte[] ping = Encoding.UTF8.GetBytes($"{{\"type\":6}}{RS}");
        while (!token.IsCancellationRequested && _ws?.State == WebSocketState.Open)
        {
            try
            {
                await Task.Delay(15000, token);
                if (_ws?.State == WebSocketState.Open)
                    await _ws.SendAsync(new ArraySegment<byte>(ping), WebSocketMessageType.Text, true, token);
            }
            catch { break; }
        }
    }

    async Task SignalRReceiveLoopAsync(CancellationToken token)
    {
        var buffer = new byte[8192];
        var ms     = new System.IO.MemoryStream();

        while (!token.IsCancellationRequested && _ws?.State == WebSocketState.Open)
        {
            var result = await _ws.ReceiveAsync(new ArraySegment<byte>(buffer), token);

            if (result.MessageType == WebSocketMessageType.Close) break;

            ms.Write(buffer, 0, result.Count);

            if (!result.EndOfMessage) continue;

            string raw = Encoding.UTF8.GetString(ms.ToArray());
            ms.SetLength(0);

            foreach (var frame in raw.Split(RS))
            {
                if (string.IsNullOrWhiteSpace(frame)) continue;
                ProcessSignalRFrame(frame.Trim());
            }
        }
    }

    void ProcessSignalRFrame(string json)
    {
        if (json == "{}") return;           // handshake ack
        if (json.Contains("\"type\":6")) return; // ping

        try
        {
            // Parse "target" field
            var hub = JsonUtility.FromJson<SignalRFrame>(json);
            if (hub == null || hub.target != "ReceiveMessage") return;

            // Extract arguments[1] — the SupportMessageDto object
            int argStart = json.IndexOf("\"arguments\":[", StringComparison.Ordinal);
            if (argStart < 0) return;

            // Skip playerId string argument, find the second object {}
            int firstObj = json.IndexOf('{', argStart + 12);
            if (firstObj < 0) return;
            int secondObj = json.IndexOf('{', firstObj + 1);
            if (secondObj < 0) return;

            string msgJson = ExtractObject(json, secondObj);
            if (string.IsNullOrEmpty(msgJson)) return;

            var dto = JsonUtility.FromJson<SupportMessageDto>(msgJson);
            if (dto == null) return;

            // Capture for closure
            var captured = dto;

            _mainThread.Enqueue(() => OnMessageReceived(captured));
        }
        catch (Exception ex)
        {
            Debug.LogError($"[Chat] SignalR parse error: {ex.Message} | {json}");
        }
    }

    void OnMessageReceived(SupportMessageDto msg)
    {
        // Chỉ hiện nếu đang xem đúng conversation
        bool relevant = _isAdmin
            ? msg.sender == "user"   // admin nhận message từ player đang chọn
            : true;                  // player nhận mọi message trong conversation của mình

        if (!relevant) return;

        SpawnMessageBubble(msg);
        ScrollToBottom();

        // Cập nhật preview trong left panel nếu là player mode
        if (!_isAdmin && _selectedListItem != null)
            _selectedListItem.SetupAsSupport(msg.text, null);
    }

    // ── UI Helpers ────────────────────────────────────────────────────────────

    ChatListItem SpawnListItem()
    {
        if (chatListItemPrefab == null)
        {
            Debug.LogError("[Chat] chatListItemPrefab chưa được gán trong Inspector!");
            return null;
        }
        if (listContainer == null)
        {
            Debug.LogError("[Chat] listContainer chưa được gán trong Inspector!");
            return null;
        }

        var obj  = Instantiate(chatListItemPrefab, listContainer);
        var item = obj.GetComponent<ChatListItem>();

        if (item == null)
            Debug.LogError("[Chat] Prefab ChatListItem thiếu component ChatListItem.cs!");

        return item;
    }

    void SelectListItem(ChatListItem item)
    {
        if (_selectedListItem != null) _selectedListItem.SetSelected(false);
        _selectedListItem = item;
        if (_selectedListItem != null) _selectedListItem.SetSelected(true);
    }

    void ClearList()
    {
        foreach (Transform t in listContainer) Destroy(t.gameObject);
    }

    void ClearMessages()
    {
        foreach (var obj in _messageObjects) if (obj != null) Destroy(obj);
        _messageObjects.Clear();
    }

    void OnBackClicked()
    {
        SceneManager.LoadScene(sceneConfig.mainMenuScene);
    }

    // ── DTOs ─────────────────────────────────────────────────────────────────

    [Serializable]
    private class SignalRFrame
    {
        public int    type;
        public string target;
    }

    static string ExtractObject(string text, int start)
    {
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
}
