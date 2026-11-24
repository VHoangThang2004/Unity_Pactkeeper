using UnityEngine;
using TMPro;

/// <summary>
/// Prefab script for message bubbles in the chat right panel.
///
/// Layout:
///   - My message     : right side bubble (playerBubble)
///   - Friend message : left side bubble
/// </summary>
public class ChatMessageItem : MonoBehaviour
{
    [Header("My Message Bubble (right side)")]
    [SerializeField] private GameObject playerBubble;
    [SerializeField] private TMP_Text   playerMessageText;
    [SerializeField] private TMP_Text   playerTimeText;

    [Header("Friend Message Bubble (left side)")]
    [SerializeField] private GameObject friendBubble;
    [SerializeField] private TMP_Text   friendSenderNameText;
    [SerializeField] private TMP_Text   friendMessageText;
    [SerializeField] private TMP_Text   friendTimeText;

    public void Setup(DirectChatMessageDto msg)
    {
        bool isMyMessage = msg.senderId == PlayerSession.PlayerId
            || (!string.IsNullOrEmpty(msg.senderUsername)
                && msg.senderUsername == PlayerSession.Username);

        if (playerBubble != null) playerBubble.SetActive(isMyMessage);
        if (friendBubble != null) friendBubble.SetActive(!isMyMessage);

        string timeStr = FormatTime(msg.createdAt);

        if (isMyMessage)
        {
            if (playerMessageText != null) playerMessageText.text = msg.text;
            if (playerTimeText    != null) playerTimeText.text    = timeStr;
        }
        else
        {
            if (friendSenderNameText != null) friendSenderNameText.text = msg.senderUsername;
            if (friendMessageText    != null) friendMessageText.text    = msg.text;
            if (friendTimeText       != null) friendTimeText.text       = timeStr;
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    static string FormatTime(string iso)
    {
        if (string.IsNullOrEmpty(iso)) return string.Empty;

        if (System.DateTime.TryParse(iso,
                null,
                System.Globalization.DateTimeStyles.RoundtripKind,
                out var dt))
        {
            var local = dt.ToLocalTime();
            return local.Date == System.DateTime.Today
                ? local.ToString("HH:mm")
                : local.ToString("dd/MM HH:mm");
        }

        return iso;
    }
}
