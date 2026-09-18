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
    [SerializeField] private Image backgroundImage;
    [SerializeField] private Sprite victoryBackground;
    [SerializeField] private Sprite defeatBackground;

    // Readable by UI scripts after OnResultLoaded fires
    public bool ResultLoaded { get; private set; }
    public bool IsWinner { get; private set; }
    public string WinnerId { get; private set; } = string.Empty;
    public int DurationSeconds { get; private set; }
    public int TotalInstants { get; private set; }

    void Start()
    {
        StartCoroutine(FetchResultLoop());
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
        if (resultText != null) resultText.text = "Failed to load match result.";
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
        ResultLoaded = true;

        Debug.Log($"[MatchResult] Loaded — winner={WinnerId} isWinner={IsWinner} instants={TotalInstants}");

        ApplyResultBackground(IsWinner);

        if (resultText != null)
        {
            bool isDraw = string.IsNullOrEmpty(data.result.winnerId);
            resultText.text = isDraw
                ? $"Draw.\n Instants: {TotalInstants}"
                : $" Instants: {TotalInstants}";
        }

        OnResultLoaded();
    }

    void ApplyResultBackground(bool isWinner)
    {
        if (backgroundImage == null) return;

        Sprite sprite = isWinner ? victoryBackground : defeatBackground;
        if (sprite == null) return;

        backgroundImage.sprite = sprite;
        backgroundImage.color = Color.white;
        backgroundImage.preserveAspect = false;
        backgroundImage.type = Image.Type.Simple;
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
    }
}
