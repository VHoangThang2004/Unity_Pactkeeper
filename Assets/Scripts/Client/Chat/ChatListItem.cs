using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Prefab script for conversation entries in the chat left panel.
/// </summary>
public class ChatListItem : MonoBehaviour
{
    [Header("UI Elements")]
    [SerializeField] private GameObject selectedBackground;
    [SerializeField] private TMP_Text   senderNameText;
    [SerializeField] private TMP_Text   lastMessageText;

    private System.Action _onClick;
    private Button _button;

    public string FriendPlayerId { get; private set; } = string.Empty;

    /// <summary>Sets up the list item as a friend conversation.</summary>
    public void SetupAsFriend(DirectChatConversationDto conv, System.Action onClick)
    {
        FriendPlayerId = conv.friendPlayerId;

        UpdateDisplay(
            conv.friendUsername,
            conv.isOnline,
            conv.status,
            conv.latestMessageText,
            conv.unreadCount
        );

        SetSelected(false);
        WireClick(onClick);
    }

    public void UpdateDisplay(string username, bool isOnline, string status, string latestMessage, int unreadCount)
    {
        if (senderNameText != null)
        {
            string statusTag = isOnline
                ? (status == "in_match" ? " <color=#FFCC00>[Battle]</color>" : " <color=#00FF66>[Online]</color>")
                : " <color=#888888>[Offline]</color>";

            string unreadTag = unreadCount > 0 ? $" <color=#FF4444>({unreadCount})</color>" : "";
            senderNameText.text = $"{username}{statusTag}{unreadTag}";
        }

        if (lastMessageText != null)
        {
            lastMessageText.text = string.IsNullOrEmpty(latestMessage)
                ? "No messages yet"
                : latestMessage;
        }
    }

    public void SetSelected(bool selected)
    {
        if (selectedBackground != null)
            selectedBackground.SetActive(selected);
    }

    void WireClick(System.Action onClick)
    {
        _onClick = onClick;

        _button = GetComponent<Button>();
        if (_button == null) _button = gameObject.AddComponent<Button>();

        _button.onClick.RemoveAllListeners();
        _button.onClick.AddListener(() => _onClick?.Invoke());
    }
}
