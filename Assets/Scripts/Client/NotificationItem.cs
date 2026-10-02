using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Individual notification item in the mailbox list.
/// Setup UI in the Inspector.
/// </summary>
public class NotificationItem : MonoBehaviour
{
    [Header("UI Elements")]
    [SerializeField] private Image typeIcon;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text messageText;
    [SerializeField] private TMP_Text timeText;
    [SerializeField] private Image unreadIndicator;
    [SerializeField] private Image selectedBackground;

    private NotificationDto notification;
    private MailboxManager mailboxManager;

    public void Setup(NotificationDto notification, MailboxManager mailboxManager)
    {
        this.notification = notification;
        this.mailboxManager = mailboxManager;

        // Set text
        if (titleText != null)
            titleText.text = notification.title;

        if (messageText != null)
            messageText.text = notification.message;

        if (timeText != null)
            timeText.text = FormatTime(notification.createdAt);

        // Set unread indicator
        if (unreadIndicator != null)
            unreadIndicator.gameObject.SetActive(!notification.isRead);

        // Set selected background
        if (selectedBackground != null)
            selectedBackground.gameObject.SetActive(false);

        // Set icon based on type
        SetTypeIcon(notification.type);
    }

    public void OnClick()
    {
        if (notification == null || mailboxManager == null)
            return;

        mailboxManager.OnNotificationSelected(this, notification);
    }

    public void SetSelected(bool isSelected)
    {
        if (selectedBackground != null)
            selectedBackground.gameObject.SetActive(isSelected);
    }

    public void UpdateReadStatus(bool isRead)
    {
        if (unreadIndicator != null)
            unreadIndicator.gameObject.SetActive(!isRead);
    }

    void SetTypeIcon(string type)
    {
        if (typeIcon == null)
            return;

        // TODO: Set icon sprite based on type
        // You can assign sprites in the Inspector or load from a registry
        switch (type)
        {
            case "GachaReward":
                // typeIcon.sprite = gachaIcon;
                break;
            case "MatchResult":
                // typeIcon.sprite = matchIcon;
                break;
            case "StoryProgress":
                // typeIcon.sprite = storyIcon;
                break;
            case "System":
                // typeIcon.sprite = systemIcon;
                break;
            default:
                // typeIcon.sprite = defaultIcon;
                break;
        }
    }

    string FormatTime(string createdAt)
    {
        // TODO: Parse and format the time string
        // e.g., "2 hours ago", "Yesterday", etc.
        return createdAt;
    }
}
