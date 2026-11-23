using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Mailbox UI manager. Fetches notifications from backend and displays them.
/// Wire UI in the Inspector; panel starts hidden until Open() is called.
/// </summary>
public class MailboxManager : MonoBehaviour
{
    [Header("Config")]
    [SerializeField] private BackendConfig config;

    [Header("Panel")]
    [SerializeField] private GameObject panel;
    [SerializeField] private Button closeButton;

    [Header("Left Panel - List")]
    [SerializeField] private Transform notificationListContainer;
    [SerializeField] private GameObject notificationItemPrefab;
    [SerializeField] private Button readAllButton;

    [Header("Right Panel - Detail")]
    [SerializeField] private GameObject detailPanel;
    [SerializeField] private TMP_Text detailTitleText;
    [SerializeField] private TMP_Text detailMessageText;
    [SerializeField] private TMP_Text detailTimeText;
    [SerializeField] private Image detailTypeIcon;
    [SerializeField] private GameObject detailEmptyState;

    [Header("Empty State")]
    [SerializeField] private GameObject emptyStatePanel;

    [Header("Filters")]
    [SerializeField] private Button allButton;
    [SerializeField] private Button systemButton;
    [SerializeField] private Button noticeButton;

    [Header("Type Icons")]
    [SerializeField] private NotificationTypeIcon[] typeIcons;

    static readonly Color32 ActiveFilterColor = new Color32(0xA9, 0xA9, 0xA9, 0xFF);

    enum MailFilter { All, System, Notice }

    readonly List<NotificationDto> notifications = new List<NotificationDto>();
    readonly List<NotificationItem> spawnedItems = new List<NotificationItem>();
    NotificationItem selectedItem;
    MailFilter filter = MailFilter.All;
    Color allDefaultColor = Color.white;
    Color systemDefaultColor = Color.white;
    Color noticeDefaultColor = Color.white;

    void Awake()
    {
        if (closeButton != null)
            closeButton.onClick.AddListener(Close);
        if (readAllButton != null)
            readAllButton.onClick.AddListener(ReadAll);

        PrepareFilterButton(allButton, MailFilter.All, ref allDefaultColor);
        PrepareFilterButton(systemButton, MailFilter.System, ref systemDefaultColor);
        PrepareFilterButton(noticeButton, MailFilter.Notice, ref noticeDefaultColor);
        ApplyFilterColors();

        HidePanel();
        HideDetailPanel();
    }

    void OnDestroy()
    {
        if (closeButton != null)
            closeButton.onClick.RemoveListener(Close);
        if (readAllButton != null)
            readAllButton.onClick.RemoveListener(ReadAll);
        if (allButton != null)
            allButton.onClick.RemoveAllListeners();
        if (systemButton != null)
            systemButton.onClick.RemoveAllListeners();
        if (noticeButton != null)
            noticeButton.onClick.RemoveAllListeners();
    }

    public void Open()
    {
        this.OpenPanel(panel);
        HideDetailPanel();
        selectedItem = null;
        filter = MailFilter.All;
        ApplyFilterColors();
        StartCoroutine(LoadNotifications());
    }

    public void Close()
    {
        this.ClosePanel(panel);
        HideDetailPanel();
        selectedItem = null;
    }

    IEnumerator LoadNotifications()
    {
        if (config == null)
        {
            Debug.LogError("[Mailbox] Config is null!");
            yield break;
        }

        // Clear existing items
        foreach (Transform child in notificationListContainer)
            Destroy(child.gameObject);

        notifications.Clear();

        // Fetch notifications
        using var request = UnityWebRequest.Get($"{config.backendUrl}/api/game-notification");
        config.SetHeaders(request, PlayerSession.Token);
        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError($"[Mailbox] FetchNotifications failed: {request.error}");
            ShowEmptyState();
            yield break;
        }

        var wrapper = JsonUtility.FromJson<NotificationArrayWrapper>("{\"items\":" + request.downloadHandler.text + "}");
        if (wrapper != null && wrapper.items != null)
        {
            foreach (var notification in wrapper.items)
                notification.ApplyReadState();
            notifications.AddRange(wrapper.items);
        }

        RebuildList();
    }

    public void OnNotificationSelected(NotificationItem item, NotificationDto notification)
    {
        // Deselect previous item
        if (selectedItem != null)
            selectedItem.SetSelected(false);

        // Select new item
        selectedItem = item;
        selectedItem.SetSelected(true);

        // Show detail
        ShowNotificationDetail(notification);

        if (!notification.isRead)
        {
            notification.isRead = true;
            notification.read = true;
            item.ApplyReadVisual();
            StartCoroutine(MarkAsRead(notification.id));
        }
    }

    void ShowNotificationDetail(NotificationDto notification)
    {
        if (detailPanel == null)
            return;

        detailPanel.SetActive(true);

        if (detailEmptyState != null)
            detailEmptyState.SetActive(false);

        if (detailTitleText != null)
            detailTitleText.text = notification.title;

        if (detailMessageText != null)
            detailMessageText.text = notification.message;

        if (detailTimeText != null)
            detailTimeText.text = FormatDetailTime(notification.createdAt);

        if (detailTypeIcon != null)
        {
            Sprite icon = IconFor(notification.type);
            detailTypeIcon.sprite = icon;
            detailTypeIcon.enabled = icon != null;
        }
    }

    public static string FormatListDate(string createdAt)
    {
        return TryParseTime(createdAt, out var time)
            ? time.ToLocalTime().ToString("MM/dd", CultureInfo.InvariantCulture)
            : createdAt;
    }

    static string FormatDetailTime(string createdAt)
    {
        return TryParseTime(createdAt, out var time)
            ? time.ToLocalTime().ToString("MM/dd/yyyy HH:mm", CultureInfo.InvariantCulture)
            : createdAt;
    }

    static bool TryParseTime(string createdAt, out DateTime time)
    {
        return DateTime.TryParse(createdAt, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out time);
    }

    public Sprite IconFor(string type)
    {
        if (typeIcons == null || string.IsNullOrEmpty(type))
            return null;

        foreach (var entry in typeIcons)
        {
            if (entry != null && string.Equals(entry.type, type, StringComparison.OrdinalIgnoreCase))
                return entry.icon;
        }

        return null;
    }

    public IEnumerator MarkAsRead(string notificationId)
    {
        using var request = UnityWebRequest.PostWwwForm(
            $"{config.backendUrl}/api/game-notification/{notificationId}/read", "");
        config.SetHeaders(request, PlayerSession.Token);
        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError($"[Mailbox] MarkAsRead failed: {request.error}");
        }
    }

    public void ReadAll()
    {
        StartCoroutine(ReadAllCoroutine());
    }

    IEnumerator ReadAllCoroutine()
    {
        using var request = UnityWebRequest.PostWwwForm(
            $"{config.backendUrl}/api/game-notification/read-all", "");
        config.SetHeaders(request, PlayerSession.Token);
        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError($"[Mailbox] ReadAll failed: {request.error}");
            yield break;
        }

        // Update all notifications as read
        foreach (var notification in notifications)
        {
            notification.isRead = true;
            notification.read = true;
        }

        foreach (var item in spawnedItems)
            item.ApplyReadVisual();
    }

    void HidePanel()
    {
        this.HidePanelImmediate(panel);
    }

    void HideDetailPanel()
    {
        if (detailPanel != null)
            detailPanel.SetActive(false);
        if (detailEmptyState != null)
            detailEmptyState.SetActive(true);
    }

    void ShowEmptyState()
    {
        if (emptyStatePanel != null)
            emptyStatePanel.SetActive(true);
    }

    void HideEmptyState()
    {
        if (emptyStatePanel != null)
            emptyStatePanel.SetActive(false);
    }

    void PrepareFilterButton(Button button, MailFilter value, ref Color defaultColor)
    {
        if (button == null)
            return;

        button.transition = Selectable.Transition.None;
        Image image = button.targetGraphic as Image;
        if (image != null)
            defaultColor = image.color;

        MailFilter captured = value;
        button.onClick.AddListener(() => SetFilter(captured));
    }

    void SetFilter(MailFilter value)
    {
        filter = value;
        ApplyFilterColors();
        selectedItem = null;
        HideDetailPanel();
        RebuildList();
    }

    void ApplyFilterColors()
    {
        SetFilterColor(allButton, filter == MailFilter.All ? ActiveFilterColor : allDefaultColor);
        SetFilterColor(systemButton, filter == MailFilter.System ? ActiveFilterColor : systemDefaultColor);
        SetFilterColor(noticeButton, filter == MailFilter.Notice ? ActiveFilterColor : noticeDefaultColor);
    }

    static void SetFilterColor(Button button, Color color)
    {
        if (button == null)
            return;

        Image image = button.targetGraphic as Image;
        if (image != null)
            image.color = color;
    }

    void RebuildList()
    {
        spawnedItems.Clear();
        if (notificationListContainer != null)
        {
            foreach (Transform child in notificationListContainer)
                Destroy(child.gameObject);
        }

        int shown = 0;
        foreach (var notification in notifications)
        {
            if (!PassesFilter(notification))
                continue;

            shown++;
            if (notificationItemPrefab == null || notificationListContainer == null)
                continue;

            var item = Instantiate(notificationItemPrefab, notificationListContainer);
            var itemScript = item.GetComponent<NotificationItem>();
            if (itemScript == null)
                continue;

            itemScript.Setup(notification, this, IconFor(notification.type));
            spawnedItems.Add(itemScript);
        }

        if (shown == 0)
            ShowEmptyState();
        else
            HideEmptyState();
    }

    bool PassesFilter(NotificationDto notification)
    {
        if (filter == MailFilter.All)
            return true;

        string type = notification.type ?? string.Empty;
        if (filter == MailFilter.System)
            return type.Equals("System", StringComparison.OrdinalIgnoreCase)
                || type.Equals("Maintenance", StringComparison.OrdinalIgnoreCase);

        return type.Equals("Announcement", StringComparison.OrdinalIgnoreCase)
            || type.Equals("MatchResult", StringComparison.OrdinalIgnoreCase);
    }

    [System.Serializable]
    private class NotificationArrayWrapper
    {
        public NotificationDto[] items;
    }
}

[System.Serializable]
public class NotificationTypeIcon
{
    public string type;
    public Sprite icon;
}
