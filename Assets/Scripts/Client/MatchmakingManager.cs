using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
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
    [SerializeField] private MatchmakingTimerUI matchmakingTimer;
    [SerializeField] private TMP_Text player1Text;
    [SerializeField] private TMP_Text player2Text;
    [SerializeField] private TMP_Text vsText;
    [SerializeField] private RectTransform blueBanner;
    [SerializeField] private RectTransform redBanner;
    [SerializeField] private RectTransform vsImage;

    [Header("Random Units")]
    [SerializeField] private Image unitRandomBlue;
    [SerializeField] private Image unitRandomRed;
    [SerializeField] private Sprite[] unitRandomSprites;

    private bool isInQueue = false;
    private Coroutine pollingCoroutine;
    private Coroutine introRoutine;
    private Vector2 blueRest;
    private Vector2 redRest;
    private Vector3 vsRestScale = Vector3.one;
    private bool restsCached;

    void Awake()
    {
        CacheRests();
        if (matchFoundPanel != null)
            matchFoundPanel.SetActive(false);
    }

    public void OnClickFindMatch(string mode = "pvp")
    {
        if (!PlayerSession.IsLoggedIn)
        {
            statusText.text = Loc.Get(LocTables.MainMenu, LocKeys.Matchmaking.NotLoggedIn);
            return;
        }
        StartCoroutine(JoinQueue(mode));
    }

    public void OnClickFindRankedMatch()
    {
        OnClickFindMatch("ranked");
    }

    public void OnClickCancel()
    {
        StartCoroutine(LeaveQueue());
    }

    IEnumerator JoinQueue(string mode = "pvp")
    {
        statusText.text = Loc.Get(LocTables.MainMenu, LocKeys.Matchmaking.JoiningQueue);

        string bodyJson = $"{{\"mode\":\"{mode}\"}}";
        byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(bodyJson);

        using var request = new UnityWebRequest($"{config.backendUrl}/api/match/queue/join", "POST");
        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");
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
        matchmakingTimer?.StartTimer();

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
        matchmakingTimer?.StopTimer();
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
                    match => StartCoroutine(ShowMatchLoadingScreenAndLoad(match)),
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

    public IEnumerator ShowMatchLoadingScreenAndLoad(CurrentMatchResponse match)
    {
        if (match == null)
            yield break;
        yield return ShowMatchLoadingScreenAndLoad(match.player1Name, match.player2Name);
    }

    public IEnumerator ShowMatchLoadingScreenAndLoad(string p1Name, string p2Name)
    {
        Debug.Log($"[Matchmaking] ShowMatchLoadingScreenAndLoad: {p1Name} vs {p2Name}");
        isInQueue = false;
        if (pollingCoroutine != null) StopCoroutine(pollingCoroutine);

        findMatchButton.SetActive(false);
        cancelButton.SetActive(false);
        matchmakingTimer?.StopTimer();

        if (matchFoundPanel != null)
        {
            matchFoundPanel.SetActive(true);
            if (player1Text != null) player1Text.text = p1Name;
            if (player2Text != null) player2Text.text = p2Name;
            if (vsText != null) vsText.text = Loc.Get(LocTables.MainMenu, LocKeys.MainMenu.Vs);
            ResolveBanners();
            AssignRandomUnits();
            PlayIntro();
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

    void ResolveBanners()
    {
        if (matchFoundPanel == null)
            return;
        var root = matchFoundPanel.transform;
        if (blueBanner == null)
            blueBanner = root.Find("ImageForBlue") as RectTransform;
        if (redBanner == null)
            redBanner = root.Find("ImageForRed") as RectTransform;
        if (vsImage == null)
            vsImage = root.Find("VS_UI") as RectTransform;
        CacheRests();
    }

    void CacheRests()
    {
        if (restsCached)
            return;
        if (blueBanner != null)
            blueRest = blueBanner.anchoredPosition;
        if (redBanner != null)
            redRest = redBanner.anchoredPosition;
        if (vsImage != null)
            vsRestScale = vsImage.localScale;
        restsCached = blueBanner != null || redBanner != null || vsImage != null;
    }

    void PlayIntro()
    {
        if (introRoutine != null)
            StopCoroutine(introRoutine);
        introRoutine = StartCoroutine(IntroRoutine());
    }

    IEnumerator IntroRoutine()
    {
        const float duration = 0.6f;
        var blueStart = blueRest + Vector2.left * 1400f;
        var redStart = redRest + Vector2.right * 1400f;
        if (blueBanner != null)
            blueBanner.anchoredPosition = blueStart;
        if (redBanner != null)
            redBanner.anchoredPosition = redStart;
        if (vsImage != null)
        {
            vsImage.SetAsLastSibling();
            vsImage.localScale = vsRestScale * 0.05f;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float slide = EaseOutCubic(elapsed / duration);
            float drop = EaseOutBack(elapsed / duration);
            if (blueBanner != null)
                blueBanner.anchoredPosition = Vector2.LerpUnclamped(blueStart, blueRest, slide);
            if (redBanner != null)
                redBanner.anchoredPosition = Vector2.LerpUnclamped(redStart, redRest, slide);
            if (vsImage != null)
                vsImage.localScale = vsRestScale * Mathf.LerpUnclamped(0.05f, 1f, drop);
            yield return null;
        }

        if (blueBanner != null)
            blueBanner.anchoredPosition = blueRest;
        if (redBanner != null)
            redBanner.anchoredPosition = redRest;
        if (vsImage != null)
            vsImage.localScale = vsRestScale;
    }

    static float EaseOutCubic(float t)
    {
        t = Mathf.Clamp01(t);
        return 1f - Mathf.Pow(1f - t, 3f);
    }

    static float EaseOutBack(float t)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        t = Mathf.Clamp01(t);
        return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
    }

    void AssignRandomUnits()
    {
        if (matchFoundPanel != null)
        {
            var images = matchFoundPanel.GetComponentsInChildren<Image>(true);
            for (int i = 0; i < images.Length; i++)
            {
                if (unitRandomBlue == null && images[i].gameObject.name == "UnitRandomBlue")
                    unitRandomBlue = images[i];
                else if (unitRandomRed == null && images[i].gameObject.name == "UnitRandomRed")
                    unitRandomRed = images[i];
            }
        }

        var pool = new List<Sprite>();
        if (unitRandomSprites != null)
        {
            for (int i = 0; i < unitRandomSprites.Length; i++)
            {
                if (unitRandomSprites[i] != null)
                    pool.Add(unitRandomSprites[i]);
            }
        }

        Sprite blue = null;
        Sprite red = null;
        if (pool.Count > 0)
        {
            int blueIndex = Random.Range(0, pool.Count);
            blue = pool[blueIndex];
            pool.RemoveAt(blueIndex);
        }
        if (pool.Count > 0)
        {
            int redIndex = Random.Range(0, pool.Count);
            red = pool[redIndex];
        }

        ApplyUnitSprite(unitRandomBlue, blue);
        ApplyUnitSprite(unitRandomRed, red);
    }

    static void ApplyUnitSprite(Image image, Sprite sprite)
    {
        if (image == null || sprite == null)
            return;
        image.sprite = sprite;
        image.enabled = true;
        image.color = Color.white;
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