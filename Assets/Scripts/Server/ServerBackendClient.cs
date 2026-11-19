using System.Collections;
using Unity.Netcode;
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

public class ServerBackendClient : MonoBehaviour, IMatchEnder
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
        Debug.Log($"[ServerBackendClient] Process PID={System.Diagnostics.Process.GetCurrentProcess().Id} " +
                  $"backend={config.backendUrl} args='{System.Environment.CommandLine}'");
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
        StartCoroutine(HeartbeatLoop()); // until the backend kills this process — also while ending/idle
        if (string.IsNullOrEmpty(ServerToken))
        {
            EndMatch("startup failed: could not log in to the backend");
            yield break;
        }

        Debug.Log($"[ServerBackendClient] MatchId={MatchId} — fetching match info.");
        bool gotInfo = false;
        yield return StartCoroutine(FetchMatchInfo(() => gotInfo = true));
        if (!gotInfo)
        {
            EndMatch("startup failed: could not fetch match info");
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
            EndMatch("startup failed: could not fetch loadouts");
            yield break;
        }

        IsReady = true;
        Debug.Log($"[ServerBackendClient] Ready. Mode={Mode}");
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
        // A player can connect before this server's own login finished — calling now would send no token (401).
        // Wait a little; the whole approval must stay under NGO's ~10s pending-connection timeout.
        float waitStart = Time.realtimeSinceStartup;
        while (string.IsNullOrEmpty(ServerToken) && Time.realtimeSinceStartup - waitStart < 5f)
            yield return null;
        if (string.IsNullOrEmpty(ServerToken))
        {
            Debug.LogWarning("[ServerBackendClient] verify-player: server not logged in to the backend yet — " +
                             "rejecting for now, the client's retry will try again.");
            done(null);
            yield break;
        }

        byte[] body = System.Text.Encoding.UTF8.GetBytes(JsonUtility.ToJson(new VerifyPlayerRequest { token = playerToken }));

        using var request = new UnityWebRequest($"{config.backendUrl}/api/match/{MatchId}/verify-player", "POST");
        request.uploadHandler = new UploadHandlerRaw(body);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");
        config.SetHeaders(request, ServerToken);
        request.timeout = 4;
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
    // Heartbeat — tells the backend "this server is alive and its main thread is running".
    // The backend ends a match with no heartbeat for 2.5 min (crashed / hung server), so one lost beat is fine:
    // no retries here, the next beat is the retry.
    // -------------------------------------------------------

    const float HeartbeatIntervalSeconds = 60f;

    IEnumerator HeartbeatLoop()
    {
        bool warnedNoRecord = false;
        while (true)
        {
            // Token missing (startup login failed) or rejected (401 below) — get a new one; ReportStatus may too
            if (string.IsNullOrEmpty(ServerToken))
                yield return StartCoroutine(Login());

            if (!string.IsNullOrEmpty(ServerToken))
            {
                string phase = IsEnding ? "ending" : UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
                byte[] body = System.Text.Encoding.UTF8.GetBytes(JsonUtility.ToJson(new HeartbeatRequest { phase = phase }));

                using var request = new UnityWebRequest($"{config.backendUrl}/api/match/{MatchId}/heartbeat", "POST");
                request.uploadHandler = new UploadHandlerRaw(body);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                config.SetHeaders(request, ServerToken);
                request.timeout = 10;
                yield return request.SendWebRequest();

                if (request.responseCode == 401)
                    ServerToken = string.Empty;
                else if (request.responseCode == 404 && !warnedNoRecord)
                {
                    warnedNoRecord = true;
                    Debug.LogWarning($"[ServerBackendClient] Heartbeat: match record {MatchId} is gone — " +
                                     "waiting for the backend to stop this server.");
                }
                else if (request.result != UnityWebRequest.Result.Success && request.responseCode != 404)
                    Debug.LogWarning($"[ServerBackendClient] Heartbeat failed ({phase}): {request.responseCode} {request.error}");
            }

            yield return new WaitForSecondsRealtime(HeartbeatIntervalSeconds);
        }
    }

    [System.Serializable] private class HeartbeatRequest { public string phase; }

    // -------------------------------------------------------
    // End of life — a match server NEVER quits itself: only the backend has cleanup authority.
    // It reports its final status, then idles until the backend stops the process:
    //   report accepted          → backend kills it right away
    //   record already gone (404) → backend kills it as an orphan within seconds
    //   backend unreachable      → keeps retrying; if it never comes back, the backend's 60-min limit ends it
    // -------------------------------------------------------

    public bool IsEnding { get; private set; }

    // Any failure / timeout / abort path: the match is cancelled
    public void EndMatch(string reason) => EndMatch(reason, "cancelled", null);

    public void ReportResult(string winnerId, int durationSeconds, int totalInstants, AfterMatchPlayerReport[] afterMatchTeamData, bool isForfeit = false, string forfeitPlayerId = null)
    {
        EndMatch($"match finished — winner={winnerId} isForfeit={isForfeit}", "completed", new MatchReportResult
        {
            winnerId = winnerId,
            durationSeconds = durationSeconds,
            totalInstants = totalInstants,
            isForfeit = isForfeit,
            forfeitPlayerId = forfeitPlayerId ?? string.Empty,
            afterMatchTeamData = afterMatchTeamData
        });
    }

    void EndMatch(string reason, string status, MatchReportResult result)
    {
        if (IsEnding)
        {
            Debug.LogWarning($"[ServerBackendClient][END] Ignored second end request ('{reason}') — already ending.");
            return;
        }
        IsEnding = true;
        Debug.Log($"[ServerBackendClient][END] match={MatchId} status={status} reason={reason} " +
                  $"uptime={Time.realtimeSinceStartup:0}s");
        // Runs on this DontDestroyOnLoad object, so a scene change can't cut the report short
        StartCoroutine(EndSequence(reason, status, result));
    }

    IEnumerator EndSequence(string reason, string status, MatchReportResult result)
    {
        // Let the last RPCs (match end screen etc.) reach the clients before closing the transport
        yield return new WaitForSecondsRealtime(2f);
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            NetworkManager.Singleton.Shutdown();
            Debug.Log("[ServerBackendClient][END] Network shut down — clients disconnected.");
        }

        if (string.IsNullOrEmpty(MatchId))
            Debug.Log("[ServerBackendClient][END] No matchId (dev run) — nothing to report.");
        else
            yield return StartCoroutine(ReportStatus(status, result));

        float idleSince = Time.realtimeSinceStartup;
        while (true)
        {
            yield return new WaitForSecondsRealtime(60f);
            Debug.LogWarning($"[ServerBackendClient][END] Still alive, waiting for the backend to stop this server — " +
                             $"match={MatchId} reason={reason} waited={Time.realtimeSinceStartup - idleSince:0}s " +
                             $"PID={System.Diagnostics.Process.GetCurrentProcess().Id}");
        }
    }

    // Story mode only — notifies backend the player won a story battle scene.
    // Called by StoryTimelineManager.EndMatch(), never by PvP code.
    public IEnumerator ReportBattleComplete(string playerId)
    {
        Debug.Log($"[ServerBackendClient] Reporting battle complete for player {playerId} " +
                  $"chapter={ChapterId} scene={SceneId}");

        string json = $"{{\"playerId\":\"{playerId}\",\"chapterId\":{ChapterId},\"sceneId\":{SceneId}}}";
        byte[] body = System.Text.Encoding.UTF8.GetBytes(json);

        // Retried: the backend ignores a repeat for an already-completed scene, so resending is safe
        bool ok = false;
        long failCode = 0;
        yield return StartCoroutine(SendWithRetry("Report battle complete", () =>
        {
            var request = new UnityWebRequest($"{config.backendUrl}/api/story/progress/battle-complete", "POST");
            request.uploadHandler = new UploadHandlerRaw(body);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            config.SetHeaders(request, ServerToken);
            return request;
        }, _ => ok = true, attempts: 8, timeoutSeconds: 30, onGiveUp: code => failCode = code));

        if (ok) Debug.Log($"[ServerBackendClient] Battle complete reported for player {playerId}.");
        else Debug.LogError($"[ServerBackendClient] Battle complete NOT recorded for player {playerId} " +
                            $"chapter={ChapterId} scene={SceneId} (last HTTP {failCode}) — story progress lost.");
    }

    // Retries until the backend gives a definitive answer. Safe to repeat: the backend applies only the
    // first report of a match (the rest get "already_reported").
    IEnumerator ReportStatus(string status, MatchReportResult result)
    {
        string json = result != null
            ? $"{{\"status\":\"{status}\",\"result\":{JsonUtility.ToJson(result)}}}"
            : $"{{\"status\":\"{status}\"}}";
        byte[] body = System.Text.Encoding.UTF8.GetBytes(json);
        Debug.Log($"[ServerBackendClient] Reporting '{status}' for match {MatchId}: {json}");

        float start = Time.realtimeSinceStartup;
        for (int attempt = 1; ; attempt++)
        {
            float elapsed = Time.realtimeSinceStartup - start;

            // No token (startup login failed, or the last one was rejected) — try to get one first
            if (string.IsNullOrEmpty(ServerToken))
                yield return StartCoroutine(Login());

            if (string.IsNullOrEmpty(ServerToken))
            {
                Debug.LogWarning($"[ServerBackendClient] Report '{status}' attempt {attempt} ({elapsed:0}s): " +
                                 "can't log in to the backend — retrying.");
            }
            else
            {
                using var request = new UnityWebRequest($"{config.backendUrl}/api/match/{MatchId}/status", "PATCH");
                request.uploadHandler = new UploadHandlerRaw(body);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                config.SetHeaders(request, ServerToken);
                request.timeout = 30;
                yield return request.SendWebRequest();

                long code = request.responseCode;
                string response = Truncate(request.downloadHandler?.text);

                if (request.result == UnityWebRequest.Result.Success)
                {
                    Debug.Log($"[ServerBackendClient] Report '{status}' accepted (attempt {attempt}, {elapsed:0}s): " +
                              $"{code} {response}");
                    yield break;
                }

                if (code == 404)
                {
                    Debug.LogError($"[ServerBackendClient] Report '{status}' → 404: match record {MatchId} no longer " +
                                   "exists on the backend — RESULT NOT RECORDED. Something removed the record before " +
                                   "the match ended (admin end-all, 60-min limit, or another backend sharing the DB). " +
                                   "The backend will stop this server as an orphan.");
                    yield break;
                }

                if (code == 401)
                {
                    Debug.LogWarning($"[ServerBackendClient] Report '{status}' attempt {attempt}: 401 — token rejected, " +
                                     "logging in again.");
                    ServerToken = string.Empty;
                }
                else if (code >= 400 && code < 500 && code != 408 && code != 429)
                {
                    Debug.LogError($"[ServerBackendClient] Report '{status}' rejected with {code} (not retryable) — " +
                                   $"RESULT NOT RECORDED. Response: {response}");
                    yield break;
                }
                else
                {
                    Debug.LogWarning($"[ServerBackendClient] Report '{status}' attempt {attempt} ({elapsed:0}s) failed: " +
                                     $"{code} {request.error} {response} — retrying.");
                }
            }

            yield return new WaitForSecondsRealtime(Mathf.Min(attempt * 2f, 30f));
        }
    }

    static string Truncate(string s, int max = 300) =>
        string.IsNullOrEmpty(s) ? string.Empty : (s.Length <= max ? s : s.Substring(0, max) + "…");

    [System.Serializable]
    private class MatchReportResult
    {
        public string winnerId;
        public int durationSeconds;
        public int totalInstants;
        public bool isForfeit;
        public string forfeitPlayerId;
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