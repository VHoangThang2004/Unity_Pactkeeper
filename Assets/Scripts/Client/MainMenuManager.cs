using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using TMPro;

public class MainMenuManager : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private TMP_Text welcomeText;
    [SerializeField] private TMP_Text profileText;
    [SerializeField] private TMP_Text loadoutText;
    [SerializeField] private TMP_Text matchHistoryText;

    [Header("Config")]
    [SerializeField] private BackendConfig config;

    void Start()
    {
        if (!PlayerSession.IsLoggedIn)
        {
            SceneManager.LoadScene("1_Login");
            return;
        }

        welcomeText.text = $"Welcome, {PlayerSession.Username}!";
        StartCoroutine(FetchAllData());
    }

    IEnumerator FetchAllData()
    {
        yield return StartCoroutine(FetchProfile());
        yield return StartCoroutine(FetchLoadout());
        yield return StartCoroutine(FetchMatchHistory());
    }

    // -------------------------------------------------------
    // Profile
    // -------------------------------------------------------

    IEnumerator FetchProfile()
    {
        using var request = UnityWebRequest.Get($"{config.backendUrl}/api/PlayerProfile");
        config.SetHeaders(request, PlayerSession.Token);
        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            profileText.text = "Profile: failed to load.";
            yield break;
        }

        var profile = JsonUtility.FromJson<PlayerProfileResponse>(request.downloadHandler.text);
        profileText.text = $"Level {profile.level} | EXP {profile.experience}";
    }

    // -------------------------------------------------------
    // Loadout
    // -------------------------------------------------------

    IEnumerator FetchLoadout()
    {
        using var request = UnityWebRequest.Get($"{config.backendUrl}/api/TeamLoadout");
        config.SetHeaders(request, PlayerSession.Token);
        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            loadoutText.text = "Team: failed to load.";
            yield break;
        }

        var loadout = JsonUtility.FromJson<TeamLoadoutResponse>(request.downloadHandler.text);
        if (loadout.units == null || loadout.units.Length == 0)
        {
            loadoutText.text = "Team: empty";
            yield break;
        }

        string units = string.Join(", ", System.Array.ConvertAll(loadout.units, u => $"uId:{u.uId}"));
        loadoutText.text = $"Team: {units}";
    }

    // -------------------------------------------------------
    // Match History
    // -------------------------------------------------------

    IEnumerator FetchMatchHistory()
    {
        using var request = UnityWebRequest.Get($"{config.backendUrl}/api/Match/history");
        config.SetHeaders(request, PlayerSession.Token);
        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            matchHistoryText.text = "Matches: failed to load.";
            yield break;
        }

        var wrapper = JsonUtility.FromJson<MatchHistoryWrapper>("{\"matches\":" + request.downloadHandler.text + "}");
        if (wrapper.matches == null || wrapper.matches.Length == 0)
        {
            matchHistoryText.text = "Matches: none";
            yield break;
        }

        string history = "";
        foreach (var m in wrapper.matches)
            history += $"[{m.status.ToUpper()}] {m.matchId.Substring(0, 8)}... {m.serverIp}:{m.serverPort}\n";

        matchHistoryText.text = history.TrimEnd();
    }

    // -------------------------------------------------------
    // Response Models
    // -------------------------------------------------------

    [System.Serializable]
    private class PlayerProfileResponse
    {
        public string playerId;
        public string username;
        public int level;
        public int experience;
    }

    [System.Serializable]
    private class TeamLoadoutResponse
    {
        public string playerId;
        public UnitEntry[] units;
    }

    [System.Serializable]
    private class UnitEntry
    {
        public int uId;
        public int movementSkillId;
        public int weaponSkillId;
        public int classSkillId;
        public int equipmentSkillId;
    }

    [System.Serializable]
    private class MatchHistoryWrapper
    {
        public MatchEntry[] matches;
    }

    [System.Serializable]
    private class MatchEntry
    {
        public string matchId;
        public string player1Id;
        public string player2Id;
        public string serverIp;
        public int serverPort;
        public string status;
    }
}