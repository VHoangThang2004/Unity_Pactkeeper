using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;

/// <summary>
/// Shared story API client - any scene can use these coroutines.
/// No MonoBehaviour dependency. Pass a MonoBehaviour as the runner to start coroutines.
/// </summary>
public static class StoryClient
{
    // -------------------------------------------------------
    // GET /api/story/progress/current
    // -------------------------------------------------------

    public static IEnumerator GetCurrent(
        BackendConfig config,
        System.Action<StoryProgressData> onSuccess,
        System.Action onFail = null)
    {
        using var req = UnityWebRequest.Get(
            $"{config.backendUrl}/api/story/progress/current");
        config.SetHeaders(req, PlayerSession.Token);
        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            Debug.LogWarning($"[StoryClient] GetCurrent failed: {req.error} - Lý do: {req.downloadHandler.text}");
            onFail?.Invoke();
            yield break;
        }

        var data = JsonUtility.FromJson<StoryProgressData>(req.downloadHandler.text);
        onSuccess(data);
    }

    // -------------------------------------------------------
    // POST /api/story/progress/complete
    // Marks current scene as completed (with optional chapterId/sceneId idempotency guard).
    // -------------------------------------------------------

    public static IEnumerator CompleteCurrentScene(
        BackendConfig config,
        System.Action<StoryProgressData> onSuccess,
        System.Action onFail = null,
        int? chapterId = null,
        int? sceneId = null)
    {
        string url = $"{config.backendUrl}/api/story/progress/complete";
        if (chapterId.HasValue && sceneId.HasValue && chapterId.Value >= 0 && sceneId.Value >= 0)
            url += $"?chapterId={chapterId.Value}&sceneId={sceneId.Value}";

        using var req = UnityWebRequest.PostWwwForm(url, "");
        config.SetHeaders(req, PlayerSession.Token);
        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            Debug.LogWarning($"[StoryClient] CompleteCurrentScene failed: {req.error} - Lý do: {req.downloadHandler.text}");
            onFail?.Invoke();
            yield break;
        }

        var data = JsonUtility.FromJson<StoryProgressData>(req.downloadHandler.text);
        onSuccess(data);
    }

    // -------------------------------------------------------
    // POST /api/story/progress/next
    // Advances to the next scene.
    // -------------------------------------------------------

    public static IEnumerator StartNextScene(
        BackendConfig config,
        System.Action<StoryProgressData> onSuccess,
        System.Action onFail = null)
    {
        using var req = UnityWebRequest.PostWwwForm(
            $"{config.backendUrl}/api/story/progress/next", "");
        config.SetHeaders(req, PlayerSession.Token);
        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            Debug.LogWarning($"[StoryClient] StartNextScene failed: {req.error}");
            onFail?.Invoke();
            yield break;
        }

        var data = JsonUtility.FromJson<StoryProgressData>(req.downloadHandler.text);
        onSuccess(data);
    }

    // -------------------------------------------------------
    // POST /api/story/match
    // Spawns a story battle server for current battle scene.
    // -------------------------------------------------------

    public static IEnumerator StartStoryMatch(
        BackendConfig config,
        System.Action<StoryMatchData> onSuccess,
        System.Action onFail = null)
    {
        using var req = UnityWebRequest.PostWwwForm(
            $"{config.backendUrl}/api/story/match", "");
        config.SetHeaders(req, PlayerSession.Token);
        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            Debug.LogWarning($"[StoryClient] StartStoryMatch failed: {req.error}");
            onFail?.Invoke();
            yield break;
        }

        var data = JsonUtility.FromJson<StoryMatchData>(req.downloadHandler.text);
        onSuccess(data);
    }

    // -------------------------------------------------------
    // POST /api/story/match/select
    // Manually starts or replays a specific (chapterId, sceneId) from 10_StoryProgress
    // with autoResume = false.
    // -------------------------------------------------------

    public static IEnumerator StartSelectedStoryMatch(
        BackendConfig config,
        int chapterId,
        int sceneId,
        bool autoResume,
        System.Action<StoryMatchSelectData> onSuccess,
        System.Action<string> onFail = null)
    {
        var payload = new SelectStoryMatchRequestPayload
        {
            chapterId = chapterId,
            sceneId = sceneId,
            autoResume = false // Strictly false for manual chapter/scene selection
        };

        string json = JsonUtility.ToJson(payload);
        byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);

        using var req = new UnityWebRequest($"{config.backendUrl}/api/story/match/select", "POST");
        req.uploadHandler = new UploadHandlerRaw(bodyRaw);
        req.downloadHandler = new DownloadHandlerBuffer();
        config.SetHeaders(req, PlayerSession.Token);
        req.SetRequestHeader("Content-Type", "application/json");

        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            string err = req.downloadHandler != null ? req.downloadHandler.text : req.error;
            Debug.LogWarning($"[StoryClient] StartSelectedStoryMatch failed: {err}");
            onFail?.Invoke(err);
            yield break;
        }

        var data = JsonUtility.FromJson<StoryMatchSelectData>(req.downloadHandler.text);
        PlayerSession.IsAutoResumed = false;
        PlayerSession.CanSkipOrExitStory = data.canSkipOrExit;
        PlayerSession.SelectedStoryChapterId = data.chapterId;
        PlayerSession.SelectedStorySceneId = data.sceneId;

        if (!string.IsNullOrEmpty(data.matchId))
        {
            PlayerSession.MatchId = data.matchId;
            PlayerSession.ServerIp = data.serverIp;
            PlayerSession.ServerPort = data.serverPort;
        }

        onSuccess?.Invoke(data);
    }

    // -------------------------------------------------------
    // POST /api/story/terminate
    // Kills the active Dedicated Server immediately when clicking "Exit to Map".
    // -------------------------------------------------------

    public static IEnumerator TerminateStoryMatch(
        BackendConfig config,
        System.Action onDone = null)
    {
        using var req = UnityWebRequest.PostWwwForm(
            $"{config.backendUrl}/api/story/terminate", "");
        config.SetHeaders(req, PlayerSession.Token);
        yield return req.SendWebRequest();

        PlayerSession.MatchId = string.Empty;
        PlayerSession.ServerIp = string.Empty;
        onDone?.Invoke();
    }

    // -------------------------------------------------------
    // POST /api/story/match/abandon
    // Abandons an active story battle match so the player can retry cleanly.
    // -------------------------------------------------------

    public static IEnumerator AbandonStoryMatch(
        BackendConfig config,
        System.Action onSuccess = null,
        System.Action onFail = null)
    {
        using var req = UnityWebRequest.PostWwwForm(
            $"{config.backendUrl}/api/story/match/abandon", "");
        config.SetHeaders(req, PlayerSession.Token);
        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            Debug.LogWarning($"[StoryClient] AbandonStoryMatch failed: {req.error}");
            onFail?.Invoke();
            yield break;
        }

        PlayerSession.MatchId = string.Empty;
        PlayerSession.ServerIp = string.Empty;
        onSuccess?.Invoke();
    }

    // -------------------------------------------------------
    // Helper: Complete the current scene. The backend auto-advances to the next
    // scene if the completed scene has AutoAdvance (reported via advancedFromPrev).
    // -------------------------------------------------------

    public static IEnumerator CompleteAndAdvance(
        BackendConfig config,
        MonoBehaviour runner,
        System.Action<StoryProgressData> onNext,
        System.Action onFail = null,
        int? chapterId = null,
        int? sceneId = null)
    {
        StoryProgressData completed = null;
        yield return runner.StartCoroutine(CompleteCurrentScene(
            config,
            data => completed = data,
            onFail,
            chapterId,
            sceneId));

        if (completed == null) yield break;

        if (Unity.Netcode.NetworkManager.Singleton != null &&
            Unity.Netcode.NetworkManager.Singleton.IsListening)
        {
            Debug.Log("[StoryClient] Shutting down NetworkManager and waiting...");
            Unity.Netcode.NetworkManager.Singleton.Shutdown();

            float waitLimit = 0f;
            while (Unity.Netcode.NetworkManager.Singleton != null &&
                   Unity.Netcode.NetworkManager.Singleton.IsListening &&
                   waitLimit < 2.0f)
            {
                waitLimit += Time.deltaTime;
                yield return null;
            }
        }
        onNext(completed);
    }

    // -------------------------------------------------------
    // Helper: Route to correct Unity scene based on progress.
    // Uses naming convention: C{chapterId}_S{sceneId}
    // Ensures seamless cross-scene continuity (S1 -> S2 -> S3) within the same chapter.
    // -------------------------------------------------------

    public static void RouteToStoryScene(StoryProgressData progress, BackendConfig config = null, MonoBehaviour runner = null)
    {
        bool isManualStorySelection = !PlayerSession.IsAutoResumed;

        // If launched manually from 10_StoryProgress and the next scene is non-PvE (e.g. 0.4..0.6 Dialogue),
        // or the chapter/scene run has finished, return to 10_StoryProgress.
        bool returnToStoryMap = progress.isChapterCompleted ||
            (isManualStorySelection && (progress.isCompleted || progress.sceneType != "Battle") && Application.CanStreamedLevelBeLoaded("10_StoryProgress"));

        // Case 1: Chapter complete or explicit return to Story Map requested
        if (progress.isChapterCompleted || returnToStoryMap)
        {
            // When completing Chapter 0 for the first time via AutoResume (after 0.6 MainMenu tutorial), return to 3_MainMenu.
            // Otherwise (Chapters 1..3 or manual replay from Story Progress), return to 10_StoryProgress.
            string dest = (progress.chapterId == 0 && PlayerSession.IsAutoResumed)
                ? "3_MainMenu"
                : (Application.CanStreamedLevelBeLoaded("10_StoryProgress") ? "10_StoryProgress" : "3_MainMenu");
            Debug.Log($"[StoryClient] Chapter complete or returning to story map -> loading {dest}.");
            SceneManager.LoadScene(dest);
            return;
        }

        // Case 2: Chapter is NOT complete -> seamless cross-scene continuity to next scene (C{chapterId}_S{sceneId})
        string targetScene = $"C{progress.chapterId}_S{progress.sceneId}";

        if (progress.sceneType == "Battle")
        {
            if (runner != null && config != null)
            {
                Debug.Log($"[StoryClient] Next scene is Battle ({targetScene}) - requesting story match before transition...");
                runner.StartCoroutine(SpawnMatchAndLoadScene(config, progress.chapterId, progress.sceneId, targetScene));
                return;
            }
            else
            {
                // Fallback if caller did not pass runner/config: route through 3_MainMenu so MainMenuManager spawns the battle server
                Debug.LogWarning($"[StoryClient] Next scene is Battle ({targetScene}) but runner/config is null - routing via 3_MainMenu to spawn server.");
                SceneManager.LoadScene("3_MainMenu");
                return;
            }
        }

        if (targetScene == SceneManager.GetActiveScene().name)
        {
            Debug.Log($"[StoryClient] Already on {targetScene} - no scene change needed.");
            return;
        }

        Debug.Log($"[StoryClient] Seamless transition to next scene: {targetScene}");
        SceneManager.LoadScene(targetScene);
    }

    private static IEnumerator SpawnMatchAndLoadScene(BackendConfig config, int chapterId, int sceneId, string targetScene)
    {
        bool matchReady = false;
        yield return StartStoryMatch(
            config,
            match =>
            {
                PlayerSession.MatchId = match.matchId;
                PlayerSession.ServerIp = match.serverIp;
                PlayerSession.ServerPort = match.serverPort;
                matchReady = true;
            },
            () => Debug.LogError($"[StoryClient] Failed to start story match for {targetScene}"));

        if (matchReady)
        {
            Debug.Log($"[StoryClient] Story match ready - loading {targetScene}...");
            SceneManager.LoadScene(targetScene);
        }
        else
        {
            Debug.LogWarning("[StoryClient] Story match failed - routing to 10_StoryProgress fallback.");
            string fallback = Application.CanStreamedLevelBeLoaded("10_StoryProgress") ? "10_StoryProgress" : "3_MainMenu";
            SceneManager.LoadScene(fallback);
        }
    }
}

// -------------------------------------------------------
// Shared response DTOs
// -------------------------------------------------------

[System.Serializable]
public class StoryProgressData
{
    public int chapterId;
    public int sceneId;
    public bool isCompleted;
    public bool isChapterCompleted;
    public string sceneType;
    public bool autoResume;
    public bool advancedFromPrev;
}

[System.Serializable]
public class StoryMatchData
{
    public string matchId;
    public string serverIp;
    public int serverPort;
}

[System.Serializable]
public class SelectStoryMatchRequestPayload
{
    public int chapterId;
    public int sceneId;
    public bool autoResume;
}

[System.Serializable]
public class StoryMatchSelectData
{
    public string matchId;
    public string serverIp;
    public int serverPort;
    public int chapterId;
    public int sceneId;
    public string sceneType;
    public bool autoResume;
    public bool canSkipOrExit;
}
