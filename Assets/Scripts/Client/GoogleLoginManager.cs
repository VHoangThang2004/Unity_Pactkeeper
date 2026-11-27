using System;
using System.Collections;
using System.Net;
using System.Text;
using System.Threading;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;

public class GoogleLoginManager : MonoBehaviour
{
    [Header("Config")]
    [SerializeField] private BackendConfig config;
    [SerializeField] private SceneConfig sceneConfig;

    private HttpListener httpListener;
    private Coroutine activeOAuthCoroutine;
    private volatile string receivedCode;
    private volatile string receivedError;
    private string receivedToken;
    private string receivedUsername;
    private string receivedPlayerId;

    private const string CallbackSuccessHtml = @"<!DOCTYPE html>
<html lang=""en"">
<head>
  <meta charset=""UTF-8"" />
  <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"" />
  <title>Pactkeeper — Login Successful</title>
  <style>
    body {
      margin: 0; height: 100vh; display: flex; align-items: center; justify-content: center;
      background: radial-gradient(circle at top, #2B1D24 0%, #140D10 70%);
      color: #F2E4C8; font-family: 'Segoe UI', system-ui, sans-serif;
    }
    .card {
      background: #1E161C; border: 2px solid #C99746; border-radius: 10px;
      padding: 36px 44px; text-align: center; max-width: 400px;
      box-shadow: 0 18px 45px rgba(0, 0, 0, 0.65);
    }
    .icon {
      width: 56px; height: 56px; margin: 0 auto 18px; border-radius: 50%;
      background: rgba(44, 94, 59, 0.25); border: 2px solid #4CAF70;
      display: flex; align-items: center; justify-content: center;
      font-size: 28px; color: #6EE798;
    }
    h1 { margin: 0 0 8px; font-size: 20px; letter-spacing: 2px; color: #E8C678; text-transform: uppercase; }
    p { margin: 0; font-size: 14px; color: #C9B9A6; line-height: 1.5; }
    .hint { margin-top: 18px; font-size: 12px; color: #8F7E72; }
  </style>
</head>
<body>
  <div class=""card"">
    <div class=""icon"">&#10003;</div>
    <h1>Pactkeeper</h1>
    <p>Authentication successful.<br/>Returning you to the game client...</p>
    <p class=""hint"">You may safely close this browser tab.</p>
  </div>
  <script>
    if (window.history && window.history.replaceState) {
      window.history.replaceState({}, document.title, '/callback');
    }
  </script>
</body>
</html>";

    private const string CallbackCancelledHtml = @"<!DOCTYPE html>
<html lang=""en"">
<head>
  <meta charset=""UTF-8"" />
  <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"" />
  <title>Pactkeeper — Login Cancelled</title>
  <style>
    body {
      margin: 0; height: 100vh; display: flex; align-items: center; justify-content: center;
      background: radial-gradient(circle at top, #2B1D24 0%, #140D10 70%);
      color: #F2E4C8; font-family: 'Segoe UI', system-ui, sans-serif;
    }
    .card {
      background: #1E161C; border: 2px solid #9E5B40; border-radius: 10px;
      padding: 36px 44px; text-align: center; max-width: 400px;
      box-shadow: 0 18px 45px rgba(0, 0, 0, 0.65);
    }
    .icon {
      width: 56px; height: 56px; margin: 0 auto 18px; border-radius: 50%;
      background: rgba(158, 91, 64, 0.22); border: 2px solid #D97757;
      display: flex; align-items: center; justify-content: center;
      font-size: 26px; color: #F09878;
    }
    h1 { margin: 0 0 8px; font-size: 20px; letter-spacing: 2px; color: #E8C678; text-transform: uppercase; }
    p { margin: 0; font-size: 14px; color: #C9B9A6; line-height: 1.5; }
    .hint { margin-top: 18px; font-size: 12px; color: #8F7E72; }
  </style>
</head>
<body>
  <div class=""card"">
    <div class=""icon"">&#10005;</div>
    <h1>Login Cancelled</h1>
    <p>Google sign-in was cancelled.<br/>You can try again from the game client anytime.</p>
    <p class=""hint"">You may safely close this browser tab.</p>
  </div>
  <script>
    if (window.history && window.history.replaceState) {
      window.history.replaceState({}, document.title, '/callback');
    }
  </script>
</body>
</html>";

    [Serializable]
    private class ExchangeCodeRequest
    {
        public string code;
    }

    [Serializable]
    private class ExchangeCodeResponse
    {
        public string token;
        public string role;
        public string username;
        public string playerId;
    }

    // -------------------------------------------------------
    // Login
    // -------------------------------------------------------

    public void OnClickGoogleLogin()
    {
        if (activeOAuthCoroutine != null)
        {
            StopCoroutine(activeOAuthCoroutine);
            activeOAuthCoroutine = null;
        }
        StopHttpListener();
        activeOAuthCoroutine = StartCoroutine(PCOAuthFlow());
    }

    IEnumerator PCOAuthFlow()
    {
        receivedCode = null;
        receivedError = null;
        receivedToken = null;
        receivedUsername = null;
        receivedPlayerId = null;

        // 1. Start local listener
        if (!StartHttpListener())
        {
            activeOAuthCoroutine = null;
            yield break;
        }

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

        // 3. Wait for one-time code or cancellation from localhost callback
        float timeout = 120f;
        float elapsed = 0f;
        while (string.IsNullOrEmpty(receivedCode) && string.IsNullOrEmpty(receivedError) && elapsed < timeout)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        StopHttpListener();
        activeOAuthCoroutine = null;

        if (!string.IsNullOrEmpty(receivedError))
        {
            Debug.Log($"[GoogleLogin] Login cancelled or aborted ({receivedError}).");
            yield break;
        }

        if (string.IsNullOrEmpty(receivedCode))
        {
            Debug.LogError("[GoogleLogin] Timeout — no one-time code received.");
            yield break;
        }

        // 4. Exchange one-time code for JWT token & user metadata
        yield return StartCoroutine(ExchangeCodeForToken(receivedCode));

        if (string.IsNullOrEmpty(receivedToken))
        {
            Debug.LogError("[GoogleLogin] Failed to exchange one-time code for JWT token.");
            yield break;
        }

        // 5. Store session
        PlayerSession.Token = receivedToken;
        PlayerSession.Username = receivedUsername;
        PlayerSession.PlayerId = receivedPlayerId;

        Debug.Log($"[GoogleLogin] Logged in as {receivedUsername}");
        SceneManager.LoadScene(sceneConfig.mainMenuScene);
    }

    private IEnumerator ExchangeCodeForToken(string code)
    {
        string jsonBody = JsonUtility.ToJson(new ExchangeCodeRequest { code = code });
        byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);

        using var request = new UnityWebRequest($"{config.backendUrl}/api/auth/exchange-code", "POST");
        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");
        config.SetHeaders(request);

        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError($"[GoogleLogin] Code exchange HTTP error ({request.responseCode}): {request.error}");
            yield break;
        }

        var response = JsonUtility.FromJson<ExchangeCodeResponse>(request.downloadHandler.text);
        if (response != null)
        {
            receivedToken = response.token;
            if (!string.IsNullOrEmpty(response.username))
                receivedUsername = response.username;
            if (!string.IsNullOrEmpty(response.playerId))
                receivedPlayerId = response.playerId;
        }
    }

    // -------------------------------------------------------
    // HttpListener
    // -------------------------------------------------------

    bool StartHttpListener()
    {
        HttpListener listener;
        try
        {
            listener = new HttpListener();
            listener.Prefixes.Add("http://localhost:5000/callback/");
            listener.Start();
            httpListener = listener;
            Debug.Log("[GoogleLogin] Listening on http://localhost:5000/callback/");
        }
        catch (Exception e)
        {
            Debug.LogError($"[GoogleLogin] Could not start local callback listener on port 5000: {e.Message}");
            return false;
        }

        Thread listenerThread = new Thread(() =>
        {
            try
            {
                var context = listener.GetContext();
                var query = context.Request.QueryString;

                string errorParam = query["error"];
                string codeParam = query["code"];

                bool isError = !string.IsNullOrEmpty(errorParam) || string.IsNullOrEmpty(codeParam);
                string htmlToSend = isError ? CallbackCancelledHtml : CallbackSuccessHtml;

                byte[] buffer = Encoding.UTF8.GetBytes(htmlToSend);
                context.Response.ContentType = "text/html; charset=utf-8";
                context.Response.ContentLength64 = buffer.Length;
                context.Response.OutputStream.Write(buffer, 0, buffer.Length);
                context.Response.OutputStream.Flush();
                context.Response.OutputStream.Close();

                // Wake main thread ONLY AFTER response stream is fully flushed & closed
                if (isError)
                    receivedError = !string.IsNullOrEmpty(errorParam) ? errorParam : "cancelled";
                else
                    receivedCode = codeParam;
            }
            catch (HttpListenerException)
            {
                // Listener was stopped before a callback arrived (e.g. timeout or user clicked login again)
            }
            catch (ObjectDisposedException)
            {
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        });

        listenerThread.IsBackground = true;
        listenerThread.Start();
        return true;
    }

    void StopHttpListener()
    {
        try
        {
            if (httpListener != null)
            {
                if (httpListener.IsListening)
                    httpListener.Stop();
                httpListener.Close();
                httpListener = null;
            }
        }
        catch { }
    }

    void OnDestroy()
    {
        StopHttpListener();
    }
}
