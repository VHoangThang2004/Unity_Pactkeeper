using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Prefab script cho một entry trong left panel (danh sách conversation).
///
/// Với player: chỉ có 1 entry duy nhất — cuộc hội thoại với "Keeper of Records".
/// Với admin:  mỗi entry là 1 player khác nhau (ActiveChatPlayerDto).
///
/// Prefab hierarchy gợi ý:
///   ChatListItem
///   ├── SelectedBackground   (Image — hiện khi item đang được chọn)
///   ├── AvatarImage          (Image — optional, có thể bỏ qua)
///   ├── SenderNameText       (TMP_Text — tên người gửi / player)
///   └── LastMessageText      (TMP_Text — preview tin nhắn gần nhất)
/// </summary>
public class ChatListItem : MonoBehaviour
{
    [Header("UI Elements")]
    [SerializeField] private GameObject selectedBackground;
    [SerializeField] private TMP_Text   senderNameText;
    [SerializeField] private TMP_Text   lastMessageText;

    // ── Runtime ───────────────────────────────────────────────────────────────

    private System.Action _onClick;
    private UnityEngine.UI.Button _button;

    // ── Setup ─────────────────────────────────────────────────────────────────

    /// <summary>Dùng cho player — entry duy nhất "Support".</summary>
    public void SetupAsSupport(string lastMessage, System.Action onClick)
    {
        if (senderNameText  != null) senderNameText.text  = "Keeper of Records";
        if (lastMessageText != null) lastMessageText.text = lastMessage;
        SetSelected(false);
        WireClick(onClick);
    }

    /// <summary>Dùng cho admin — mỗi player là 1 entry.</summary>
    public void SetupAsPlayer(ChatManager.ActiveChatPlayerDto player, System.Action onClick)
    {
        if (senderNameText  != null) senderNameText.text  = player.playerName;
        if (lastMessageText != null) lastMessageText.text = player.latestMessageText;
        SetSelected(false);
        WireClick(onClick);
    }

    public void SetSelected(bool selected)
    {
        if (selectedBackground != null)
            selectedBackground.SetActive(selected);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    void WireClick(System.Action onClick)
    {
        _onClick = onClick;

        _button = GetComponent<UnityEngine.UI.Button>();
        if (_button == null) _button = gameObject.AddComponent<UnityEngine.UI.Button>();

        _button.onClick.RemoveAllListeners();
        _button.onClick.AddListener(() => _onClick?.Invoke());
    }
}
