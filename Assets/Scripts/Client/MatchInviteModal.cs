using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MatchInviteModal : MonoBehaviour
{
    public static MatchInviteModal Instance { get; private set; }

    [Header("Dependencies")]
    [SerializeField] private MatchmakingManager matchmakingManager;
    [SerializeField] private SceneConfig sceneConfig;

    [Header("Incoming Invite Modal")]
    [SerializeField] private GameObject incomingModal;
    [SerializeField] private TMP_Text incomingPromptText;
    [SerializeField] private TMP_Text incomingCountdownText;
    [SerializeField] private Image incomingCountdownFill;
    [SerializeField] private Button acceptButton;
    [SerializeField] private Button declineButton;

    [Header("Outgoing Invite Modal")]
    [SerializeField] private GameObject outgoingModal;
    [SerializeField] private TMP_Text outgoingStatusText;
    [SerializeField] private Button cancelButton;

    private string currentIncomingInviteId;
    private string currentOutgoingInviteId;
    private Coroutine countdownCoroutine;
    private bool isSubscribed = false;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(transform.root.gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(transform.root.gameObject);
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;

        if (acceptButton != null) acceptButton.onClick.AddListener(OnAcceptClicked);
        if (declineButton != null) declineButton.onClick.AddListener(OnDeclineClicked);
        if (cancelButton != null) cancelButton.onClick.AddListener(OnCancelClicked);

        HideAll();
    }

    private void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        // When returning to login scene or logged out, clean up persistent canvas
        if (scene.name == "1_Login" || !PlayerSession.IsLoggedIn)
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
            UnsubscribeEvents();
            if (Instance == this) Instance = null;
            Destroy(transform.root.gameObject);
        }
    }

    void Start()
    {
        StartCoroutine(WaitForHubAndSubscribe());
    }

    private IEnumerator WaitForHubAndSubscribe()
    {
        while (LobbyHubClient.Instance == null)
            yield return null;

        SubscribeEvents();
    }

    private void SubscribeEvents()
    {
        if (isSubscribed || LobbyHubClient.Instance == null) return;
        LobbyHubClient.Instance.OnMatchInviteReceived += HandleIncomingInvite;
        LobbyHubClient.Instance.OnMatchReady += HandleMatchReady;
        LobbyHubClient.Instance.OnMatchInviteDeclined += HandleInviteDeclined;
        LobbyHubClient.Instance.OnMatchInviteCancelled += HandleInviteCancelled;
        isSubscribed = true;
    }

    private void UnsubscribeEvents()
    {
        if (!isSubscribed || LobbyHubClient.Instance == null) return;
        LobbyHubClient.Instance.OnMatchInviteReceived -= HandleIncomingInvite;
        LobbyHubClient.Instance.OnMatchReady -= HandleMatchReady;
        LobbyHubClient.Instance.OnMatchInviteDeclined -= HandleInviteDeclined;
        LobbyHubClient.Instance.OnMatchInviteCancelled -= HandleInviteCancelled;
        isSubscribed = false;
    }

    void OnDestroy()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
        UnsubscribeEvents();
        if (Instance == this) Instance = null;
    }

    public void ShowOutgoingWaiting(string inviteId, string friendUsername)
    {
        currentOutgoingInviteId = inviteId;
        if (outgoingStatusText != null)
            outgoingStatusText.text = $"Waiting for {friendUsername} to accept challenge...";

        if (outgoingModal != null) outgoingModal.SetActive(true);
        if (countdownCoroutine != null) StopCoroutine(countdownCoroutine);
        countdownCoroutine = StartCoroutine(OutgoingTimeoutCountdown(30f));
    }

    private void HandleIncomingInvite(LobbyHubClient.MatchInviteData invite)
    {
        // Suppress invite popups if currently inside an active match scene
        string activeSceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        if (activeSceneName == "5_ClientMatch" || !string.IsNullOrEmpty(PlayerSession.MatchId))
        {
            if (LobbyHubClient.Instance != null)
                StartCoroutine(LobbyHubClient.Instance.DeclineMatchInvite(invite.inviteId));
            return;
        }

        currentIncomingInviteId = invite.inviteId;
        if (incomingPromptText != null)
            incomingPromptText.text = $"{invite.senderUsername} has challenged you to a friendly match!";

        if (incomingModal != null) incomingModal.SetActive(true);

        if (countdownCoroutine != null) StopCoroutine(countdownCoroutine);
        countdownCoroutine = StartCoroutine(IncomingCountdown(invite.expiresInSeconds > 0 ? invite.expiresInSeconds : 30));
    }

    private IEnumerator IncomingCountdown(int totalSeconds)
    {
        float remaining = totalSeconds;
        while (remaining > 0)
        {
            if (incomingCountdownText != null)
                incomingCountdownText.text = $"Expires in {Mathf.CeilToInt(remaining)}s";
            if (incomingCountdownFill != null)
                incomingCountdownFill.fillAmount = remaining / totalSeconds;

            yield return new WaitForSeconds(0.1f);
            remaining -= 0.1f;
        }

        // Expired
        HideIncoming();
    }

    private IEnumerator OutgoingTimeoutCountdown(float duration)
    {
        yield return new WaitForSeconds(duration);
        HideOutgoing();
    }

    private void OnAcceptClicked()
    {
        if (string.IsNullOrEmpty(currentIncomingInviteId)) return;

        if (incomingPromptText != null)
            incomingPromptText.text = "Accepting match...";

        StartCoroutine(LobbyHubClient.Instance.AcceptMatchInvite(
            currentIncomingInviteId,
            matchReady =>
            {
                HideIncoming();
                HandleMatchReady(matchReady);
            },
            error =>
            {
                Debug.LogError($"[MatchInviteModal] Accept failed: {error}");
                HideIncoming();
            }
        ));
    }

    private void OnDeclineClicked()
    {
        if (!string.IsNullOrEmpty(currentIncomingInviteId))
        {
            StartCoroutine(LobbyHubClient.Instance.DeclineMatchInvite(currentIncomingInviteId));
        }
        HideIncoming();
    }

    private void OnCancelClicked()
    {
        if (!string.IsNullOrEmpty(currentOutgoingInviteId))
        {
            StartCoroutine(LobbyHubClient.Instance.CancelMatchInvite(currentOutgoingInviteId));
        }
        HideOutgoing();
    }

    private void HandleInviteDeclined(string _)
    {
        if (outgoingStatusText != null)
            outgoingStatusText.text = "Friend declined the challenge.";
        StartCoroutine(AutoCloseOutgoingAfterDelay(2f));
    }

    private void HandleInviteCancelled(string _)
    {
        HideIncoming();
    }

    private IEnumerator AutoCloseOutgoingAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        HideOutgoing();
    }

    private void HandleMatchReady(LobbyHubClient.MatchReadyData match)
    {
        Debug.Log($"[MatchInviteModal] Friendly match ready! {match.matchId} at {match.serverIp}:{match.serverPort}");
        HideAll();

        PlayerSession.MatchId = match.matchId;
        PlayerSession.ServerIp = match.serverIp;
        PlayerSession.ServerPort = match.serverPort;

        if (matchmakingManager == null)
            matchmakingManager = FindAnyObjectByType<MatchmakingManager>();

        if (matchmakingManager != null)
        {
            StartCoroutine(matchmakingManager.ShowMatchLoadingScreenAndLoad(match.player1Name, match.player2Name));
        }
        else
        {
            StartCoroutine(DirectLoadMatch(match));
        }
    }

    private IEnumerator DirectLoadMatch(LobbyHubClient.MatchReadyData match)
    {
        yield return new WaitForSeconds(1.5f);
        string targetScene = sceneConfig != null && !string.IsNullOrEmpty(sceneConfig.clientMatch)
            ? sceneConfig.clientMatch
            : "5_ClientMatch";
        UnityEngine.SceneManagement.SceneManager.LoadScene(targetScene);
    }

    private void HideIncoming()
    {
        if (countdownCoroutine != null) StopCoroutine(countdownCoroutine);
        if (incomingModal != null) incomingModal.SetActive(false);
        currentIncomingInviteId = null;
    }

    private void HideOutgoing()
    {
        if (countdownCoroutine != null) StopCoroutine(countdownCoroutine);
        if (outgoingModal != null) outgoingModal.SetActive(false);
        currentOutgoingInviteId = null;
    }

    private void HideAll()
    {
        HideIncoming();
        HideOutgoing();
    }
}
