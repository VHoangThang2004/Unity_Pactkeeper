using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

public class MatchResultManager : MonoBehaviour
{
    [Header("Config")]
    [SerializeField] private BackendConfig config;
    [SerializeField] private SceneConfig sceneConfig;
    [SerializeField] private float retryInterval = 2f;
    [SerializeField] private int maxRetries = 10;

    [Header("UI")]
    public TMP_Text resultText;
    [SerializeField] private RectTransform resultContentPanel;
    [SerializeField] private Image winLoseImage;
    [SerializeField] private Sprite victoryBackground;
    [SerializeField] private Sprite defeatBackground;
    [SerializeField] private float slideDistance = 700f;
    [SerializeField] private float slideDuration = 0.45f;

    // Readable by UI scripts after OnResultLoaded fires
    public bool ResultLoaded { get; private set; }
    public bool IsWinner { get; private set; }
    public string WinnerId { get; private set; } = string.Empty;
    public int DurationSeconds { get; private set; }
    public int TotalInstants { get; private set; }
    public bool IsForfeit { get; private set; }
    public string ForfeitPlayerId { get; private set; } = string.Empty;

    void Start()
    {
        StartCoroutine(PlayEntrance());
        StartCoroutine(FetchResultLoop());
    }

    IEnumerator PlayEntrance()
    {
        Vector2 panelRest = resultContentPanel != null ? resultContentPanel.anchoredPosition : Vector2.zero;
        Vector2 imageRest = winLoseImage != null ? winLoseImage.rectTransform.anchoredPosition : Vector2.zero;
        float elapsed = 0f;

        while (elapsed < slideDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float remain = 1f - Mathf.SmoothStep(0f, 1f, elapsed / slideDuration);
            if (resultContentPanel != null)
                resultContentPanel.anchoredPosition = panelRest + new Vector2(0f, -slideDistance * remain);
            if (winLoseImage != null)
            {
                float panelShift = resultContentPanel != null ? -slideDistance * remain : 0f;
                float imageShift = slideDistance * remain;
                winLoseImage.rectTransform.anchoredPosition = imageRest + new Vector2(0f, imageShift - panelShift);
            }
            yield return null;
        }

        if (resultContentPanel != null)
            resultContentPanel.anchoredPosition = panelRest;
        if (winLoseImage != null)
            winLoseImage.rectTransform.anchoredPosition = imageRest;
    }

    IEnumerator FetchResultLoop()
    {
        string matchId = PlayerSession.MatchId;

        if (string.IsNullOrEmpty(matchId))
        {
            Debug.LogWarning("[MatchResult] No matchId in session.");
            yield break;
        }

        for (int attempt = 0; attempt < maxRetries; attempt++)
        {
            yield return StartCoroutine(TryFetchResult(matchId));
            if (ResultLoaded) yield break;
            yield return new WaitForSeconds(retryInterval);
        }

        Debug.LogError("[MatchResult] Failed to fetch result after max retries.");
        if (resultText != null) resultText.text = Loc.Get(LocTables.MatchResult, LocKeys.MatchResult.LoadFailed);
    }

    IEnumerator TryFetchResult(string matchId)
    {
        using var request = UnityWebRequest.Get($"{config.backendUrl}/api/match/history/{matchId}");
        config.SetHeaders(request, PlayerSession.Token);
        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            Debug.Log($"[MatchResult] Not ready yet ({request.responseCode}) — retrying...");
            yield break;
        }

        var data = JsonUtility.FromJson<MatchHistoryResponse>(request.downloadHandler.text);
        if (data == null || data.status != "completed" || data.result == null)
        {
            Debug.Log("[MatchResult] Result not yet available — retrying...");
            yield break;
        }

        WinnerId = data.result.winnerId;
        IsWinner = (data.result.winnerId == PlayerSession.PlayerId);
        DurationSeconds = data.result.durationSeconds;
        TotalInstants = data.result.totalInstants;
        IsForfeit = data.result.isForfeit;
        ForfeitPlayerId = data.result.forfeitPlayerId;
        ResultLoaded = true;

        Debug.Log($"[MatchResult] Loaded — " +
                  $"winnerId={WinnerId} | " +
                  $"myPlayerId={PlayerSession.PlayerId} | " +
                  $"isWinner={IsWinner} | " +
                  $"isForfeit={IsForfeit} | " +
                  $"forfeitPlayerId={ForfeitPlayerId} | " +
                  $"instants={TotalInstants}");

        // Safety check: nếu PlayerId trống → không xác định được thắng/thua
        if (string.IsNullOrEmpty(PlayerSession.PlayerId))
            Debug.LogError("[MatchResult] PlayerSession.PlayerId is EMPTY — IsWinner will be incorrect!");

        bool iForfeited = IsForfeit && (
            (!string.IsNullOrEmpty(ForfeitPlayerId) && ForfeitPlayerId == PlayerSession.PlayerId)
            || (string.IsNullOrEmpty(ForfeitPlayerId) && !IsWinner));
        bool showVictory = IsForfeit ? !iForfeited : IsWinner;
        ApplyWinLoseImage(showVictory);

        if (resultText != null)
        {
            bool isDraw = string.IsNullOrEmpty(data.result.winnerId);
            if (isDraw)
            {
                resultText.text = Loc.Format(LocTables.MatchResult, LocKeys.MatchResult.DrawFormat, TotalInstants);
            }
            else if (IsForfeit)
            {
                resultText.text = iForfeited
                    ? $"Defeat\nYou forfeited ({TotalInstants} instants)"
                    : $"Victory\nOpponent forfeited ({TotalInstants} instants)";
            }
            else
            {
                resultText.text = Loc.Format(LocTables.MatchResult, LocKeys.MatchResult.InstantsFormat, TotalInstants);
            }
        }

        OnResultLoaded();
    }

    void ApplyWinLoseImage(bool isWinner)
    {
        if (winLoseImage == null) return;

        Sprite sprite = isWinner ? victoryBackground : defeatBackground;
        if (sprite == null) return;

        winLoseImage.sprite = sprite;
        winLoseImage.color = Color.white;
        winLoseImage.preserveAspect = false;
        winLoseImage.type = Image.Type.Simple;
    }

    protected virtual void OnResultLoaded() { }

    public void OnReturnMainMenuClick()
    {
        SceneManager.LoadScene(sceneConfig.mainMenuScene);
    }

    // -------------------------------------------------------
    // Response types
    // -------------------------------------------------------

    [System.Serializable]
    private class MatchHistoryResponse
    {
        public string matchId;
        public string player1Id;
        public string player2Id;
        public string status;
        public MatchResultResponse result;
        public string completedAt;
    }

    [System.Serializable]
    private class MatchResultResponse
    {
        public string winnerId;
        public int durationSeconds;
        public int totalInstants;
        public bool isForfeit;
        public string forfeitPlayerId;
    }
}
