using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Manages red notification dots on MainMenu nav buttons.
///
/// Logic per tab:
///   Mailbox  — has any notification with isRead = false OR
///              has a notification created AFTER the last time the player opened Mailbox
///   Quest    — has progress with isCompleted=true && isReceived=false (claimable)
///   Shop     — has active shops (dot just shows shop is available, cleared when opened)
///
/// "New since last visit" is tracked via PlayerPrefs timestamps (UTC ticks).
///   Key "MailboxLastSeenAt" — saved when player opens Mailbox tab
///   Compared against the latest notification's createdAt from the API
/// </summary>
public class NotificationDotManager : MonoBehaviour
{
    // ── Inspector ─────────────────────────────────────────────────────────────

    [Header("Config")]
    [SerializeField] private BackendConfig config;

    [Tooltip("How often (seconds) to silently re-check in the background.")]
    [SerializeField] private float refreshIntervalSeconds = 60f;

    [Header("Dot GameObjects — assign the red circle on each button")]
    [SerializeField] private GameObject mailboxDot;
    [SerializeField] private GameObject questDot;
    [SerializeField] private GameObject shopDot;

    // ── PlayerPrefs keys ──────────────────────────────────────────────────────

    private const string KeyMailbox = "MailboxLastSeenAt";

    // ── Minimal DTOs ──────────────────────────────────────────────────────────

    [System.Serializable]
    private class NotificationDto
    {
        public bool   isRead;
        public string createdAt; // ISO 8601 string from backend
    }

    [System.Serializable] private class NotificationWrapper  { public NotificationDto[]  items; }

    [System.Serializable] private class ProgressDto { public bool isCompleted; public bool isReceived; }
    [System.Serializable] private class ProgressWrapper     { public ProgressDto[]       items; }

    [System.Serializable] private class ShopStub  { public string id; }
    [System.Serializable] private class ShopWrapper          { public ShopStub[]          items; }

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    void Start() => HideAll();

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>Kick off a one-time refresh of all dots.</summary>
    public void RefreshAll()
    {
        StartCoroutine(CheckMailbox());
        StartCoroutine(CheckQuest());
        StartCoroutine(CheckShop());
    }

    /// <summary>Begin periodic background refresh.</summary>
    public void StartAutoRefresh() => StartCoroutine(AutoRefreshLoop());

    // ── Called by MainMenuManager when player opens a tab ────────────────────

    public void ClearMailboxDot()
    {
        // Save current time so only notifications newer than this trigger the dot next time
        PlayerPrefs.SetString(KeyMailbox, System.DateTime.UtcNow.ToString("o"));
        PlayerPrefs.Save();
        SetDot(mailboxDot, false);
    }

    public void ClearQuestDot()  => SetDot(questDot,   false);
    public void ClearShopDot()   => SetDot(shopDot,    false);

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

        // Condition A: any unread notification
        bool hasUnread = false;
        foreach (var n in wrapper.items)
            if (!n.isRead) { hasUnread = true; break; }

        // Condition B: any notification newer than last time player opened Mailbox
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
                {
                    if (created > lastSeen) { hasNew = true; break; }
                }
            }
        }
        else
        {
            // Player has never opened Mailbox this install → treat everything as new
            hasNew = wrapper.items.Length > 0;
        }

        SetDot(mailboxDot, hasUnread || hasNew);
    }

    IEnumerator CheckQuest()
    {
        using var req = UnityWebRequest.Get($"{config.backendUrl}/api/rewardprogress");
        config.SetHeaders(req, PlayerSession.Token);
        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success) yield break;

        var wrapper = JsonUtility.FromJson<ProgressWrapper>(
            "{\"items\":" + req.downloadHandler.text + "}");

        bool hasClaimable = false;
        if (wrapper?.items != null)
            foreach (var p in wrapper.items)
                if (p.isCompleted && !p.isReceived) { hasClaimable = true; break; }

        SetDot(questDot, hasClaimable);
    }

    IEnumerator CheckShop()
    {
        using var req = UnityWebRequest.Get($"{config.backendUrl}/api/shop/active");
        config.SetHeaders(req, PlayerSession.Token);
        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success) yield break;

        var wrapper = JsonUtility.FromJson<ShopWrapper>(
            "{\"items\":" + req.downloadHandler.text + "}");

        bool hasShop = wrapper?.items != null && wrapper.items.Length > 0;
        SetDot(shopDot, hasShop);
    }

    // ── Background loop ───────────────────────────────────────────────────────

    IEnumerator AutoRefreshLoop()
    {
        while (true)
        {
            yield return new WaitForSeconds(refreshIntervalSeconds);
            RefreshAll();
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    static void SetDot(GameObject dot, bool show)
    {
        if (dot != null) dot.SetActive(show);
    }

    void HideAll()
    {
        SetDot(mailboxDot, false);
        SetDot(questDot,   false);
        SetDot(shopDot,    false);
    }
}
