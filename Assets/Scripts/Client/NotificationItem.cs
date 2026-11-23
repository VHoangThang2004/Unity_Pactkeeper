using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// One row in the mailbox list.
/// </summary>
public class NotificationItem : MonoBehaviour
{
    static readonly Color32 ReadColor = new Color32(0xA9, 0xA9, 0xA9, 0xFF);

    [Header("UI Elements")]
    [SerializeField] private TMP_Text messageText;
    [SerializeField] private TMP_Text timeText;
    [SerializeField] private Image iconType;
    [SerializeField] private Image selectedBackground;

    private NotificationDto notification;
    private MailboxManager mailboxManager;
    private Color defaultBackgroundColor = Color.white;

    void Awake()
    {
        if (selectedBackground != null)
            defaultBackgroundColor = selectedBackground.color;
    }

    public void Setup(NotificationDto notification, MailboxManager mailboxManager, Sprite typeIcon)
    {
        this.notification = notification;
        this.mailboxManager = mailboxManager;

        if (messageText != null)
        {
            if (string.IsNullOrEmpty(notification.title))
                messageText.text = notification.message;
            else
                messageText.text = notification.title + "\n" + notification.message;
        }

        if (timeText != null)
            timeText.text = MailboxManager.FormatListDate(notification.createdAt);

        if (iconType != null)
        {
            iconType.sprite = typeIcon;
            iconType.enabled = typeIcon != null;
        }

        if (selectedBackground != null)
            selectedBackground.gameObject.SetActive(true);

        ApplyReadVisual();
    }

    public void ApplyReadVisual()
    {
        if (selectedBackground == null || notification == null)
            return;

        selectedBackground.color = notification.isRead ? ReadColor : defaultBackgroundColor;
    }

    public void OnClick()
    {
        if (notification == null || mailboxManager == null)
            return;

        mailboxManager.OnNotificationSelected(this, notification);
    }

    public void SetSelected(bool isSelected)
    {
        ApplyReadVisual();
    }
}
