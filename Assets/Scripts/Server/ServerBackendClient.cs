using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

[System.Serializable]
public class MatchLoadoutsResponse
{
    public PlayerLoadoutData player1;
    public PlayerLoadoutData player2;
}

[System.Serializable]
public class MatchInfoResponse
{
    public string matchId;
    public string mode;
    public string mapId;
}

[System.Serializable]
public class LoginResponse
{
    public string token;
    public string role;
    public string username;
    public string playerId;
}

[System.Serializable]
public class PlayerLoadoutData
{
    public string playerId;
    public UnitConfigData[] units;
}

[System.Serializable]
public class UnitConfigData
{
    public string ownedUnitId;
    public int uId;
    public int grade;
    public int passiveSkillId;
    public int equippedMovementSkillId;
    public int equippedClassSkillId;
    public UnitGradeStatsData gradeStats;
    public EquippedEquipmentData equippedWeapon;
    public EquippedEquipmentData equippedTrinket;
}

[System.Serializable]
public class UnitGradeStatsData
{
    public int maxHP;
    public int maxSkillPoint;
    public int speed;
    public int damageMultiplier;
    public int damageReduction;
}

[System.Serializable]
public class EquippedEquipmentData
{
    public int definitionId;
    public int skillId;
    public int maxHP;
    public int maxSkillPoint;
    public int speed;
    public int damageMultiplier;
    public int damageReduction;
}

public class ServerBackendClient : MonoBehaviour
{
    [Header("Config")]
    [SerializeField] private BackendConfig config;
    [SerializeField] private ServerCredentialsConfig serverCredentials;

    public string MatchId { get; private set; } = string.Empty;
    public string Mode { get; private set; } = "pvp";
    public string MapId { get; private set; } = "defaultPvpMap";
    public bool IsReady { get; private set; } = false;
    public string ServerToken { get; private set; } = string.Empty;
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
            Debug.LogError("[ServerBackendClient] No -matchId arg provided — server cannot operate without a match.");
            IsReady = true;
            yield break;
        }

        yield return StartCoroutine(Login());

        Debug.Log($"[ServerBackendClient] MatchId={MatchId} — fetching match info.");
        yield return StartCoroutine(FetchMatchInfo());

        if (Mode == "pvp")
            yield return StartCoroutine(FetchLoadouts());
        else if (Mode == "story")
        {
            Debug.Log($"[ServerBackendClient] Story mode — MapId={MapId} (placeholder).");
            // TODO: fetch story data
        }

        IsReady = true;
        Debug.Log($"[ServerBackendClient] Ready. Mode={Mode}");
    }

    IEnumerator Login()
    {
        if (serverCredentials == null)
        {
            Debug.LogWarning("[ServerBackendClient] No server credentials asset assigned — requests will be unauthenticated.");
            yield break;
        }

        string json = $"{{\"username\":\"{serverCredentials.username}\",\"password\":\"{serverCredentials.password}\"}}";
        byte[] body = System.Text.Encoding.UTF8.GetBytes(json);

        using var request = new UnityWebRequest($"{config.backendUrl}/api/auth/login", "POST");
        request.uploadHandler = new UploadHandlerRaw(body);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");
        request.SetRequestHeader("ngrok-skip-browser-warning", "true");

        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError($"[ServerBackendClient] Login failed: {request.error}");
            yield break;
        }

        var response = JsonUtility.FromJson<LoginResponse>(request.downloadHandler.text);
        ServerToken = response.token;
        Debug.Log($"[ServerBackendClient] Logged in as {response.username} (role={response.role})");
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
        MapId = info.mapId;
        Debug.Log($"[ServerBackendClient] Mode={Mode}");
    }

    IEnumerator FetchLoadouts()
    {
        string url = $"{config.backendUrl}/api/match/{MatchId}/loadouts";

        using var request = UnityWebRequest.Get(url);
        config.SetHeaders(request, ServerToken);
        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError($"[ServerBackendClient] Failed to fetch loadouts: {request.error}");
            yield break;
        }

        string json = request.downloadHandler.text;
        Debug.Log($"[ServerBackendClient] Loadouts received: {json}");

        LoadoutResponse = JsonUtility.FromJson<MatchLoadoutsResponse>(json);
        Debug.Log($"[ServerBackendClient] Player1={LoadoutResponse.player1?.playerId} Player2={LoadoutResponse.player2?.playerId}");
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
        config.SetHeaders(request, ServerToken);

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