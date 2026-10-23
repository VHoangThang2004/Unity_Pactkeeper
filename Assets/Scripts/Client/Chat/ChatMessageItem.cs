using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Prefab script cho một bubble tin nhắn trong right panel của chat.
///
/// Hỗ trợ 2 layout:
///   - Player message  : bubble bên phải, màu xanh/tím
///   - Admin message   : bubble bên trái, màu xám
///
/// Prefab hierarchy gợi ý:
///   ChatMessageItem
///   ├── PlayerBubble        (active khi sender = "user")
///   │   ├── BubbleBackground (Image)
///   │   ├── MessageText      (TMP_Text)
///   │   └── TimeText         (TMP_Text)
///   └── AdminBubble         (active khi sender = "admin")
///       ├── SenderNameText   (TMP_Text) — "Keeper of Records"
///       ├── BubbleBackground (Image)
///       ├── MessageText      (TMP_Text)
///       └── TimeText         (TMP_Text)
/// </summary>
public class ChatMessageItem : MonoBehaviour
{
    [Header("Player Bubble (sender = user, right side)")]
    [SerializeField] private GameObject playerBubble;
    [SerializeField] private TMP_Text   playerMessageText;
    [SerializeField] private TMP_Text   playerTimeText;

    [Header("Admin Bubble (sender = admin, left side)")]
    [SerializeField] private GameObject adminBubble;
    [SerializeField] private TMP_Text   adminSenderNameText;
    [SerializeField] private TMP_Text   adminMessageText;
    [SerializeField] private TMP_Text   adminTimeText;

    // ── Setup ─────────────────────────────────────────────────────────────────

    public void Setup(ChatManager.SupportMessageDto msg)
    {
        bool isPlayer = msg.sender == "user";

        if (playerBubble != null) playerBubble.SetActive(isPlayer);
        if (adminBubble  != null) adminBubble.SetActive(!isPlayer);

        string timeStr = FormatTime(msg.createdAt);

        if (isPlayer)
        {
            if (playerMessageText != null) playerMessageText.text = msg.text;
            if (playerTimeText    != null) playerTimeText.text    = timeStr;
        }
        else
        {
            if (adminSenderNameText != null) adminSenderNameText.text = msg.senderName;
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
            // Nếu cùng ngày hôm nay → chỉ hiện giờ:phút, ngược lại hiện ngày/tháng
            return local.Date == System.DateTime.Today
                ? local.ToString("HH:mm")
                : local.ToString("dd/MM HH:mm");
        }

        return iso;
    }
}
