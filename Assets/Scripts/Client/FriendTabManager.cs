using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

public class FriendTabManager : MonoBehaviour
{
    public static FriendTabManager Instance { get; private set; }

    [Header("Configuration")]
    [SerializeField] private BackendConfig config;

    [Header("Panel / Sidebar Drawer")]
    [SerializeField] private GameObject panel;
    [SerializeField] private Button openButton;
    [SerializeField] private Button closeButton;
    [SerializeField] private TMP_Text onlineCountText;

    [Header("Navigation Sub-Tabs")]
    [SerializeField] private Button friendsTabButton;
    [SerializeField] private Button requestsTabButton;
    [SerializeField] private GameObject friendsView;
    [SerializeField] private GameObject requestsView;

    [Header("Friend List UI")]
    [SerializeField] private Transform friendsContainer;
    [SerializeField] private GameObject friendItemPrefab;
    [SerializeField] private TMP_Text emptyStateText;

    [Header("Pending Requests UI")]
    [SerializeField] private Transform requestsContainer;
    [SerializeField] private GameObject requestItemPrefab;
    [SerializeField] private TMP_Text emptyRequestsText;
    [SerializeField] private GameObject requestsBadge;
    [SerializeField] private TMP_Text requestsBadgeCountText;

    [Header("Add Friend UI")]
    [SerializeField] private TMP_InputField addFriendInput;
    [SerializeField] private Button addFriendButton;
    [SerializeField] private TMP_Text addFriendStatusText;

    // Cache of friends: playerId -> FriendProfile
    private readonly Dictionary<string, FriendProfile> friendsCache = new();
    private readonly Dictionary<string, GameObject> friendItemViews = new();
    private readonly List<GameObject> requestItemViews = new();

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (openButton != null) openButton.onClick.AddListener(Open);
        if (closeButton != null) closeButton.onClick.AddListener(Close);
        if (addFriendButton != null) addFriendButton.onClick.AddListener(OnAddFriendClicked);

        if (friendsTabButton != null) friendsTabButton.onClick.AddListener(SwitchToFriendsTab);
        if (requestsTabButton != null) requestsTabButton.onClick.AddListener(SwitchToRequestsTab);

        HidePanel();
    }

    void Start()
    {
        if (LobbyHubClient.Instance != null)
        {
            LobbyHubClient.Instance.Connect();
            LobbyHubClient.Instance.OnFriendPresenceChanged += HandlePresenceChanged;
            LobbyHubClient.Instance.OnFriendRequestReceived += HandleNewFriendRequest;
        }

        SwitchToFriendsTab();

        // Fetch initial snapshot once on Main Menu start
        StartCoroutine(FetchFriendsList());
        StartCoroutine(FetchPendingRequestsCount());
    }

    void OnDestroy()
    {
        if (openButton != null) openButton.onClick.RemoveListener(Open);
        if (closeButton != null) closeButton.onClick.RemoveListener(Close);
        if (addFriendButton != null) addFriendButton.onClick.RemoveListener(OnAddFriendClicked);

        if (friendsTabButton != null) friendsTabButton.onClick.RemoveListener(SwitchToFriendsTab);
        if (requestsTabButton != null) requestsTabButton.onClick.RemoveListener(SwitchToRequestsTab);

        if (LobbyHubClient.Instance != null)
        {
            LobbyHubClient.Instance.OnFriendPresenceChanged -= HandlePresenceChanged;
            LobbyHubClient.Instance.OnFriendRequestReceived -= HandleNewFriendRequest;
        }

        if (Instance == this) Instance = null;
    }

    public void Open()
    {
        if (panel != null) panel.SetActive(true);
        SwitchToFriendsTab();
    }

    public void Close()
    {
        HidePanel();
    }

    public void Toggle()
    {
        if (panel != null)
            panel.SetActive(!panel.activeSelf);
    }

    private void HidePanel()
    {
        if (panel != null) panel.SetActive(false);
    }

    // -------------------------------------------------------
    // Sub-Tab Navigation
    // -------------------------------------------------------

    public void SwitchToFriendsTab()
    {
        if (friendsView != null) friendsView.SetActive(true);
        if (requestsView != null) requestsView.SetActive(false);
    }

    public void SwitchToRequestsTab()
    {
        if (friendsView != null) friendsView.SetActive(false);
        if (requestsView != null) requestsView.SetActive(true);
        StartCoroutine(FetchPendingRequestsList());
    }

    // -------------------------------------------------------
    // Initial Snapshot & Data Loading
    // -------------------------------------------------------

    public IEnumerator FetchFriendsList()
    {
        if (!PlayerSession.IsLoggedIn) yield break;

        using var request = UnityWebRequest.Get($"{config.backendUrl}/api/friends");
        config.SetHeaders(request, PlayerSession.Token);
        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError($"[FriendTabManager] Failed to fetch friends: {request.error}");
            yield break;
        }

        string json = $"{{\"friends\":{request.downloadHandler.text}}}";
        var wrapper = JsonUtility.FromJson<FriendsListWrapper>(json);

        friendsCache.Clear();
        if (wrapper?.friends != null)
        {
            foreach (var friend in wrapper.friends)
            {
                friendsCache[friend.playerId] = friend;
            }
        }

        RebuildFriendListView();
        UpdateOnlineCount();
    }

    private IEnumerator FetchPendingRequestsCount()
    {
        if (!PlayerSession.IsLoggedIn) yield break;

        using var request = UnityWebRequest.Get($"{config.backendUrl}/api/friends/requests");
        config.SetHeaders(request, PlayerSession.Token);
        yield return request.SendWebRequest();

        if (request.result == UnityWebRequest.Result.Success)
        {
            string json = $"{{\"requests\":{request.downloadHandler.text}}}";
            var wrapper = JsonUtility.FromJson<FriendRequestsWrapper>(json);
            int count = wrapper?.requests?.Length ?? 0;

            if (requestsBadge != null)
                requestsBadge.SetActive(count > 0);
            if (requestsBadgeCountText != null)
                requestsBadgeCountText.text = count.ToString();
        }
    }

    public IEnumerator FetchPendingRequestsList()
    {
        if (!PlayerSession.IsLoggedIn) yield break;

        using var request = UnityWebRequest.Get($"{config.backendUrl}/api/friends/requests");
        config.SetHeaders(request, PlayerSession.Token);
        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError($"[FriendTabManager] Failed to fetch pending requests: {request.error}");
            yield break;
        }

        string json = $"{{\"requests\":{request.downloadHandler.text}}}";
        var wrapper = JsonUtility.FromJson<FriendRequestsWrapper>(json);

        // Update badge
        int count = wrapper?.requests?.Length ?? 0;
        if (requestsBadge != null) requestsBadge.SetActive(count > 0);
        if (requestsBadgeCountText != null) requestsBadgeCountText.text = count.ToString();

        RebuildRequestsListView(wrapper?.requests ?? new FriendRequestItem[0]);
    }

    // -------------------------------------------------------
    // UI Rendering: Friends List
    // -------------------------------------------------------

    private void RebuildFriendListView()
    {
        if (friendsContainer == null) return;

        foreach (var view in friendItemViews.Values)
        {
            if (view != null) Destroy(view);
        }
        friendItemViews.Clear();

        if (friendsCache.Count == 0)
        {
            if (emptyStateText != null) emptyStateText.gameObject.SetActive(true);
            return;
        }

        if (emptyStateText != null) emptyStateText.gameObject.SetActive(false);

        foreach (var kvp in friendsCache)
        {
            CreateFriendItemView(kvp.Value);
        }
    }

    private void CreateFriendItemView(FriendProfile friend)
    {
        if (friendItemPrefab == null || friendsContainer == null) return;

        var itemObj = Instantiate(friendItemPrefab, friendsContainer);
        friendItemViews[friend.playerId] = itemObj;

        UpdateFriendItemView(itemObj, friend);
    }

    private void UpdateFriendItemView(GameObject itemObj, FriendProfile friend)
    {
        var nameText = itemObj.transform.Find("NameText")?.GetComponent<TMP_Text>();
        if (nameText != null) nameText.text = $"{friend.username} (Lv.{friend.level})";

        var statusDot = itemObj.transform.Find("StatusDot")?.GetComponent<Image>();
        var statusLabel = itemObj.transform.Find("StatusText")?.GetComponent<TMP_Text>();

        if (statusDot != null)
        {
            if (friend.status == "in_match")
                statusDot.color = Color.yellow;
            else if (friend.isOnline)
                statusDot.color = Color.green;
            else
                statusDot.color = Color.gray;
        }

        if (statusLabel != null)
        {
            if (friend.status == "in_match") statusLabel.text = "In Battle";
            else if (friend.isOnline) statusLabel.text = "Online";
            else statusLabel.text = "Offline";
        }

        var challengeBtn = itemObj.transform.Find("ChallengeButton")?.GetComponent<Button>();
        if (challengeBtn != null)
        {
            challengeBtn.onClick.RemoveAllListeners();
            challengeBtn.interactable = friend.isOnline && friend.status != "in_match";
            challengeBtn.onClick.AddListener(() => OnChallengeClicked(friend));
        }
    }

    private void UpdateOnlineCount()
    {
        int onlineCount = 0;
        foreach (var f in friendsCache.Values)
        {
            if (f.isOnline) onlineCount++;
        }

        if (onlineCountText != null)
            onlineCountText.text = $"Friends ({onlineCount}/{friendsCache.Count})";
    }

    // -------------------------------------------------------
    // UI Rendering: Pending Requests
    // -------------------------------------------------------

    private void RebuildRequestsListView(FriendRequestItem[] requests)
    {
        if (requestsContainer == null) return;

        foreach (var view in requestItemViews)
        {
            if (view != null) Destroy(view);
        }
        requestItemViews.Clear();

        if (requests == null || requests.Length == 0)
        {
            if (emptyRequestsText != null) emptyRequestsText.gameObject.SetActive(true);
            return;
        }

        if (emptyRequestsText != null) emptyRequestsText.gameObject.SetActive(false);

        foreach (var req in requests)
        {
            CreateRequestItemView(req);
        }
    }

    private void CreateRequestItemView(FriendRequestItem req)
    {
        if (requestItemPrefab == null || requestsContainer == null) return;

        var itemObj = Instantiate(requestItemPrefab, requestsContainer);
        requestItemViews.Add(itemObj);

        var nameText = itemObj.transform.Find("NameText")?.GetComponent<TMP_Text>();
        if (nameText != null) nameText.text = $"{req.senderUsername} (Lv.{req.senderLevel})";

        var acceptBtn = itemObj.transform.Find("AcceptButton")?.GetComponent<Button>();
        if (acceptBtn != null)
        {
            acceptBtn.onClick.RemoveAllListeners();
            acceptBtn.onClick.AddListener(() => StartCoroutine(AcceptFriendRequest(req.requestId)));
        }

        var declineBtn = itemObj.transform.Find("DeclineButton")?.GetComponent<Button>();
        if (declineBtn != null)
        {
            declineBtn.onClick.RemoveAllListeners();
            declineBtn.onClick.AddListener(() => StartCoroutine(DeclineFriendRequest(req.requestId)));
        }
    }

    // -------------------------------------------------------
    // Real-Time SignalR Event Handlers
    // -------------------------------------------------------

    private void HandlePresenceChanged(LobbyHubClient.FriendPresenceData presence)
    {
        if (friendsCache.TryGetValue(presence.playerId, out var friend))
        {
            friend.isOnline = presence.isOnline;
            friend.status = presence.status;

            if (friendItemViews.TryGetValue(presence.playerId, out var itemObj) && itemObj != null)
            {
                UpdateFriendItemView(itemObj, friend);
            }
            UpdateOnlineCount();
        }
    }

    private void HandleNewFriendRequest(string _)
    {
        StartCoroutine(FetchPendingRequestsCount());
        if (requestsView != null && requestsView.activeSelf)
        {
            StartCoroutine(FetchPendingRequestsList());
        }
    }

    // -------------------------------------------------------
    // Actions: Challenge, Accept, Decline, Add Friend
    // -------------------------------------------------------

    private void OnChallengeClicked(FriendProfile friend)
    {
        Debug.Log($"[FriendTabManager] Challenging friend: {friend.username} ({friend.playerId})");

        StartCoroutine(LobbyHubClient.Instance.SendMatchInvite(
            friend.playerId,
            inviteId =>
            {
                if (MatchInviteModal.Instance != null)
                {
                    MatchInviteModal.Instance.ShowOutgoingWaiting(inviteId, friend.username);
                }
            },
            error =>
            {
                Debug.LogError($"[FriendTabManager] Failed to send challenge: {error}");
            }
        ));
    }

    private IEnumerator AcceptFriendRequest(string requestId)
    {
        using var req = new UnityWebRequest($"{config.backendUrl}/api/friends/requests/{requestId}/accept", "POST");
        req.uploadHandler = new UploadHandlerRaw(new byte[0]);
        req.downloadHandler = new DownloadHandlerBuffer();
        config.SetHeaders(req, PlayerSession.Token);

        yield return req.SendWebRequest();

        if (req.result == UnityWebRequest.Result.Success)
        {
            Debug.Log("[FriendTabManager] Friend request accepted!");
            StartCoroutine(FetchFriendsList());
            StartCoroutine(FetchPendingRequestsList());
        }
        else
        {
            Debug.LogError($"[FriendTabManager] Accept failed: {req.error}");
        }
    }

    private IEnumerator DeclineFriendRequest(string requestId)
    {
        using var req = new UnityWebRequest($"{config.backendUrl}/api/friends/requests/{requestId}/decline", "POST");
        req.uploadHandler = new UploadHandlerRaw(new byte[0]);
        req.downloadHandler = new DownloadHandlerBuffer();
        config.SetHeaders(req, PlayerSession.Token);

        yield return req.SendWebRequest();

        if (req.result == UnityWebRequest.Result.Success)
        {
            Debug.Log("[FriendTabManager] Friend request declined.");
            StartCoroutine(FetchPendingRequestsList());
        }
        else
        {
            Debug.LogError($"[FriendTabManager] Decline failed: {req.error}");
        }
    }

    private void OnAddFriendClicked()
    {
        if (addFriendInput == null || string.IsNullOrWhiteSpace(addFriendInput.text)) return;
        string target = addFriendInput.text.Trim();

        StartCoroutine(SendFriendRequest(target));
    }

    private IEnumerator SendFriendRequest(string targetUsernameOrId)
    {
        if (addFriendStatusText != null)
            addFriendStatusText.text = "Sending request...";

        string json = $"{{\"targetUsernameOrId\":\"{targetUsernameOrId}\"}}";
        using var req = new UnityWebRequest($"{config.backendUrl}/api/friends/request", "POST");
        req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
        req.downloadHandler = new DownloadHandlerBuffer();
        config.SetHeaders(req, PlayerSession.Token);

        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            if (addFriendStatusText != null)
                addFriendStatusText.text = "Player not found or request failed.";
        }
        else
        {
            if (addFriendStatusText != null)
                addFriendStatusText.text = "Friend request sent!";
            if (addFriendInput != null) addFriendInput.text = string.Empty;
        }
    }

    // -------------------------------------------------------
    // DTOs
    // -------------------------------------------------------

    [Serializable]
    public class FriendProfile
    {
        public string playerId;
        public string username;
        public int level;
        public bool isOnline;
        public string status; // "idle", "in_match", "offline"
    }

    [Serializable]
    private class FriendsListWrapper
    {
        public FriendProfile[] friends;
    }

    [Serializable]
    private class FriendRequestsWrapper
    {
        public FriendRequestItem[] requests;
    }

    [Serializable]
    private class FriendRequestItem
    {
        public string requestId;
        public string senderPlayerId;
        public string senderUsername;
        public int senderLevel;
    }
}
