using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Manages red notification dots on MainMenu nav buttons.
///
/// Tab        Logic
/// ───────    ──────────────────────────────────────────────────────────────
/// Mailbox  — any notification with isRead=false, OR newer than last open
/// Shop     — active shops available (cleared when player opens Shop)
/// Chat     — any admin message newer than last time player opened Chat tab
/// Friend   — pending friend requests > 0, OR new request arrived via SignalR
///
/// Quest dot removed — not needed anymore.
///
/// "New since last visit" tracked via PlayerPrefs UTC timestamps.
/// </summary>
public class NotificationDotManager : MonoBehaviour
{
    // ── Inspector ─────────────────────────────────────────────────────────────

    [Header("Config")]
    [SerializeField] private BackendConfig config;

    [Tooltip("Background re-check interval (seconds).")]
    [SerializeField] private float refreshIntervalSeconds = 60f;

    [Header("Dot GameObjects")]
    [SerializeField] private GameObject mailboxDot;
    [SerializeField] private GameObject shopDot;
    [SerializeField] private GameObject chatDot;
    [SerializeField] private GameObject friendDot;

    // ── PlayerPrefs keys ──────────────────────────────────────────────────────

    private const string KeyMailbox = "MailboxLastSeenAt";
    private const string KeyChat    = "ChatLastSeenAt";

    // ── DTOs ──────────────────────────────────────────────────────────────────

    [System.Serializable] private class NotificationDto    { public bool isRead; public string createdAt; }
    [System.Serializable] private class NotificationWrapper{ public NotificationDto[] items; }

    [System.Serializable] private class ShopStub           { public string id; }
    [System.Serializable] private class ShopWrapper        { public ShopStub[] items; }

    [System.Serializable] private class SupportMsgDto      { public string sender; public string createdAt; }
    [System.Serializable] private class SupportMsgWrapper  { public SupportMsgDto[] items; }

    [System.Serializable] private class FriendRequestStub  { public string requestId; }
    [System.Serializable] private class FriendRequestWrapper{ public FriendRequestStub[] items; }

    MonoBehaviour _runner;
    bool _autoRefreshStarted;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    void Start()
    {
        // Không gọi HideAll() ở đây — hãy set inactive các dot trong Editor sẵn.
        // Gọi HideAll() sẽ SetActive(false) các dot GO, nếu vô tình gán nhầm
        // chính GO này vào một field thì sẽ tắt luôn NotificationDotManager.
    }

    void OnEnable()
    {
        // Hook vào SignalR events để cập nhật dot ngay lập tức khi có event mới
        if (LobbyHubClient.Instance != null)
        {
            LobbyHubClient.Instance.OnFriendRequestReceived += OnFriendRequestReceived;
        }
    }

    void OnDisable()
    {
        if (LobbyHubClient.Instance != null)
        {
            LobbyHubClient.Instance.OnFriendRequestReceived -= OnFriendRequestReceived;
        }
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Host used to run coroutines. Must be an active GameObject
    /// (typically MainMenuManager). If this component sits on an inactive
    /// dot object, Unity cannot StartCoroutine on it.
    /// </summary>
    public void SetRunner(MonoBehaviour runner)
    {
        _runner = runner;
    }

    public void RefreshAll()
    {
        var host = GetRunner();
        if (host == null)
        {
            Debug.LogWarning("[NotificationDot] Cannot refresh — manager GameObject is inactive. " +
                             "Put NotificationDotManager on an always-active object (e.g. MainMenuController).");
            return;
        }

        host.StartCoroutine(CheckMailbox());
        host.StartCoroutine(CheckShop());
        host.StartCoroutine(CheckChat());
        host.StartCoroutine(CheckFriend());
    }

    public void StartAutoRefresh()
    {
        if (_autoRefreshStarted) return;

        var host = GetRunner();
        if (host == null)
        {
            Debug.LogWarning("[NotificationDot] Cannot start auto-refresh — manager GameObject is inactive. " +
                             "Put NotificationDotManager on an always-active object (e.g. MainMenuController).");
            return;
        }

        _autoRefreshStarted = true;
        host.StartCoroutine(AutoRefreshLoop());
    }

    // ── Clear dots khi player mở tab ─────────────────────────────────────────

    public void ClearMailboxDot()
    {
        PlayerPrefs.SetString(KeyMailbox, System.DateTime.UtcNow.ToString("o"));
        PlayerPrefs.Save();
        SetDot(mailboxDot, false);
    }

    public void ClearShopDot() => SetDot(shopDot, false);

    public void ClearChatDot()
    {
        PlayerPrefs.SetString(KeyChat, System.DateTime.UtcNow.ToString("o"));
        PlayerPrefs.Save();
        SetDot(chatDot, false);
    }

    public void ClearFriendDot() => SetDot(friendDot, false);

    // ── SignalR real-time handlers ────────────────────────────────────────────

    // Được gọi ngay khi có friend request mới qua SignalR (không cần chờ polling)
    private void OnFriendRequestReceived(string _) => SetDot(friendDot, true);

    // ── Check coroutines ──────────────────────────────────────────────────────

    IEnumerator CheckMailbox()
    {
        using var req = UnityWebRequest.Get($"{config.backendUrl}/api/Notification");
        config.SetHeaders(req, PlayerSession.Token);
        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success) yield break;

        var wrapper = JsonUtility.FromJson<NotificationWrapper>(
            "{\"items\":" + req.downloadHandler.text + "}");

        if (wrapper?.items == null || wrapper.items.Length == 0)
        {
            SetDot(mailboxDot, false);
            yield break;
        }

        // Condition A: bất kỳ notification chưa đọc
        bool hasUnread = false;
        foreach (var n in wrapper.items)
            if (!n.isRead) { hasUnread = true; break; }

        // Condition B: có notification mới hơn lần cuối player mở Mailbox
        bool hasNew = false;
        string lastSeenStr = PlayerPrefs.GetString(KeyMailbox, string.Empty);
        if (!string.IsNullOrEmpty(lastSeenStr) &&
            System.DateTime.TryParse(lastSeenStr, null,
                System.Globalization.DateTimeStyles.RoundtripKind,
                out var lastSeen))
        {
            foreach (var n in wrapper.items)
            {
                if (string.IsNullOrEmpty(n.createdAt)) continue;
                if (System.DateTime.TryParse(n.createdAt, null,
                        System.Globalization.DateTimeStyles.RoundtripKind,
                        out var created))
                    if (created > lastSeen) { hasNew = true; break; }
            }
        }
        else
        {
            hasNew = wrapper.items.Length > 0;
        }

        SetDot(mailboxDot, hasUnread || hasNew);
    }

    IEnumerator CheckShop()
    {
        using var req = UnityWebRequest.Get($"{config.backendUrl}/api/shop/active");
        config.SetHeaders(req, PlayerSession.Token);
        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success) yield break;

        var wrapper = JsonUtility.FromJson<ShopWrapper>(
            "{\"items\":" + req.downloadHandler.text + "}");

        SetDot(shopDot, wrapper?.items != null && wrapper.items.Length > 0);
    }

    IEnumerator CheckChat()
    {
        // Chỉ check cho Player — Admin không cần dot này
        if (PlayerSession.IsAdmin) yield break;

        using var req = UnityWebRequest.Get($"{config.backendUrl}/api/support/chat");
        config.SetHeaders(req, PlayerSession.Token);
        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success) yield break;

        var wrapper = JsonUtility.FromJson<SupportMsgWrapper>(
            "{\"items\":" + req.downloadHandler.text + "}");

        if (wrapper?.items == null || wrapper.items.Length == 0)
        {
            SetDot(chatDot, false);
            yield break;
        }

        // Dot bật nếu có tin nhắn từ admin mới hơn lần cuối player mở Chat
        string lastSeenStr = PlayerPrefs.GetString(KeyChat, string.Empty);
        bool hasNewAdminMsg = false;

        if (!string.IsNullOrEmpty(lastSeenStr) &&
            System.DateTime.TryParse(lastSeenStr, null,
                System.Globalization.DateTimeStyles.RoundtripKind,
                out var lastSeen))
        {
            foreach (var m in wrapper.items)
            {
                if (m.sender != "admin") continue;
                if (string.IsNullOrEmpty(m.createdAt)) continue;
                if (System.DateTime.TryParse(m.createdAt, null,
                        System.Globalization.DateTimeStyles.RoundtripKind,
                        out var created))
                    if (created > lastSeen) { hasNewAdminMsg = true; break; }
            }
        }
        else
        {
            // Chưa từng mở Chat — dot bật nếu admin đã từng nhắn
            foreach (var m in wrapper.items)
                if (m.sender == "admin") { hasNewAdminMsg = true; break; }
        }

        SetDot(chatDot, hasNewAdminMsg);
    }

    IEnumerator CheckFriend()
    {
        using var req = UnityWebRequest.Get($"{config.backendUrl}/api/friends/requests");
        config.SetHeaders(req, PlayerSession.Token);
        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success) yield break;

        var wrapper = JsonUtility.FromJson<FriendRequestWrapper>(
            "{\"items\":" + req.downloadHandler.text + "}");

        bool hasPending = wrapper?.items != null && wrapper.items.Length > 0;
        SetDot(friendDot, hasPending);
    }

    // ── Background loop ───────────────────────────────────────────────────────

    IEnumerator AutoRefreshLoop()
    {
        while (true)
        {
            yield return new WaitForSeconds(refreshIntervalSeconds);
            if (PlayerSession.IsLoggedIn) RefreshAll();
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Ẩn/hiện dot bằng cách toggle Image component thay vì SetActive.
    /// SetActive sẽ deactivate GO → nếu GO đó chứa script này thì tắt luôn manager.
    /// </summary>
    void SetDot(GameObject dot, bool show)
    {
        if (dot == null) return;

        // Dùng CanvasGroup nếu có
        var cg = dot.GetComponent<CanvasGroup>();
        if (cg != null)
        {
            cg.alpha          = show ? 1f : 0f;
            cg.blocksRaycasts = false; // dot không cần nhận click
            return;
        }

        // Fallback: toggle Image component
        var img = dot.GetComponent<UnityEngine.UI.Image>();
        if (img != null)
        {
            img.enabled = show;
            return;
        }

        // Last resort: SetActive — never deactivate the GO that hosts this script
        if (dot == gameObject) return;
        dot.SetActive(show);
    }

    MonoBehaviour GetRunner()
    {
        if (isActiveAndEnabled) return this;
        if (_runner != null && _runner.isActiveAndEnabled) return _runner;
        return null;
    }

    void HideAll()
    {
        SetDot(mailboxDot, false);
        SetDot(shopDot,    false);
        SetDot(chatDot,    false);
        SetDot(friendDot,  false);
    }
}
