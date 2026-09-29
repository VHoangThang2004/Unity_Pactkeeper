using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;

/// <summary>
/// Chapter 0 Scene 4 — Unit config tutorial.
/// Fetches the first unit in the player's team loadout, sets it in PlayerSession,
/// then loads 8_UnitConfig additively so the tutorial can guide the player.
/// </summary>
public class C0_S4_Manager : MonoBehaviour
{
    [Header("Config")]
    [SerializeField] private BackendConfig config;

    [Header("Refs")]
    [SerializeField] private PlotSequencer sequencer;

    void Start() => StartCoroutine(Run());

    IEnumerator Run()
    {
        TutorialContext.IsActive = true;

        // Fetch first unit in team loadout
        bool fetchDone = false;
        yield return StartCoroutine(FetchFirstUnitId(unitId =>
        {
            if (!string.IsNullOrEmpty(unitId))
                PlayerSession.SelectedOwnedUnitId = unitId;
            else
                Debug.LogWarning("[C0_S4] No unit found in team loadout — tutorial may not display correctly.");
            fetchDone = true;
        }));

        yield return SceneManager.LoadSceneAsync("8_UnitConfig", LoadSceneMode.Additive);
        yield return null;
        yield return null;

        bool done = false;
        sequencer.StartPlot(() => done = true);
        yield return new WaitUntil(() => done);

        yield return StartCoroutine(StoryClient.CompleteAndAdvance(config, this,
            next => { TutorialContext.Reset(); SceneManager.LoadScene("3_MainMenu"); },
            () => { TutorialContext.Reset(); SceneManager.LoadScene("3_MainMenu"); }
        ));
    }

    IEnumerator FetchFirstUnitId(System.Action<string> onResult)
    {
        using var request = UnityWebRequest.Get($"{config.backendUrl}/api/TeamLoadout/first-unit-id");
        config.SetHeaders(request, PlayerSession.Token);
        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError($"[C0_S4] Failed to fetch first unit id: {request.error}");
            onResult?.Invoke(string.Empty);
            yield break;
        }

        var response = JsonUtility.FromJson<FirstUnitIdResponse>(request.downloadHandler.text);
        onResult?.Invoke(response?.ownedUnitId ?? string.Empty);
    }

    [System.Serializable]
    private class FirstUnitIdResponse
    {
        public string ownedUnitId;
    }
}