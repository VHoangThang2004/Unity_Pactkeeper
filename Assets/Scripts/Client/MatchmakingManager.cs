using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using TMPro;

public class MatchmakingManager : MonoBehaviour
{
    [Header("Config")]
    [SerializeField] private BackendConfig config;
    [SerializeField] private SceneConfig sceneConfig;


    [Header("UI")]
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private GameObject findMatchButton;
    [SerializeField] private GameObject cancelButton;
    [SerializeField] private GameObject matchFoundPanel;
    [SerializeField] private TMP_Text player1Text;
    [SerializeField] private TMP_Text player2Text;
    [SerializeField] private TMP_Text vsText;

    private bool isInQueue = false;
    private Coroutine pollingCoroutine;

    private void OnDestroy()
    {
        // OnClickCancel();
    }

    public void OnClickFindMatch()
    {
        if (!PlayerSession.IsLoggedIn)
        {
            statusText.text = "Not logged in.";
            return;
        }
        StartCoroutine(JoinQueue());
    }

    public void OnClickCancel()
    {
        StartCoroutine(LeaveQueue());
    }

    IEnumerator JoinQueue()
    {
        statusText.text = "Joining queue...";

        using var request = new UnityWebRequest($"{config.backendUrl}/api/match/queue/join", "POST");
        request.uploadHandler = new UploadHandlerRaw(new byte[0]);
        request.downloadHandler = new DownloadHandlerBuffer();
        config.SetHeaders(request, PlayerSession.Token);

        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            statusText.text = "Failed to join queue.";
            Debug.LogError($"[Matchmaking] {request.error}");
            yield break;
        }

        isInQueue = true;
        findMatchButton.SetActive(false);
        cancelButton.SetActive(true);
        statusText.text = "Finding match...";

        pollingCoroutine = StartCoroutine(PollQueueStatus());
    }

    IEnumerator LeaveQueue()
    {
        using var request = new UnityWebRequest($"{config.backendUrl}/api/match/queue/leave", "DELETE");
        request.downloadHandler = new DownloadHandlerBuffer();
        config.SetHeaders(request, PlayerSession.Token);

        yield return request.SendWebRequest();

        isInQueue = false;
        if (pollingCoroutine != null) StopCoroutine(pollingCoroutine);
        findMatchButton.SetActive(true);
        cancelButton.SetActive(false);
        statusText.text = "Cancelled.";
    }

    IEnumerator PollQueueStatus()
    {
        while (isInQueue)
        {
            yield return new WaitForSeconds(3f);
            yield return StartCoroutine(CheckQueueStatus());
        }
    }

    public IEnumerator CheckQueueStatus()
    {
        using var request = UnityWebRequest.Get($"{config.backendUrl}/api/match/queue/status");
        config.SetHeaders(request, PlayerSession.Token);

        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError($"[Matchmaking] Poll failed: {request.error}");
            yield break;
        }

        string rawResponseText = request.downloadHandler.text;
        var response = JsonUtility.FromJson<QueueStatusResponse>(rawResponseText);
        Debug.Log($"[Matchmaking] Queue status: {response.status}, player1Name='{response.player1Name}', player2Name='{response.player2Name}', raw: {rawResponseText}");

        if (response.status == "matched")
        {
            PlayerSession.MatchId = response.matchId;
            PlayerSession.ServerIp = response.serverIp;
            PlayerSession.ServerPort = response.serverPort;

            StartCoroutine(ShowMatchLoadingScreenAndLoad(response.player1Name, response.player2Name));
        }
    }

    IEnumerator ShowMatchLoadingScreenAndLoad(string p1Name, string p2Name)
    {
        Debug.Log($"[Matchmaking] ShowMatchLoadingScreenAndLoad: p1Name='{p1Name}', p2Name='{p2Name}'");
        isInQueue = false;
        if (pollingCoroutine != null) StopCoroutine(pollingCoroutine);

        findMatchButton.SetActive(false);
        cancelButton.SetActive(false);

        if (matchFoundPanel != null)
        {
            matchFoundPanel.SetActive(true);
            Debug.Log($"[Matchmaking] matchFoundPanel active. player1Text={player1Text != null}, player2Text={player2Text != null}");
            if (player1Text != null) 
            {
                player1Text.text = p1Name;
                Debug.Log($"[Matchmaking] Set player1Text.text = '{p1Name}'");
            }
            if (player2Text != null) 
            {
                player2Text.text = p2Name;
                Debug.Log($"[Matchmaking] Set player2Text.text = '{p2Name}'");
            }
            if (vsText != null) vsText.text = "VS";
        }
        else
        {
            // Fallback UI using statusText
            statusText.text = $"Match Found!\n\n{p1Name}\n  VS  \n{p2Name}\n\nLoading game...";
        }

        yield return new WaitForSeconds(4f);

        UnityEngine.SceneManagement.SceneManager.LoadScene(sceneConfig.clientMatch);
    }

    [System.Serializable]
    private class QueueStatusResponse
    {
        public string status;
        public string matchId;
        public string serverIp;
        public int serverPort;
        public string player1Name;
        public string player2Name;
    }
}