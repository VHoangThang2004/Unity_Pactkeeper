using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

[System.Serializable]
public class MatchLoadoutsResponse
{
    public string player1Id;
    public string player2Id;
    public TeamLoadoutData player1Loadout;
    public TeamLoadoutData player2Loadout;
}

[System.Serializable]
public class MatchInfoResponse
{
    public string matchId;
    public string mode;
    public string storyChapterId;
}

[System.Serializable]
public class TeamLoadoutData
{
    public string playerId;
    public UnitLoadoutData[] units;
}

[System.Serializable]
public class UnitLoadoutData
{
    public int uId;
    public int movementSkillId;
    public int weaponSkillId;
    public int classSkillId;
    public int equipmentSkillId;
}

public class ServerBackendClient : MonoBehaviour
{
    [Header("Config")]
    [SerializeField] private BackendConfig config;

    public string MatchId { get; private set; } = string.Empty;
    public string Mode { get; private set; } = "pvp";
    public string StoryChapterId { get; private set; } = string.Empty;
    public bool IsReady { get; private set; } = false;
    public MatchLoadoutsResponse LoadoutResponse { get; private set; }

    // -------------------------------------------------------
    // Init
    // -------------------------------------------------------

    void Awake()
    {
        DontDestroyOnLoad(gameObject);
    }

    void Start()
    {
        StartCoroutine(FetchMatchData());
    }

    // -------------------------------------------------------
    // Fetch
    // -------------------------------------------------------

    IEnumerator FetchMatchData()
    {
        MatchId = GetMatchIdFromArgs();

        if (string.IsNullOrEmpty(MatchId))
        {
            Debug.Log("[ServerBackendClient] No matchId arg — using hardcoded loadouts.");
            IsReady = true;
            yield break;
        }

        Debug.Log($"[ServerBackendClient] MatchId={MatchId} — fetching match info.");
        yield return StartCoroutine(FetchMatchInfo());

        if (Mode == "pvp")
            yield return StartCoroutine(FetchLoadouts());
        else if (Mode == "story")
        {
            Debug.Log($"[ServerBackendClient] Story mode — chapterId={StoryChapterId} (placeholder).");
            // TODO: fetch story data
        }

        IsReady = true;
        Debug.Log($"[ServerBackendClient] Ready. Mode={Mode}");
    }

    IEnumerator FetchMatchInfo()
    {
        string url = $"{config.backendUrl}/api/match/{MatchId}/info";

        using var request = UnityWebRequest.Get(url);
        request.SetRequestHeader("ngrok-skip-browser-warning", "true");
        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError($"[ServerBackendClient] Failed to fetch match info: {request.error}");
            IsReady = true;
            yield break;
        }

        var info = JsonUtility.FromJson<MatchInfoResponse>(request.downloadHandler.text);
        Mode = info.mode;
        StoryChapterId = info.storyChapterId;
        Debug.Log($"[ServerBackendClient] Mode={Mode}");
    }

    IEnumerator FetchLoadouts()
    {
        string url = $"{config.backendUrl}/api/match/{MatchId}/loadouts";

        using var request = UnityWebRequest.Get(url);
        request.SetRequestHeader("ngrok-skip-browser-warning", "true");
        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError($"[ServerBackendClient] Failed to fetch loadouts: {request.error}");
            yield break;
        }

        string json = request.downloadHandler.text;
        Debug.Log($"[ServerBackendClient] Loadouts received: {json}");

        LoadoutResponse = JsonUtility.FromJson<MatchLoadoutsResponse>(json);
        Debug.Log($"[ServerBackendClient] Player1={LoadoutResponse.player1Id} Player2={LoadoutResponse.player2Id}");
    }

    // -------------------------------------------------------
    // Report
    // -------------------------------------------------------

    public IEnumerator ReportCancelled()
    {
        Debug.Log($"[ServerBackendClient] Reporting cancelled for match {MatchId}");
        yield return StartCoroutine(ReportStatus("cancelled", null));
    }

    public IEnumerator ReportResult(string winnerId, int durationSeconds, int totalInstants)
    {
        Debug.Log($"[ServerBackendClient] Reporting result for match {MatchId} — winner: {winnerId}");
        yield return StartCoroutine(ReportStatus("completed", new MatchReportResult
        {
            winnerId = winnerId,
            durationSeconds = durationSeconds,
            totalInstants = totalInstants
        }));
    }

    IEnumerator ReportStatus(string status, MatchReportResult result)
    {
        string json = result != null
            ? $"{{\"status\":\"{status}\",\"result\":{{\"winnerId\":\"{result.winnerId}\",\"durationSeconds\":{result.durationSeconds},\"totalInstants\":{result.totalInstants}}}}}"
            : $"{{\"status\":\"{status}\"}}";

        byte[] body = System.Text.Encoding.UTF8.GetBytes(json);

        using var request = new UnityWebRequest(
            $"{config.backendUrl}/api/match/{MatchId}/status", "PATCH");
        request.uploadHandler = new UploadHandlerRaw(body);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");
        request.SetRequestHeader("ngrok-skip-browser-warning", "true");

        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
            Debug.LogError($"[ServerBackendClient] Failed to report status: {request.error}");
        else
            Debug.Log($"[ServerBackendClient] Status reported: {status}");
    }

    [System.Serializable]
    private class MatchReportResult
    {
        public string winnerId;
        public int durationSeconds;
        public int totalInstants;
    }

    // -------------------------------------------------------
    // Helpers
    // -------------------------------------------------------

    private string GetMatchIdFromArgs()
    {
        string[] args = System.Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
            if (args[i] == "-matchId")
                return args[i + 1];
        return string.Empty;
    }
}