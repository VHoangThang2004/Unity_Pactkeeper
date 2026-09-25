using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using TMPro;

public class MainMenuManager : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private TMP_Text welcomeText;
    [SerializeField] private TMP_Text profileText;
    [SerializeField] private TMP_Text gemsText;
    [SerializeField] private TMP_Text usernameText;
    [SerializeField] private GameObject loadingScreen;

    [Header("Config")]
    [SerializeField] private BackendConfig config;
    [SerializeField] private SceneConfig sceneConfig;

    [Header("Ref")]
    [SerializeField] private MatchmakingManager matchmakingManager;

    void Start()
    {
        loadingScreen?.SetActive(true);

        if (!PlayerSession.IsLoggedIn)
        {
            SceneManager.LoadScene("1_Login");
            return;
        }

        if (gemsText == null)
        {
            var gemObj = GameObject.Find("Gem");
            if (gemObj != null)
                gemsText = gemObj.GetComponentInChildren<TMP_Text>();
        }

        if (usernameText == null)
        {
            var profileContainer = GameObject.Find("ProfileContainer");
            if (profileContainer != null)
                usernameText = profileContainer.GetComponentInChildren<TMP_Text>();
        }

        welcomeText.text = Loc.Format(LocTables.MainMenu, LocKeys.MainMenu.WelcomeUser, PlayerSession.Username);
        if (usernameText != null)
            usernameText.text = PlayerSession.Username;

        StartCoroutine(FetchAllData());
    }

    IEnumerator FetchAllData()
    {
        yield return StartCoroutine(FetchProfile());

        // ── 0. Skip all flow checks if loaded as additive subscene (e.g. under C0_S2)
        if (TutorialContext.IsActive)
        {
            loadingScreen?.SetActive(false);
            yield break;
        }

        // ── 1. Ongoing match check ─────────────────────────────────────────
        bool reconnectedToPvP = false;
        yield return StartCoroutine(matchmakingManager.FetchCurrentMatch(
            match =>
            {
                if (match.mode == "pvp")
                {
                    reconnectedToPvP = true;
                    StartCoroutine(matchmakingManager.ShowMatchLoadingScreenAndLoad(
                        match.player1Name, match.player2Name));
                }
                // story: PlayerSession already populated by FetchCurrentMatch, flow continues
            },
            () => { } // no ongoing match
        ));

        if (reconnectedToPvP) yield break;

        // ── 2. Story progress ──────────────────────────────────────────────
        bool stayOnMenu = true;
        yield return StartCoroutine(StoryClient.GetCurrent(config,
            progress =>
            {
                if (!progress.isChapterCompleted)
                    stayOnMenu = ResumeStory(progress);
            }));

        // ── 3. Normal main menu ────────────────────────────────────────────
        if (stayOnMenu) loadingScreen?.SetActive(false);
    }

    // -------------------------------------------------------
    // Story resume
    // -------------------------------------------------------

    // Returns true if the main menu should stay visible, false if transitioning away.
    bool ResumeStory(StoryProgressData progress)
    {
        Debug.Log($"[MainMenu] Resuming story: " +
                  $"ch={progress.chapterId} sc={progress.sceneId} " +
                  $"type={progress.sceneType} completed={progress.isCompleted}");

        if (progress.isCompleted)
        {
            if (progress.autoNext)
            {
                StartCoroutine(AdvanceStory());
                return false;
            }
            return true; // show main menu, wait for player input
        }

        var targetScene = $"C{progress.chapterId}_S{progress.sceneId}";

        if (targetScene == SceneManager.GetActiveScene().name)
        {
            HandleInlineStory(progress);
            return true;
        }

        if (progress.sceneType == "Battle")
        {
            if (!string.IsNullOrEmpty(PlayerSession.MatchId))
                SceneManager.LoadScene(targetScene);
            else
                StartCoroutine(RequestAndStartStoryBattle(targetScene));
            return false;
        }

        // Dialogue scenes own the stage — they load as Single and pull
        // whatever subscenes they need additively themselves.
        // (e.g. C0_S2 loads 3_MainMenu additively under itself)
        SceneManager.LoadScene(targetScene);
        return false;
    }

    IEnumerator RequestAndStartStoryBattle(string targetScene)
    {
        yield return StartCoroutine(StoryClient.StartStoryMatch(config,
            match =>
            {
                PlayerSession.MatchId = match.matchId;
                PlayerSession.ServerIp = match.serverIp;
                PlayerSession.ServerPort = match.serverPort;
            },
            () => Debug.LogError("[MainMenu] Failed to start story battle match.")));

        if (!string.IsNullOrEmpty(PlayerSession.MatchId))
            SceneManager.LoadScene(targetScene);
    }

    void HandleInlineStory(StoryProgressData progress)
    {
        Debug.Log($"[MainMenu] Inline story ch={progress.chapterId} sc={progress.sceneId}");
        // TODO: activate tooltip overlay / tutorial panel
    }

    // -------------------------------------------------------
    // Story advancement
    // -------------------------------------------------------

    public IEnumerator CompleteAndContinue()
    {
        yield return StartCoroutine(StoryClient.CompleteAndAdvance(
            config, this,
            next => ResumeStory(next),
            () => Debug.LogWarning("[MainMenu] Story advance failed.")
        ));
    }

    public IEnumerator AdvanceStory()
    {
        yield return StartCoroutine(StoryClient.StartNextScene(config,
            next => ResumeStory(next),
            () => Debug.LogWarning("[MainMenu] AdvanceStory failed.")
        ));
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
            profileText.text = Loc.Get(LocTables.MainMenu, LocKeys.MainMenu.ProfileLoadFailed);
            yield break;
        }

        var profile = JsonUtility.FromJson<PlayerProfileResponse>(request.downloadHandler.text);
        profileText.text = Loc.Format(LocTables.MainMenu, LocKeys.MainMenu.ProfileFormat, profile.level, profile.experience);

        if (gemsText != null)
            gemsText.text = profile.gems.ToString();

        if (usernameText != null)
            usernameText.text = profile.username;
    }

    // -------------------------------------------------------
    // Scene Navigation
    // -------------------------------------------------------

    public void GoToUnitList()
    {
        matchmakingManager.OnClickCancel();
        SceneManager.LoadScene(sceneConfig.unitListScene);
    }

    public void GoToGacha()
    {
        matchmakingManager.OnClickCancel();
        SceneManager.LoadScene(sceneConfig.gachaScene);
    }
}