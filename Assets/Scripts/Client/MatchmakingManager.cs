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

        var response = JsonUtility.FromJson<QueueStatusResponse>(request.downloadHandler.text);
        Debug.Log($"[Matchmaking] Queue status: {response.status}");

        if (response.status == "matched")
        {
            PlayerSession.MatchId = response.matchId;
            PlayerSession.ServerIp = response.serverIp;
            PlayerSession.ServerPort = response.serverPort;

            UnityEngine.SceneManagement.SceneManager.LoadScene(sceneConfig.clientMatch);
        }
    }

    [System.Serializable]
    private class QueueStatusResponse
    {
        public string status;
        public string matchId;
        public string serverIp;
        public int serverPort;
    }
}