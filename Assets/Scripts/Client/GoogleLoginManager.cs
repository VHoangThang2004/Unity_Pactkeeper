using System;
using System.Collections;
using System.Net;
using System.Threading;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GoogleLoginManager : MonoBehaviour
{
    [Header("Config")]
    [SerializeField] private BackendConfig config;
    [SerializeField] private SceneConfig sceneConfig;

    private HttpListener httpListener;
    private string receivedToken;
    private string receivedUsername;
    private string receivedPlayerId;

    // -------------------------------------------------------
    // Login
    // -------------------------------------------------------

    public void OnClickGoogleLogin()
    {
        StartCoroutine(PCOAuthFlow());
    }

    IEnumerator PCOAuthFlow()
    {
        receivedToken = null;
        receivedUsername = null;
        receivedPlayerId = null;

        // 1. Start local listener
        StartHttpListener();

        // 2. Open browser to Google OAuth
        var clientId = "978068800706-gr8nfa4iq66m4e6njggntupslphdar10.apps.googleusercontent.com";
        var redirectUri = Uri.EscapeDataString($"{config.backendUrl}/api/auth/google-callback");
        var scope = Uri.EscapeDataString("openid email profile");
        string authUrl = $"https://accounts.google.com/o/oauth2/auth" +
                         $"?client_id={clientId}" +
                         $"&redirect_uri={redirectUri}" +
                         $"&response_type=code" +
                         $"&scope={scope}" +
                         $"&state=pc";

        Debug.Log($"[GoogleLogin] Opening browser: {authUrl}");
        Application.OpenURL(authUrl);

        // 3. Wait for token from localhost callback
        float timeout = 120f;
        float elapsed = 0f;
        while (string.IsNullOrEmpty(receivedToken) && elapsed < timeout)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        StopHttpListener();

        if (string.IsNullOrEmpty(receivedToken))
        {
            Debug.LogError("[GoogleLogin] Timeout — no token received.");
            yield break;
        }

        // 4. Store session
        PlayerSession.Token = receivedToken;
        PlayerSession.Username = receivedUsername;
        PlayerSession.PlayerId = receivedPlayerId;

        Debug.Log($"[GoogleLogin] Logged in as {receivedUsername}");
        SceneManager.LoadScene(sceneConfig.mainMenuScene);
    }

    // -------------------------------------------------------
    // HttpListener
    // -------------------------------------------------------

    void StartHttpListener()
    {
        httpListener = new HttpListener();
        httpListener.Prefixes.Add("http://localhost:5000/callback/");
        httpListener.Start();
        Debug.Log("[GoogleLogin] Listening on http://localhost:5000/callback/");

        Thread listenerThread = new Thread(() =>
        {
            try
            {
                var context = httpListener.GetContext();
                var query = context.Request.QueryString;

                receivedToken = query["token"];
                receivedUsername = Uri.UnescapeDataString(query["username"] ?? "");
                receivedPlayerId = Uri.UnescapeDataString(query["playerId"] ?? "");

                Debug.Log($"[GoogleLogin] Received token for {receivedUsername}");

                // Send response to browser
                string html = "<html><body><h2>Login successful! You can close this tab.</h2></body></html>";
                byte[] buffer = System.Text.Encoding.UTF8.GetBytes(html);
                context.Response.ContentLength64 = buffer.Length;
                context.Response.OutputStream.Write(buffer, 0, buffer.Length);
                context.Response.OutputStream.Close();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        });

        listenerThread.IsBackground = true;
        listenerThread.Start();
    }

    void StopHttpListener()
    {
        try { httpListener?.Stop(); }
        catch { }
    }

    void OnDestroy()
    {
        StopHttpListener();
    }
}