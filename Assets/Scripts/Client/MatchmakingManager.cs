using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using TMPro;

public class MatchmakingManager : MonoBehaviour
{
    [Header("Config")]
    [SerializeField] private BackendConfig config;
    [SerializeField] private SceneConfig sceneConfig;

    [Header("UI")]
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private GameObject findMatchButton;
    [SerializeField] private GameObject cancelButton;
    [SerializeField] private GameObject matchFoundPanel;
    [SerializeField] private TMP_Text player1Text;
    [SerializeField] private TMP_Text player2Text;
    [SerializeField] private TMP_Text vsText;

    private bool isInQueue = false;
    private Coroutine pollingCoroutine;

    public void OnClickFindMatch()
    {
        if (!PlayerSession.IsLoggedIn)
        {
            statusText.text = Loc.Get(LocTables.MainMenu, LocKeys.Matchmaking.NotLoggedIn);
            return;
        }
        StartCoroutine(JoinQueue());
    }

    public void OnClickCancel()
    {
        StartCoroutine(LeaveQueue());
    }

    IEnumerator JoinQueue()
    {
        statusText.text = Loc.Get(LocTables.MainMenu, LocKeys.Matchmaking.JoiningQueue);

        using var request = new UnityWebRequest($"{config.backendUrl}/api/match/queue/join", "POST");
        request.uploadHandler = new UploadHandlerRaw(new byte[0]);
        request.downloadHandler = new DownloadHandlerBuffer();
        config.SetHeaders(request, PlayerSession.Token);

        yield return request.SendWebRequest();

        if (request.responseCode == 409)
        {
            statusText.text = Loc.Get(LocTables.MainMenu, LocKeys.Matchmaking.AlreadyInMatch);
            yield break;
        }

        if (request.result != UnityWebRequest.Result.Success)
        {
            statusText.text = Loc.Get(LocTables.MainMenu, LocKeys.Matchmaking.FailedJoinQueue);
            Debug.LogError($"[Matchmaking] {request.error}");
            yield break;
        }

        isInQueue = true;
        findMatchButton.SetActive(false);
        cancelButton.SetActive(true);
        statusText.text = Loc.Get(LocTables.MainMenu, LocKeys.Matchmaking.FindingMatch);

        pollingCoroutine = StartCoroutine(PollQueueStatus());
    }

    IEnumerator LeaveQueue()
    {
        using var request = new UnityWebRequest($"{config.backendUrl}/api/match/queue/leave", "DELETE");
        request.downloadHandler = new DownloadHandlerBuffer();
        config.SetHeaders(request, PlayerSession.Token);

        yield return request.SendWebRequest();

        isInQueue = false;
        if (pollingCoroutine != null) StopCoroutine(pollingCoroutine);
        findMatchButton.SetActive(true);
        cancelButton.SetActive(false);
        statusText.text = Loc.Get(LocTables.MainMenu, LocKeys.Matchmaking.Cancelled);
    }

    IEnumerator PollQueueStatus()
    {
        while (isInQueue)
        {
            yield return new WaitForSeconds(3f);

            using var request = UnityWebRequest.Get($"{config.backendUrl}/api/match/queue/status");
            config.SetHeaders(request, PlayerSession.Token);
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[Matchmaking] Poll failed: {request.error}");
                continue;
            }

            var response = JsonUtility.FromJson<QueueStatusResponse>(request.downloadHandler.text);
            Debug.Log($"[Matchmaking] Queue status: {response.status}");

            if (response.status == "matched")
            {
                isInQueue = false;
                if (pollingCoroutine != null) StopCoroutine(pollingCoroutine);

                yield return StartCoroutine(FetchCurrentMatch(
                    match => StartCoroutine(ShowMatchLoadingScreenAndLoad(match.player1Name, match.player2Name)),
                    () => Debug.LogError("[Matchmaking] Matched but GET /match/current returned nothing.")
                ));
            }
        }
    }

    // -------------------------------------------------------
    // Public: fetch GET /api/match/current
    // Saves matchId/serverIp/serverPort to PlayerSession on success.
    // -------------------------------------------------------

    public IEnumerator FetchCurrentMatch(
        System.Action<CurrentMatchResponse> onFound,
        System.Action onNotFound = null)
    {
        using var request = UnityWebRequest.Get($"{config.backendUrl}/api/match/current");
        config.SetHeaders(request, PlayerSession.Token);
        yield return request.SendWebRequest();

        if (request.responseCode == 404 || request.result != UnityWebRequest.Result.Success)
        {
            PlayerSession.MatchId = string.Empty;
            PlayerSession.ServerIp = string.Empty;
            PlayerSession.ServerPort = 7777;
            onNotFound?.Invoke();
            yield break;
        }

        var match = JsonUtility.FromJson<CurrentMatchResponse>(request.downloadHandler.text);
        PlayerSession.MatchId = match.matchId;
        PlayerSession.ServerIp = match.serverIp;
        PlayerSession.ServerPort = match.serverPort;
        Debug.Log($"[Matchmaking] Ongoing match found: {match.matchId} mode={match.mode}");
        onFound?.Invoke(match);
    }

    // -------------------------------------------------------
    // Public: show VS screen then load PvP match scene
    // -------------------------------------------------------

    public IEnumerator ShowMatchLoadingScreenAndLoad(string p1Name, string p2Name)
    {
        Debug.Log($"[Matchmaking] ShowMatchLoadingScreenAndLoad: {p1Name} vs {p2Name}");
        isInQueue = false;
        if (pollingCoroutine != null) StopCoroutine(pollingCoroutine);

        findMatchButton.SetActive(false);
        cancelButton.SetActive(false);

        if (matchFoundPanel != null)
        {
            matchFoundPanel.SetActive(true);
            if (player1Text != null) player1Text.text = p1Name;
            if (player2Text != null) player2Text.text = p2Name;
            if (vsText != null) vsText.text = Loc.Get(LocTables.MainMenu, LocKeys.MainMenu.Vs);
        }
        else
        {
            statusText.text = Loc.Format(
                LocTables.MainMenu,
                LocKeys.Matchmaking.MatchFoundFormat,
                p1Name,
                Loc.Get(LocTables.MainMenu, LocKeys.MainMenu.Vs),
                p2Name,
                Loc.Get(LocTables.MainMenu, LocKeys.Matchmaking.LoadingGame));
        }

        yield return new WaitForSeconds(4f);

        UnityEngine.SceneManagement.SceneManager.LoadScene(sceneConfig.clientMatch);
    }

    // -------------------------------------------------------
    // DTOs
    // -------------------------------------------------------

    [System.Serializable]
    private class QueueStatusResponse
    {
        public string status;
    }

    [System.Serializable]
    public class CurrentMatchResponse
    {
        public string matchId;
        public string serverIp;
        public int serverPort;
        public string mode;
        public string player1Name;
        public string player2Name;
    }
}