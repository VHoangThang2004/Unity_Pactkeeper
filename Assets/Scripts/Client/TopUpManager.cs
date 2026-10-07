using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Manages the Top-Up panel in MainMenu.
/// Opens an in-game overlay that displays top-up package options
/// and lets the player open the web top-up portal in their browser.
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
    /// Opens the top-up web portal directly (no package pre-selection).
    /// Called by the "Go to Top-Up Site" button.
    /// </summary>
    public void OpenWebPortalDefault()
    {
        OpenUrl(topUpWebUrl);
    }

    /// <summary>
    /// Opens the web portal with a specific package pre-selected via query param.
    /// </summary>
    /// <param name="packageIndex">Index into the packageIds array.</param>
    public void OpenWebPortal(int packageIndex)
    {
        if (packageIds == null || packageIndex < 0 || packageIndex >= packageIds.Length)
        {
            OpenUrl(topUpWebUrl);
            return;
        }

        string url = $"{topUpWebUrl}?package={packageIds[packageIndex]}";
        OpenUrl(url);
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

        // Fallback: use PlayerSession if the gem count is cached there
        // gemBalanceText.text = PlayerSession.Gems.ToString();
        gemBalanceText.text = "—";
    }

    void HidePanel()
    {
        if (panel != null)
            panel.SetActive(false);
    }

    static void OpenUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            Debug.LogWarning("[TopUpManager] Top-up URL is not configured.");
            return;
        }

        Debug.Log($"[TopUpManager] Opening: {url}");
        Application.OpenURL(url);
    }
}
