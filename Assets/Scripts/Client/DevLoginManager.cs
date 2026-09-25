using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using System.Text;
using TMPro;

public class DevLoginManager : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private TMP_InputField usernameInput;
    [SerializeField] private TMP_InputField passwordInput;
    [SerializeField] private TMP_Text statusText;

    [Header("Config")]
    [SerializeField] private BackendConfig config;

    [SerializeField] private SceneConfig sceneConfig;

    void Start()
    {
        if (PlayerSession.IsLoggedIn)
        {
            Debug.Log("[DevLogin] Already logged in, skipping to main menu.");
            SceneManager.LoadScene(sceneConfig.mainMenuScene);
        }
    }

    public void OnClickLogin()
    {
        if (string.IsNullOrEmpty(usernameInput.text) || string.IsNullOrEmpty(passwordInput.text))
        {
            statusText.text = Loc.Get(LocTables.DevConnect, LocKeys.DevConnect.EnterCredentials);
            return;
        }

        StartCoroutine(LoginRoutine());
    }

    IEnumerator LoginRoutine()
    {
        statusText.text = Loc.Get(LocTables.DevConnect, LocKeys.DevConnect.LoggingIn);

        string json = $"{{\"username\":\"{usernameInput.text}\",\"password\":\"{passwordInput.text}\"}}";
        byte[] body = Encoding.UTF8.GetBytes(json);

        using var request = new UnityWebRequest($"{config.backendUrl}/api/Auth/login", "POST");
        request.uploadHandler = new UploadHandlerRaw(body);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");
        config.SetHeaders(request);

        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            statusText.text = Loc.Get(LocTables.DevConnect, LocKeys.DevConnect.LoginFailed);
            Debug.LogError($"[DevLogin] {request.error}");
            yield break;
        }

        var response = JsonUtility.FromJson<AuthResponse>(request.downloadHandler.text);
        if (string.IsNullOrEmpty(response.token))
        {
            statusText.text = Loc.Get(LocTables.DevConnect, LocKeys.DevConnect.InvalidCredentials);
            yield break;
        }

        PlayerSession.Token = response.token;
        PlayerSession.Username = response.username;
        PlayerSession.PlayerId = response.playerId;

        Debug.Log($"[DevLogin] Logged in as {response.username}");
        SceneManager.LoadScene(sceneConfig.mainMenuScene);
    }

    [System.Serializable]
    private class AuthResponse
    {
        public string token;
        public string role;
        public string username;
        public string playerId;
    }
}