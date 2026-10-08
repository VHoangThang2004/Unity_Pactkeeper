using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Individual notification item in the mailbox list.
/// Only displays message text in the left panel.
/// Setup UI in the Inspector.
/// </summary>
public class NotificationItem : MonoBehaviour
{
    [Header("UI Elements")]
    [SerializeField] private TMP_Text messageText;
    [SerializeField] private Image selectedBackground;

    private NotificationDto notification;
    private MailboxManager mailboxManager;

    public void Setup(NotificationDto notification, MailboxManager mailboxManager)
    {
        this.notification = notification;
        this.mailboxManager = mailboxManager;

        // Set message text only
        if (messageText != null)
            messageText.text = notification.message;

        // Set selected background
        if (selectedBackground != null)
            selectedBackground.gameObject.SetActive(false);
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
}
