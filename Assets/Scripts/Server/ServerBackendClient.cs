using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Called by ServerController on startup in real match flow.
/// Reads matchId from command line args, fetches loadouts from backend.
/// If no matchId arg found, signals dev mode — use hardcoded loadouts.
/// </summary>
/// 
/// 

[System.Serializable]
public class MatchLoadoutsResponse
{
    public string player1Id;
    public string player2Id;
    public TeamLoadoutData player1Loadout;
    public TeamLoadoutData player2Loadout;
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
    [SerializeField] private string backendUrl = "http://localhost:5276";

    public bool IsDevMode { get; private set; } = true;
    public string MatchId { get; private set; } = string.Empty;

    // -------------------------------------------------------
    // Init
    // -------------------------------------------------------
    public IEnumerator FetchMatchData()
    {
        MatchId = GetMatchIdFromArgs();

        if (string.IsNullOrEmpty(MatchId))
        {
            Debug.Log("[ServerBackendClient] No matchId arg — dev mode, using hardcoded loadouts.");
            IsDevMode = true;
            yield break;
        }

        IsDevMode = false;
        Debug.Log($"[ServerBackendClient] MatchId={MatchId} — fetching loadouts from backend.");
        yield return StartCoroutine(FetchLoadouts());
    }

    // -------------------------------------------------------
    // Fetch
    // -------------------------------------------------------
    public MatchLoadoutsResponse LoadoutResponse { get; private set; }

    IEnumerator FetchLoadouts()
    {
        string url = $"{backendUrl}/api/match/{MatchId}/loadouts";

        using var request = UnityWebRequest.Get(url);
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