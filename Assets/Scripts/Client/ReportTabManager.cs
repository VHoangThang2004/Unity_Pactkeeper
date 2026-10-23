using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Manages the Report panel in MainMenu.
///
/// Layout:
///   Panel (root)
///   ├── ButtonPanel
///   │   ├── ReportBugButton
///   │   ├── ReportPlayerButton
///   │   ├── TermsOfServiceButton
///   │   ├── PrivacyPolicyButton
///   │   └── AntiCheatPolicyButton
///   └── ContentPanel (inactive by default)
///       ├── TitleText
///       ├── ContentText (ScrollRect or plain TMP)
///       └── CloseContentButton
/// </summary>
public class ReportTabManager : MonoBehaviour
{
    // ── Inspector ─────────────────────────────────────────────────────────────

    [Header("Root Panel")]
    [SerializeField] private GameObject panel;
    [SerializeField] private Button     closeButton;    // optional — đặt nếu muốn nút X riêng
    // Backdrop click-to-close được add runtime vào panel (không cần tạo trong Editor)

    [Header("Button Panel — Policy Buttons")]
    [SerializeField] private Button termsOfServiceButton;
    [SerializeField] private Button privacyPolicyButton;
    [SerializeField] private Button antiCheatPolicyButton;

    [Header("Button Panel — Report Buttons (API pending)")]
    [SerializeField] private Button reportBugButton;
    [SerializeField] private Button reportPlayerButton;

    [Header("Content Panel")]
    [SerializeField] private GameObject contentPanel;
    [SerializeField] private TMP_Text   contentTitleText;
    [SerializeField] private TMP_Text   contentBodyText;
    [SerializeField] private Button     closeContentButton;
    // Backdrop click-to-close content được add runtime vào contentPanel

    // ── Policy Content (editable in Inspector) ────────────────────────────────

    [Header("Terms of Service")]
    [TextArea(6, 20)]
    [SerializeField] private string termsOfServiceContent =
        "Pactkeeper — Terms of Service\n\n" +
        "By playing Pactkeeper, you agree to the following:\n\n" +
        "• Do not use cheats, hacks, or exploits.\n" +
        "• Do not modify or interfere with client or game files.\n" +
        "• Do not use third-party software to gain an unfair advantage.\n" +
        "• Do not exploit bugs for personal gain.\n" +
        "• Do not intentionally abuse matchmaking, gacha, ranking, or inventory systems.\n" +
        "• Do not impersonate or hijack another player's account.\n" +
        "• Do not engage in fraudulent behavior during PvP.\n" +
        "• Do not intentionally disconnect or abuse disconnect-detection systems.\n" +
        "• Do not spam or abuse the Report feature.\n\n" +
        "Pactkeeper reserves the right to investigate suspected violations and take " +
        "appropriate action against accounts that violate these rules, up to and " +
        "including permanent suspension.";

    [Header("Privacy Policy")]
    [TextArea(6, 20)]
    [SerializeField] private string privacyPolicyContent =
        "Pactkeeper — Privacy Policy\n\n" +
        "We collect and store the following data to operate the game:\n\n" +
        "Account & Identity\n" +
        "• Account ID, Username, Email address\n" +
        "• Google / Steam linked ID (if applicable)\n\n" +
        "Session & Network\n" +
        "• IP address and login timestamps\n" +
        "• Device information (OS, hardware model)\n\n" +
        "Gameplay\n" +
        "• Match history and results\n" +
        "• Ranking and progression data\n" +
        "• Gacha and inventory records\n\n" +
        "Moderation\n" +
        "• Report history (submitted and received)\n" +
        "• Anti-cheat logs and detected anomalies\n\n" +
        "Your data is used solely to operate, maintain, and improve Pactkeeper. " +
        "We do not sell your personal information to third parties. " +
        "Data may be retained for moderation purposes after account deletion.";

    [Header("Anti-Cheat & Fair Play Policy")]
    [TextArea(6, 20)]
    [SerializeField] private string antiCheatPolicyContent =
        "Pactkeeper — Fair Play & Anti-Cheat Policy\n\n" +
        "Prohibited Behaviors\n\n" +
        "Cheating\n" +
        "Speed hacks, damage modification, or any client-side manipulation.\n\n" +
        "Exploiting\n" +
        "Intentionally triggering known bugs for personal gain.\n\n" +
        "Unauthorized Software\n" +
        "Any tool that reads, writes, or interferes with game memory or network traffic.\n\n" +
        "Account Abuse\n" +
        "Sharing, selling, or hijacking accounts.\n\n" +
        "Match Manipulation\n" +
        "Intentionally throwing matches or colluding with opponents.\n\n" +
        "Ranking Abuse\n" +
        "Exploiting bugs or loopholes to gain ranking points unfairly.\n\n" +
        "Report Abuse\n" +
        "Submitting false or mass reports against innocent players.\n\n" +
        "──────────────────────────\n" +
        "Enforcement\n\n" +
        "Minor violation\n" +
        "→ Written warning\n\n" +
        "Repeated violation\n" +
        "→ Temporary suspension\n\n" +
        "Serious cheating / hack\n" +
        "→ Permanent ban\n\n" +
        "Competitive / ranking abuse\n" +
        "→ Ranking points removed, match results invalidated, temporary ban";

    // ── Policy titles (fixed) ─────────────────────────────────────────────────

    private const string TitleTerms     = "Terms of Service";
    private const string TitlePrivacy   = "Privacy Policy";
    private const string TitleAntiCheat = "Anti-Cheat & Fair Play Policy";

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    void Awake()
    {
        // ── Backdrop: add Button thẳng vào rootPanel (giống MatchmodeMenuManager)
        // Click vào vùng ngoài ButtonArea/ContentPanel → Close()
        // ButtonArea và ContentPanel block raycast nên click vào đó không trigger
        if (panel != null)
        {
            var backdropBtn = panel.GetComponent<Button>();
            if (backdropBtn == null) backdropBtn = panel.AddComponent<Button>();
            backdropBtn.transition = UnityEngine.UI.Selectable.Transition.None;
            backdropBtn.onClick.AddListener(Close);
        }

        // ── Backdrop: add Button vào contentPanel để click ngoài card đóng content
        if (contentPanel != null)
        {
            var contentBackdropBtn = contentPanel.GetComponent<Button>();
            if (contentBackdropBtn == null) contentBackdropBtn = contentPanel.AddComponent<Button>();
            contentBackdropBtn.transition = UnityEngine.UI.Selectable.Transition.None;
            contentBackdropBtn.onClick.AddListener(CloseContent);
        }

        // Optional X button
        if (closeButton           != null) closeButton.onClick.AddListener(Close);
        if (closeContentButton    != null) closeContentButton.onClick.AddListener(CloseContent);

        if (termsOfServiceButton  != null) termsOfServiceButton.onClick.AddListener(OpenTermsOfService);
        if (privacyPolicyButton   != null) privacyPolicyButton.onClick.AddListener(OpenPrivacyPolicy);
        if (antiCheatPolicyButton != null) antiCheatPolicyButton.onClick.AddListener(OpenAntiCheatPolicy);

        if (reportBugButton       != null) reportBugButton.onClick.AddListener(OnReportBugClicked);
        if (reportPlayerButton    != null) reportPlayerButton.onClick.AddListener(OnReportPlayerClicked);

        HidePanel();
        CloseContent();
    }

    void OnDestroy()
    {
        if (panel != null)
        {
            var backdropBtn = panel.GetComponent<Button>();
            if (backdropBtn != null) backdropBtn.onClick.RemoveListener(Close);
        }

        if (contentPanel != null)
        {
            var contentBackdropBtn = contentPanel.GetComponent<Button>();
            if (contentBackdropBtn != null) contentBackdropBtn.onClick.RemoveListener(CloseContent);
        }

        if (closeButton           != null) closeButton.onClick.RemoveListener(Close);
        if (closeContentButton    != null) closeContentButton.onClick.RemoveListener(CloseContent);

        if (termsOfServiceButton  != null) termsOfServiceButton.onClick.RemoveListener(OpenTermsOfService);
        if (privacyPolicyButton   != null) privacyPolicyButton.onClick.RemoveListener(OpenPrivacyPolicy);
        if (antiCheatPolicyButton != null) antiCheatPolicyButton.onClick.RemoveListener(OpenAntiCheatPolicy);

        if (reportBugButton       != null) reportBugButton.onClick.RemoveListener(OnReportBugClicked);
        if (reportPlayerButton    != null) reportPlayerButton.onClick.RemoveListener(OnReportPlayerClicked);
    }

    // ── Public API ────────────────────────────────────────────────────────────

    public void Open()
    {
        CloseContent(); // reset content panel on every open
        this.OpenPanel(panel);
    }

    public void Close()
    {
        CloseContent();
        this.ClosePanel(panel);
    }

    void HidePanel()
    {
        this.HidePanelImmediate(panel);
    }

    // ── Policy content buttons ────────────────────────────────────────────────

    void OpenTermsOfService()
    {
        ShowContent(TitleTerms, termsOfServiceContent);
    }

    void OpenPrivacyPolicy()
    {
        ShowContent(TitlePrivacy, privacyPolicyContent);
    }

    void OpenAntiCheatPolicy()
    {
        ShowContent(TitleAntiCheat, antiCheatPolicyContent);
    }

    void ShowContent(string title, string body)
    {
        if (contentTitleText != null) contentTitleText.text = title;
        if (contentBodyText  != null) contentBodyText.text  = body;

        if (contentPanel != null) contentPanel.SetActive(true);
    }

    void CloseContent()
    {
        if (contentPanel != null) contentPanel.SetActive(false);
    }

    // ── Report buttons (API pending) ──────────────────────────────────────────

    void OnReportBugClicked()
    {
        // TODO: implement when backend API is ready
        Debug.Log("[ReportTab] Report Bug clicked — API not yet implemented.");
    }

    void OnReportPlayerClicked()
    {
        // TODO: implement when backend API is ready
        Debug.Log("[ReportTab] Report Player clicked — API not yet implemented.");
    }
}
