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

[System.Serializable]
public class AfterMatchPlayerReport
{
    public string playerId;
    public int unitsAlive;
    public int totalUnits;
}

public class ServerBackendClient : MonoBehaviour
{
    [Header("Config")]
    [SerializeField] private BackendConfig config;
    [SerializeField] private ServerCredentialsConfig serverCredentials;

    public string MatchId { get; private set; } = string.Empty;
    public string Mode { get; private set; } = "pvp";
    public string MapId { get; private set; } = "defaultPvpMap";

    // Story mode only — read from CLI args, set by MatchmakingService.SpawnServer.
    // Always 0 for PvP matches (never set, never used by PvP code paths).
    public int ChapterId { get; private set; } = 0;
    public int SceneId { get; set; } = 0;

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
        ChapterId = GetIntArg("-chapterId", 0);
        SceneId = GetIntArg("-sceneId", 0);
        string modeFromArgs = GetStringArg("-mode", string.Empty);

        // Fallback: if backend forgot -mode story, infer from -chapterId
        if (string.IsNullOrEmpty(modeFromArgs) && HasArg("-chapterId"))
        {
            modeFromArgs = "story";
        }

        if (string.IsNullOrEmpty(MatchId))
        {
            Debug.LogError("[ServerBackendClient] No -matchId arg provided — server cannot operate without a match.");
            IsReady = true;
            yield break;
        }

        yield return StartCoroutine(Login());
        if (string.IsNullOrEmpty(ServerToken))
        {
            yield return StartCoroutine(QuitMatchServer("could not log in to the backend"));
            yield break;
        }

        Debug.Log($"[ServerBackendClient] MatchId={MatchId} — fetching match info.");
        bool gotInfo = false;
        yield return StartCoroutine(FetchMatchInfo(() => gotInfo = true));
        if (!gotInfo)
        {
            yield return StartCoroutine(QuitMatchServer("could not fetch match info"));
            yield break;
        }

        // CLI arg -mode overrides backend response (story servers pass -mode story,
        // PvP servers don't pass -mode so they keep the backend value unchanged)
        if (!string.IsNullOrEmpty(modeFromArgs))
        {
            Debug.Log($"[ServerBackendClient] Mode overridden by CLI arg: '{Mode}' → '{modeFromArgs}'");
            Mode = modeFromArgs;
        }

        // Every mode (pvp, ranked, friendly, story) plays with backend loadouts
        if (Mode == "story")
            Debug.Log($"[ServerBackendClient] Story mode — chapter={ChapterId} scene={SceneId} " +
                      $"MapId={MapId}. Fetching player1 loadout (may be unused if scene has local preset).");
        yield return StartCoroutine(FetchLoadouts());
        if (LoadoutResponse == null)
        {
            yield return StartCoroutine(QuitMatchServer("could not fetch loadouts"));
            yield break;
        }

        IsReady = true;
        Debug.Log($"[ServerBackendClient] Ready. Mode={Mode}");
    }

    // A match server that can't talk to the backend can't run a match: nobody passes verify-player and no
    // result can be reported. Exit instead of holding the port — the backend reaper ends the match within ~30s.
    IEnumerator QuitMatchServer(string reason)
    {
        Debug.LogError($"[ServerBackendClient] Startup failed ({reason}) — quitting.");
        if (!string.IsNullOrEmpty(ServerToken))
            yield return StartCoroutine(ReportCancelled());
        Application.Quit(1);
    }

    // -------------------------------------------------------
    // Startup requests — retried, because a backend restart or slow Mongo moment used to kill the whole match
    // -------------------------------------------------------

    const int StartupAttempts = 5;        // waits 2+4+6+8 s between tries → gives up after ~45 s worst case
    const int RequestTimeoutSeconds = 5;

    // onGiveUp gets the last HTTP status code (0 = no response) when every attempt failed
    IEnumerator SendWithRetry(string what, System.Func<UnityWebRequest> makeRequest, System.Action<UnityWebRequest> onSuccess,
        int attempts = StartupAttempts, int timeoutSeconds = RequestTimeoutSeconds, System.Action<long> onGiveUp = null)
    {
        long code = 0;
        for (int attempt = 1; attempt <= attempts; attempt++)
        {
            using var request = makeRequest();
            request.timeout = timeoutSeconds;
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                onSuccess(request);
                yield break;
            }

            Debug.LogWarning($"[ServerBackendClient] {what} failed (attempt {attempt}/{attempts}): {request.responseCode} {request.error}");
            // Wrong password, missing match, forbidden... won't fix themselves — only network/5xx/timeouts are retried
            code = request.responseCode;
            if (code >= 400 && code < 500 && code != 408 && code != 429) break;
            if (attempt < attempts) yield return new WaitForSeconds(attempt * 2);
        }
        onGiveUp?.Invoke(code);
    }

    IEnumerator Login()
    {
        if (serverCredentials == null)
        {
            Debug.LogError("[ServerBackendClient] No server credentials asset assigned.");
            yield break;
        }

        byte[] body = System.Text.Encoding.UTF8.GetBytes(JsonUtility.ToJson(
            new LoginRequest { username = serverCredentials.username, password = serverCredentials.password }));

        yield return StartCoroutine(SendWithRetry("Login", () =>
        {
            var request = new UnityWebRequest($"{config.backendUrl}/api/auth/login", "POST");
            request.uploadHandler = new UploadHandlerRaw(body);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("ngrok-skip-browser-warning", "true");
            return request;
        }, request =>
        {
            var response = JsonUtility.FromJson<LoginResponse>(request.downloadHandler.text);
            ServerToken = response.token;
            Debug.Log($"[ServerBackendClient] Logged in as {response.username} (role={response.role})");
        }));
    }

    [System.Serializable] private class LoginRequest { public string username; public string password; }

    IEnumerator FetchMatchInfo(System.Action onSuccess)
    {
        yield return StartCoroutine(SendWithRetry("Fetch match info", () =>
        {
            var request = UnityWebRequest.Get($"{config.backendUrl}/api/match/{MatchId}/info");
            request.SetRequestHeader("ngrok-skip-browser-warning", "true");
            return request;
        }, request =>
        {
            var info = JsonUtility.FromJson<MatchInfoResponse>(request.downloadHandler.text);
            Mode = info.mode;
            MapId = info.mapId;
            Debug.Log($"[ServerBackendClient] Mode={Mode}");
            onSuccess();
        }));
    }

    // Also re-run after formation prep; on failure LoadoutResponse keeps its previous value
    public IEnumerator FetchLoadouts()
    {
        yield return StartCoroutine(SendWithRetry("Fetch loadouts", () =>
        {
            var request = UnityWebRequest.Get($"{config.backendUrl}/api/match/{MatchId}/loadouts");
            config.SetHeaders(request, ServerToken);
            return request;
        }, request =>
        {
            string json = request.downloadHandler.text;
            Debug.Log($"[ServerBackendClient] Loadouts received: {json}");
            LoadoutResponse = JsonUtility.FromJson<MatchLoadoutsResponse>(json);
            Debug.Log($"[ServerBackendClient] Player1={LoadoutResponse.player1?.playerId} Player2={LoadoutResponse.player2?.playerId}");
        }));
    }

    // The game server has no JWT secret, so the backend checks the connecting player's token for it.
    // Calls back with the backend-verified playerId, or null if the token is invalid or not a player of this match.
    public IEnumerator VerifyPlayer(string playerToken, System.Action<string> done)
    {
        byte[] body = System.Text.Encoding.UTF8.GetBytes(JsonUtility.ToJson(new VerifyPlayerRequest { token = playerToken }));

        using var request = new UnityWebRequest($"{config.backendUrl}/api/match/{MatchId}/verify-player", "POST");
        request.uploadHandler = new UploadHandlerRaw(body);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");
        config.SetHeaders(request, ServerToken);
        request.timeout = 5;
        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            Debug.LogWarning($"[ServerBackendClient] verify-player failed: {request.responseCode} {request.downloadHandler.text}");
            done(null);
            yield break;
        }

        done(JsonUtility.FromJson<VerifyPlayerResponse>(request.downloadHandler.text)?.playerId);
    }

    [System.Serializable] private class VerifyPlayerRequest { public string token; }
    [System.Serializable] private class VerifyPlayerResponse { public string playerId; }

    // -------------------------------------------------------
    // Report
    // -------------------------------------------------------

    public IEnumerator ReportCancelled()
    {
        Debug.Log($"[ServerBackendClient] Reporting cancelled for match {MatchId}");
        yield return StartCoroutine(ReportStatus("cancelled", null));
    }

    public IEnumerator ReportResult(string winnerId, int durationSeconds, int totalInstants, AfterMatchPlayerReport[] afterMatchTeamData)
    {
        Debug.Log($"[ServerBackendClient] Reporting result for match {MatchId} — winner: {winnerId}");
        yield return StartCoroutine(ReportStatus("completed", new MatchReportResult
        {
            winnerId = winnerId,
            durationSeconds = durationSeconds,
            totalInstants = totalInstants,
            afterMatchTeamData = afterMatchTeamData
        }));
    }

    // Story mode only — notifies backend the player won a story battle scene.
    // Called by StoryTimelineManager.EndMatch(), never by PvP code.
    public IEnumerator ReportBattleComplete(string playerId)
    {
        Debug.Log($"[ServerBackendClient] Reporting battle complete for player {playerId} " +
                  $"chapter={ChapterId} scene={SceneId}");

        string json = $"{{\"playerId\":\"{playerId}\"}}";
        byte[] body = System.Text.Encoding.UTF8.GetBytes(json);

        using var request = new UnityWebRequest(
            $"{config.backendUrl}/api/story/progress/battle-complete", "POST");
        request.uploadHandler = new UploadHandlerRaw(body);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");
        config.SetHeaders(request, ServerToken);

        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
            Debug.LogError($"[ServerBackendClient] Failed to report battle complete: {request.error}");
        else
            Debug.Log("[ServerBackendClient] Battle complete reported successfully.");
    }

    IEnumerator ReportStatus(string status, MatchReportResult result)
    {
        string json = result != null
            ? $"{{\"status\":\"{status}\",\"result\":{JsonUtility.ToJson(result)}}}"
            : $"{{\"status\":\"{status}\"}}";

        byte[] body = System.Text.Encoding.UTF8.GetBytes(json);

        // Safe to retry: the backend applies only the first report of a match (later ones get "already_reported").
        // ~8 tries over ~1 min of backoff covers a backend restart; the backend kills this process once it's done.
        // A 401 means our token is no longer accepted (JWT key rotated, backend restarted with a new key, expiry):
        // log in again once and resend — still safe, thanks to the backend's one-report-per-match claim.
        bool reported = false;
        for (int pass = 1; pass <= 2 && !reported; pass++)
        {
            long failCode = 0;
            yield return StartCoroutine(SendWithRetry($"Report {status}", () =>
            {
                var request = new UnityWebRequest($"{config.backendUrl}/api/match/{MatchId}/status", "PATCH");
                request.uploadHandler = new UploadHandlerRaw(body);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                config.SetHeaders(request, ServerToken); // read per attempt, so a fresh token is used after re-login
                return request;
            }, _ => reported = true, attempts: 8, timeoutSeconds: 30, onGiveUp: code => failCode = code));

            if (reported || failCode != 401 || pass == 2) break;

            Debug.LogWarning("[ServerBackendClient] Report rejected with 401 — logging in again.");
            ServerToken = string.Empty;
            yield return StartCoroutine(Login());
            if (string.IsNullOrEmpty(ServerToken)) break;
        }

        if (reported) Debug.Log($"[ServerBackendClient] Status reported: {status}");
        else Debug.LogError($"[ServerBackendClient] Gave up reporting status '{status}' — result is lost.");
    }

    [System.Serializable]
    private class MatchReportResult
    {
        public string winnerId;
        public int durationSeconds;
        public int totalInstants;
        public AfterMatchPlayerReport[] afterMatchTeamData;
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

    private int GetIntArg(string flag, int defaultValue)
    {
        string[] args = System.Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
            if (args[i] == flag && int.TryParse(args[i + 1], out int value))
                return value;
        return defaultValue;
    }

    private string GetStringArg(string flag, string defaultValue)
    {
        string[] args = System.Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
            if (args[i] == flag)
                return args[i + 1];
        return defaultValue;
    }

    private bool HasArg(string flag)
    {
        string[] args = System.Environment.GetCommandLineArgs();
        foreach (var arg in args)
            if (arg == flag)
                return true;
        return false;
    }
}