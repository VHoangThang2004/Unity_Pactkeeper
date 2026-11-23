using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Scene manager for 14_Chat (Friend Direct Messaging).
///
/// Features:
///   - Left Panel  : List of accepted friends with live presence, latest message previews, and unread counts.
///   - Right Panel : Full message history with selected friend, InputField, and Send button.
///   - Real-time   : Receives direct messages via LobbyHub (OnDirectMessageReceived).
/// </summary>
public class ChatManager : MonoBehaviour
{
    // ── Static Routing ────────────────────────────────────────────────────────
    public static string PendingTargetFriendId;
    public static string PendingTargetFriendUsername;

    // ── Inspector ─────────────────────────────────────────────────────────────

    [Header("Config")]
    [SerializeField] private BackendConfig config;
    [SerializeField] private SceneConfig   sceneConfig;

    [Header("Left Panel — Friend Conversation List")]
    [SerializeField] private Transform  listContainer;      // VerticalLayoutGroup content
    [SerializeField] private GameObject chatListItemPrefab;
    [SerializeField] private GameObject loadingIndicator;

    [Header("Right Panel — Message View")]
    [SerializeField] private Transform  messageContainer;   // VerticalLayoutGroup content (inside ScrollRect)
    [SerializeField] private ScrollRect messageScrollRect;
    [SerializeField] private GameObject chatMessageItemPrefab;
    [SerializeField] private TMP_InputField inputField;
    [SerializeField] private Button     sendButton;
    [SerializeField] private Button     backButton;
    [SerializeField] private GameObject emptyState;         // "Select a friend to chat"

    // ── Runtime State ─────────────────────────────────────────────────────────

    private string _selectedFriendId;
    private string _selectedFriendUsername;
    private ChatListItem _selectedListItem;

    private readonly Dictionary<string, ChatListItem> _listItemViews = new();
    private readonly Dictionary<string, DirectChatConversationDto> _conversationsCache = new();
    private readonly List<GameObject> _messageObjects = new();

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    void Awake()
    {
        if (sendButton != null) sendButton.onClick.AddListener(OnSendClicked);
        if (backButton != null) backButton.onClick.AddListener(OnBackClicked);

        // Allow Enter key to send
        if (inputField != null)
            inputField.onSubmit.AddListener(_ => OnSendClicked());

        if (emptyState != null) emptyState.SetActive(true);
    }

    void Start()
    {
        if (!PlayerSession.IsLoggedIn)
        {
            SceneManager.LoadScene(sceneConfig.mainMenuScene);
            return;
        }

        // Hook real-time events from LobbyHubClient
        if (LobbyHubClient.Instance != null)
        {
            LobbyHubClient.Instance.OnDirectMessageReceived += OnDirectMessageReceived;
            LobbyHubClient.Instance.OnFriendPresenceChanged += OnFriendPresenceChanged;
        }

        StartCoroutine(LoadConversations());
    }

    void OnDestroy()
    {
        if (sendButton != null) sendButton.onClick.RemoveListener(OnSendClicked);
        if (backButton != null) backButton.onClick.RemoveListener(OnBackClicked);

        if (LobbyHubClient.Instance != null)
        {
            LobbyHubClient.Instance.OnDirectMessageReceived -= OnDirectMessageReceived;
            LobbyHubClient.Instance.OnFriendPresenceChanged -= OnFriendPresenceChanged;
        }
    }

    // ── Left Panel: Conversations ─────────────────────────────────────────────

    IEnumerator LoadConversations()
    {
        if (loadingIndicator != null) loadingIndicator.SetActive(true);

        using var req = UnityWebRequest.Get($"{config.backendUrl}/api/chat/conversations");
        config.SetHeaders(req, PlayerSession.Token);
        yield return req.SendWebRequest();

        if (loadingIndicator != null) loadingIndicator.SetActive(false);

        if (req.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError($"[Chat] Failed to load conversations: {req.error}");
            if (emptyState != null) emptyState.SetActive(true);
            yield break;
        }

        ClearList();
        _conversationsCache.Clear();

        var wrapper = JsonUtility.FromJson<DirectChatConversationListWrapper>(
            "{\"items\":" + req.downloadHandler.text + "}");

        var convList = wrapper?.items != null
            ? new List<DirectChatConversationDto>(wrapper.items)
            : new List<DirectChatConversationDto>();

        foreach (var conv in convList)
        {
            _conversationsCache[conv.friendPlayerId] = conv;
            SpawnListItem(conv);
        }

        // Handle pre-selected target friend if opened from Friend sidebar
        if (!string.IsNullOrEmpty(PendingTargetFriendId))
        {
            string targetId = PendingTargetFriendId;
            string targetName = PendingTargetFriendUsername;
            PendingTargetFriendId = null;
            PendingTargetFriendUsername = null;

            if (_conversationsCache.TryGetValue(targetId, out var targetConv))
            {
                SelectConversation(targetConv);
            }
            else
            {
                // Create a temporary conversation entry for this friend
                var newConv = new DirectChatConversationDto
                {
                    friendPlayerId = targetId,
                    friendUsername = !string.IsNullOrEmpty(targetName) ? targetName : "Friend",
                    friendLevel = 1,
                    isOnline = true,
                    status = "idle",
                    latestMessageText = "",
                    latestMessageTime = "",
                    unreadCount = 0
                };
                _conversationsCache[targetId] = newConv;
                SpawnListItem(newConv);
                SelectConversation(newConv);
            }
        }
        else if (convList.Count > 0)
        {
            // Auto-select the first conversation
            SelectConversation(convList[0]);
        }
        else
        {
            if (emptyState != null) emptyState.SetActive(true);
        }
    }

    void SpawnListItem(DirectChatConversationDto conv)
    {
        if (chatListItemPrefab == null || listContainer == null) return;

        var obj = Instantiate(chatListItemPrefab, listContainer);
        var item = obj.GetComponent<ChatListItem>();
        if (item == null) return;

        var capturedConv = conv;
        item.SetupAsFriend(capturedConv, () => SelectConversation(capturedConv));
        _listItemViews[conv.friendPlayerId] = item;
    }

    void SelectConversation(DirectChatConversationDto conv)
    {
        _selectedFriendId = conv.friendPlayerId;
        _selectedFriendUsername = conv.friendUsername;

        if (emptyState != null) emptyState.SetActive(false);

        // Update list item selection visual
        if (_selectedListItem != null)
            _selectedListItem.SetSelected(false);

        if (_listItemViews.TryGetValue(conv.friendPlayerId, out var itemView))
        {
            _selectedListItem = itemView;
            _selectedListItem.SetSelected(true);

            // Clear unread indicator locally
            conv.unreadCount = 0;
            itemView.UpdateDisplay(conv.friendUsername, conv.isOnline, conv.status, conv.latestMessageText, 0);
        }

        StartCoroutine(FetchMessages(conv.friendPlayerId));
        StartCoroutine(MarkConversationAsRead(conv.friendPlayerId));
    }

    void ClearList()
    {
        _listItemViews.Clear();
        if (listContainer == null) return;
        foreach (Transform child in listContainer)
            Destroy(child.gameObject);
    }

    // ── Right Panel: Messages ─────────────────────────────────────────────────

    IEnumerator FetchMessages(string friendPlayerId)
    {
        using var req = UnityWebRequest.Get($"{config.backendUrl}/api/chat/messages/{friendPlayerId}");
        config.SetHeaders(req, PlayerSession.Token);
        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError($"[Chat] Failed to fetch messages: {req.error}");
            yield break;
        }

        var wrapper = JsonUtility.FromJson<DirectChatMessageListWrapper>(
            "{\"items\":" + req.downloadHandler.text + "}");

        RenderMessages(wrapper?.items ?? Array.Empty<DirectChatMessageDto>());
    }

    void RenderMessages(DirectChatMessageDto[] messages)
    {
        ClearMessages();

        foreach (var msg in messages)
            SpawnMessageBubble(msg);

        ScrollToBottom();
    }

    void SpawnMessageBubble(DirectChatMessageDto msg)
    {
        if (chatMessageItemPrefab == null || messageContainer == null) return;

        var obj  = Instantiate(chatMessageItemPrefab, messageContainer);
        var item = obj.GetComponent<ChatMessageItem>();
        item?.Setup(msg);
        _messageObjects.Add(obj);
    }

    void ClearMessages()
    {
        foreach (var obj in _messageObjects)
            if (obj != null) Destroy(obj);
        _messageObjects.Clear();
    }

    void ScrollToBottom()
    {
        StartCoroutine(ScrollToBottomNextFrame());
    }

    IEnumerator ScrollToBottomNextFrame()
    {
        yield return null; // Wait 1 frame for layout rebuild
        if (messageScrollRect != null)
            messageScrollRect.verticalNormalizedPosition = 0f;
    }

    // ── Send Message ──────────────────────────────────────────────────────────

    void OnSendClicked()
    {
        if (inputField == null) return;

        string text = inputField.text.Trim();
        if (string.IsNullOrEmpty(text)) return;
        if (string.IsNullOrEmpty(_selectedFriendId)) return;

        inputField.text = string.Empty;
        inputField.ActivateInputField();

        StartCoroutine(SendMessageCoroutine(_selectedFriendId, text));
    }

    IEnumerator SendMessageCoroutine(string targetPlayerId, string text)
    {
        if (sendButton != null) sendButton.interactable = false;

        string url = $"{config.backendUrl}/api/chat/messages";
        string json = JsonUtility.ToJson(new DirectChatSendRequest
        {
            receiverId = targetPlayerId,
            text = text
        });

        using var req = UnityWebRequest.PostWwwForm(url, "");
        var bodyBytes = System.Text.Encoding.UTF8.GetBytes(json);
        req.uploadHandler = new UploadHandlerRaw(bodyBytes);
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");
        config.SetHeaders(req, PlayerSession.Token);

        yield return req.SendWebRequest();

        if (sendButton != null) sendButton.interactable = true;

        if (req.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError($"[Chat] SendMessage failed: {req.error}\n{req.downloadHandler.text}");
            yield break;
        }

        var sentMsg = JsonUtility.FromJson<DirectChatMessageDto>(req.downloadHandler.text);
        if (sentMsg != null)
        {
            SpawnMessageBubble(sentMsg);
            ScrollToBottom();

            // Update left panel preview
            if (_conversationsCache.TryGetValue(targetPlayerId, out var conv))
            {
                conv.latestMessageText = sentMsg.text;
                conv.latestMessageTime = sentMsg.createdAt;
                if (_listItemViews.TryGetValue(targetPlayerId, out var itemView))
                {
                    itemView.UpdateDisplay(conv.friendUsername, conv.isOnline, conv.status, sentMsg.text, conv.unreadCount);
                    itemView.transform.SetAsFirstSibling();
                }
            }
        }
    }

    // ── Real-Time SignalR Event Handlers ──────────────────────────────────────

    void OnDirectMessageReceived(DirectChatMessageDto msg)
    {
        if (msg == null) return;

        // If message is from currently open conversation
        if (msg.senderId == _selectedFriendId)
        {
            SpawnMessageBubble(msg);
            ScrollToBottom();

            // Mark read immediately
            StartCoroutine(MarkConversationAsRead(msg.senderId));

            if (_conversationsCache.TryGetValue(msg.senderId, out var conv))
            {
                conv.latestMessageText = msg.text;
                conv.latestMessageTime = msg.createdAt;
                if (_listItemViews.TryGetValue(msg.senderId, out var itemView))
                {
                    itemView.UpdateDisplay(conv.friendUsername, conv.isOnline, conv.status, msg.text, 0);
                    itemView.transform.SetAsFirstSibling();
                }
            }
        }
        else
        {
            // Message from another friend -> increment unread count & update preview
            if (_conversationsCache.TryGetValue(msg.senderId, out var conv))
            {
                conv.unreadCount++;
                conv.latestMessageText = msg.text;
                conv.latestMessageTime = msg.createdAt;

                if (_listItemViews.TryGetValue(msg.senderId, out var itemView))
                {
                    itemView.UpdateDisplay(conv.friendUsername, conv.isOnline, conv.status, msg.text, conv.unreadCount);
                    itemView.transform.SetAsFirstSibling();
                }
            }
            else
            {
                // New conversation from a friend not yet in the list -> reload conversations
                StartCoroutine(LoadConversations());
            }
        }
    }

    void OnFriendPresenceChanged(LobbyHubClient.FriendPresenceData pres)
    {
        if (pres == null) return;

        if (_conversationsCache.TryGetValue(pres.playerId, out var conv))
        {
            conv.isOnline = pres.isOnline;
            conv.status = pres.status;

            if (_listItemViews.TryGetValue(pres.playerId, out var itemView))
            {
                itemView.UpdateDisplay(conv.friendUsername, conv.isOnline, conv.status, conv.latestMessageText, conv.unreadCount);
            }
        }
    }

    IEnumerator MarkConversationAsRead(string friendPlayerId)
    {
        using var req = UnityWebRequest.PostWwwForm($"{config.backendUrl}/api/chat/messages/{friendPlayerId}/read", "");
        config.SetHeaders(req, PlayerSession.Token);
        yield return req.SendWebRequest();
    }

    void OnBackClicked()
    {
        SceneManager.LoadScene(sceneConfig.mainMenuScene);
    }
}
