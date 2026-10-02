using System.Collections;
using System.Collections.Generic;
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

    private List<NotificationDto> notifications = new List<NotificationDto>();
    private NotificationItem selectedItem = null;

    void Awake()
    {
        if (closeButton != null)
            closeButton.onClick.AddListener(Close);
        if (readAllButton != null)
            readAllButton.onClick.AddListener(ReadAll);

        HidePanel();
        HideDetailPanel();
    }

    void OnDestroy()
    {
        if (closeButton != null)
            closeButton.onClick.RemoveListener(Close);
        if (readAllButton != null)
            readAllButton.onClick.RemoveListener(ReadAll);
    }

    public void Open()
    {
        if (panel != null)
            panel.SetActive(true);

        HideDetailPanel();
        selectedItem = null;
        StartCoroutine(LoadNotifications());
    }

    public void Close()
    {
        HidePanel();
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
        using var request = UnityWebRequest.Get($"{config.backendUrl}/api/Notification");
        config.SetHeaders(request, PlayerSession.Token);
        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError($"[Mailbox] FetchNotifications failed: {request.error}");
            ShowEmptyState();
            yield break;
        }

        var wrapper = JsonUtility.FromJson<NotificationArrayWrapper>("{\"items\":" + request.downloadHandler.text + "}");
        if (wrapper.items != null)
            notifications.AddRange(wrapper.items);

        // Spawn notification items
        foreach (var notification in notifications)
        {
            var item = Instantiate(notificationItemPrefab, notificationListContainer);
            var itemScript = item.GetComponent<NotificationItem>();
            if (itemScript != null)
                itemScript.Setup(notification, this);
        }

        // Show/hide empty state
        if (notifications.Count == 0)
            ShowEmptyState();
        else
            HideEmptyState();
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

        // Mark as read
        if (!notification.isRead)
        {
            StartCoroutine(MarkAsRead(notification.id));
            notification.isRead = true;
            item.UpdateReadStatus(true);
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
            detailTimeText.text = FormatTime(notification.createdAt);

        if (detailTypeIcon != null)
            SetDetailTypeIcon(notification.type);
    }

    void SetDetailTypeIcon(string type)
    {
        // TODO: Set icon sprite based on type
        // Similar to NotificationItem.SetTypeIcon
    }

    string FormatTime(string createdAt)
    {
        // TODO: Parse and format the time string
        return createdAt;
    }

    public IEnumerator MarkAsRead(string notificationId)
    {
        using var request = UnityWebRequest.PostWwwForm(
            $"{config.backendUrl}/api/Notification/{notificationId}/read", "");
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
            $"{config.backendUrl}/api/Notification/read-all", "");
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
        }

        // Update UI
        foreach (Transform child in notificationListContainer)
        {
            var item = child.GetComponent<NotificationItem>();
            if (item != null)
                item.UpdateReadStatus(true);
        }
    }

    void HidePanel()
    {
        if (panel != null)
            panel.SetActive(false);
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

    [System.Serializable]
    private class NotificationArrayWrapper
    {
        public NotificationDto[] items;
    }
}
