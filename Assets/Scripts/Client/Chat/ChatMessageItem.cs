using UnityEngine;
using TMPro;

/// <summary>
/// Prefab script for message bubbles in the chat right panel.
///
/// Layout:
///   - My message     : right side bubble (playerBubble)
///   - Friend message : left side bubble (adminBubble / otherBubble)
/// </summary>
public class ChatMessageItem : MonoBehaviour
{
    [Header("My Message Bubble (right side)")]
    [SerializeField] private GameObject playerBubble;
    [SerializeField] private TMP_Text   playerMessageText;
    [SerializeField] private TMP_Text   playerTimeText;

    [Header("Friend Message Bubble (left side)")]
    [SerializeField] private GameObject adminBubble;
    [SerializeField] private TMP_Text   adminSenderNameText;
    [SerializeField] private TMP_Text   adminMessageText;
    [SerializeField] private TMP_Text   adminTimeText;

    // ── Setup ─────────────────────────────────────────────────────────────────

    public void Setup(DirectChatMessageDto msg)
    {
        bool isMyMessage = msg.senderId == PlayerSession.PlayerId;

        if (playerBubble != null) playerBubble.SetActive(isMyMessage);
        if (adminBubble  != null) adminBubble.SetActive(!isMyMessage);

        string timeStr = FormatTime(msg.createdAt);

        if (isMyMessage)
        {
            if (playerMessageText != null) playerMessageText.text = msg.text;
            if (playerTimeText    != null) playerTimeText.text    = timeStr;
        }
        else
        {
            if (adminSenderNameText != null) adminSenderNameText.text = msg.senderUsername;
            if (adminMessageText    != null) adminMessageText.text    = msg.text;
            if (adminTimeText       != null) adminTimeText.text       = timeStr;
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
