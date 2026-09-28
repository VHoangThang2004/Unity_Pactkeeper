using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;

/// <summary>
/// Shared story API client — any scene can use these coroutines.
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
            Debug.LogWarning($"[StoryClient] GetCurrent failed: {req.error}");
            onFail?.Invoke();
            yield break;
        }

        var data = JsonUtility.FromJson<StoryProgressData>(req.downloadHandler.text);
        onSuccess(data);
    }

    // -------------------------------------------------------
    // POST /api/story/progress/complete
    // Marks current scene as completed.
    // -------------------------------------------------------

    public static IEnumerator CompleteCurrentScene(
        BackendConfig config,
        System.Action<StoryProgressData> onSuccess,
        System.Action onFail = null)
    {
        using var req = UnityWebRequest.PostWwwForm(
            $"{config.backendUrl}/api/story/progress/complete", "");
        config.SetHeaders(req, PlayerSession.Token);
        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            Debug.LogWarning($"[StoryClient] CompleteCurrentScene failed: {req.error}");
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
    // Helper: Complete then advance, respecting autoNext.
    // Common pattern used by any scene that finishes a non-battle step.
    // After completion, calls onNext with the new progress.
    // -------------------------------------------------------

    public static IEnumerator CompleteAndAdvance(
        BackendConfig config,
        MonoBehaviour runner,
        System.Action<StoryProgressData> onNext,
        System.Action onFail = null)
    {
        StoryProgressData completed = null;
        yield return runner.StartCoroutine(CompleteCurrentScene(config,
            data => completed = data, onFail));

        if (completed == null) yield break;

        if (completed.autoNext)
        {
            StoryProgressData next = null;
            yield return runner.StartCoroutine(StartNextScene(config,
                data => next = data, onFail));
            if (next != null) onNext(next);
        }
        else
        {
            onNext(completed);
        }
    }

    // -------------------------------------------------------
    // Helper: Route to correct Unity scene based on progress.
    // Uses naming convention: C{chapterId}_S{sceneId}
    // -------------------------------------------------------

    public static void RouteToStoryScene(StoryProgressData progress)
    {
        if (progress.isChapterCompleted)
        {
            Debug.Log("[StoryClient] Chapter complete — returning to main menu.");
            SceneManager.LoadScene("3_MainMenu");
            return;
        }

        var targetScene = $"C{progress.chapterId}_S{progress.sceneId}";

        if (targetScene == SceneManager.GetActiveScene().name)
        {
            Debug.Log($"[StoryClient] Already on {targetScene} — no scene change needed.");
            return;
        }

        Debug.Log($"[StoryClient] Routing to {targetScene}");
        SceneManager.LoadScene(targetScene);
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
    public bool autoNext;
}

[System.Serializable]
public class StoryMatchData
{
    public string matchId;
    public string serverIp;
    public int serverPort;
}