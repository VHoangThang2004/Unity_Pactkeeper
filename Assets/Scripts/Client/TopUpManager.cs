using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Manages the Top-Up panel in MainMenu.
/// Opens an in-game overlay that displays top-up package options
/// and lets the player open the web top-up portal in their browser.
///
/// SSO: The player's JWT (PlayerSession.Token) is appended to the URL so the
/// web app can auto-authenticate without asking for Google login again.
/// The web app should verify the token via GET /api/PlayerProfile before
/// trusting the session.
/// </summary>
public class TopUpManager : MonoBehaviour
{
    [Header("Panel")]
    [SerializeField] private GameObject panel;
    [SerializeField] private Button closeButton;

    [Header("Currency Display")]
    [SerializeField] private TMP_Text gemBalanceText;

    [Header("Web Portal")]
    [Tooltip("URL of the deployed Vercel top-up web app.")]
    [SerializeField] private string topUpWebUrl = "https://your-topup-site.vercel.app";

    [Tooltip("When enabled, appends the player's JWT as ?token=<jwt> so the web app can skip the Google login step.")]
    [SerializeField] private bool appendAuthToken = true;

    [Header("Package Buttons (optional)")]
    [Tooltip("Assign package buttons in order. Each button opens the web portal with the matching package query param.")]
    [SerializeField] private Button[] packageButtons;
    [Tooltip("Package IDs passed as ?package=<id> to the web portal. Must match packageButtons array length.")]
    [SerializeField] private string[] packageIds;

    // ── Lifecycle ────────────────────────────────────────────────────────────

    void Awake()
    {
        if (closeButton != null)
            closeButton.onClick.AddListener(Close);

        // Wire individual package buttons
        for (int i = 0; i < packageButtons.Length; i++)
        {
            if (packageButtons[i] == null) continue;

            int idx = i; // capture for lambda closure
            packageButtons[i].onClick.AddListener(() => OpenWebPortal(idx));
        }

        HidePanel();
    }

    void OnDestroy()
    {
        if (closeButton != null)
            closeButton.onClick.RemoveListener(Close);

        for (int i = 0; i < packageButtons.Length; i++)
        {
            if (packageButtons[i] != null)
                packageButtons[i].onClick.RemoveAllListeners();
        }
    }

    // ── Public API (called from MainMenuManager or UI buttons) ───────────────

    public void Open()
    {
        RefreshGemBalance();

        if (panel != null)
            panel.SetActive(true);
    }

    public void Close()
    {
        HidePanel();
    }

    // ── Web Portal ───────────────────────────────────────────────────────────

    /// <summary>
    /// Opens the top-up web portal without a specific package pre-selected.
    /// Called by the "Go to Top-Up Site" button.
    /// </summary>
    public void OpenWebPortalDefault()
    {
        OpenUrl(BuildUrl(null));
    }

    /// <summary>
    /// Opens the web portal with a specific package pre-selected via query param.
    /// </summary>
    /// <param name="packageIndex">Index into the packageIds array.</param>
    public void OpenWebPortal(int packageIndex)
    {
        bool hasPackageId = packageIds != null
                            && packageIndex >= 0
                            && packageIndex < packageIds.Length
                            && !string.IsNullOrEmpty(packageIds[packageIndex]);

        string packageId = hasPackageId ? packageIds[packageIndex] : null;
        OpenUrl(BuildUrl(packageId));
    }

    // ── URL Construction ─────────────────────────────────────────────────────

    /// <summary>
    /// Builds the final URL with optional package and auth token query params.
    /// </summary>
    /// <param name="packageId">
    /// If non-null, appended as ?package=<id> (or &amp;package=<id>).
    /// </param>
    string BuildUrl(string packageId)
    {
        if (string.IsNullOrWhiteSpace(topUpWebUrl))
        {
            Debug.LogWarning("[TopUpManager] Top-up URL is not configured.");
            return string.Empty;
        }

        // Determine the separator for the first query param
        bool alreadyHasQuery = topUpWebUrl.Contains("?");
        char sep = alreadyHasQuery ? '&' : '?';

        string url = topUpWebUrl;

        // Append auth token for SSO so the web app can skip Google login
        if (appendAuthToken && !string.IsNullOrEmpty(PlayerSession.Token))
        {
            url += $"{sep}token={UnityEngine.Networking.UnityWebRequest.EscapeURL(PlayerSession.Token)}";
            sep = '&'; // all subsequent params use &
        }

        // Append package id if provided
        if (!string.IsNullOrEmpty(packageId))
        {
            url += $"{sep}package={UnityEngine.Networking.UnityWebRequest.EscapeURL(packageId)}";
        }

        return url;
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    void RefreshGemBalance()
    {
        if (gemBalanceText == null) return;

        // Try to read gems from the shared "Gem" GameObject that MainMenuManager uses
        var gemObj = GameObject.Find("Gem");
        if (gemObj != null)
        {
            var tmp = gemObj.GetComponentInChildren<TMP_Text>();
            if (tmp != null)
            {
                gemBalanceText.text = tmp.text;
                return;
            }
        }

        // Fallback when gem display GameObject is not found
        gemBalanceText.text = "—";
    }

    void HidePanel()
    {
        if (panel != null)
            panel.SetActive(false);
    }

    static void OpenUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return;

        Debug.Log($"[TopUpManager] Opening portal.");  // token không log ra để tránh lộ
        Application.OpenURL(url);
    }
}
