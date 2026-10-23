using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Match mode selection popup — hiện khi player bấm BATTLE.
/// Đóng khi click backdrop (vùng ngoài innerPanel).
///
/// Setup Inspector:
///   rootPanel  — full-screen backdrop (Image, Raycast Target ON, PanelAnimator attached)
///   innerPanel — decorated card (Image, Raycast Target ON)
///   3 buttons  — Random / Challenge / Invite bên trong innerPanel
///
/// MatchmakingManager để trống → tự GetComponent trên cùng GO.
/// FriendTabManager   để trống → tự FindFirstObjectByType trong scene.
/// </summary>
public class MatchmodeMenuManager : MonoBehaviour
{
    [Header("Panels")]
    [SerializeField] private GameObject rootPanel;
    [SerializeField] private GameObject innerPanel;

    [Header("Mode Buttons")]
    [SerializeField] private Button randomButton;
    [SerializeField] private Button challengeButton;
    [SerializeField] private Button inviteButton;

    [Header("References (để trống nếu cùng GameObject)")]
    [SerializeField] private MatchmakingManager matchmakingManager;
    [SerializeField] private FriendTabManager   friendTabManager;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    void Awake()
    {
        // Resolve references
        if (matchmakingManager == null)
            matchmakingManager = GetComponent<MatchmakingManager>();

        if (friendTabManager == null)
            friendTabManager = FindFirstObjectByType<FriendTabManager>();

        // Buttons
        if (randomButton    != null) randomButton.onClick.AddListener(OnRandomClicked);
        if (challengeButton != null) challengeButton.onClick.AddListener(OnChallengeClicked);
        if (inviteButton    != null) inviteButton.onClick.AddListener(OnInviteClicked);

        // Backdrop — add Button vào rootPanel, click ngoài innerPanel → Close
        if (rootPanel != null)
        {
            var btn = rootPanel.GetComponent<Button>() ?? rootPanel.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.onClick.AddListener(Close);
        }

        // Ẩn ngay lập tức — dùng HidePanelImmediate để GO vẫn active
        // (cần GO active để PanelAnimator.Open() gọi StartCoroutine được)
        HidePanel();
    }

    void OnDestroy()
    {
        if (randomButton    != null) randomButton.onClick.RemoveListener(OnRandomClicked);
        if (challengeButton != null) challengeButton.onClick.RemoveListener(OnChallengeClicked);
        if (inviteButton    != null) inviteButton.onClick.RemoveListener(OnInviteClicked);

        if (rootPanel != null)
        {
            var btn = rootPanel.GetComponent<Button>();
            if (btn != null) btn.onClick.RemoveListener(Close);
        }
    }

    // ── Public API ────────────────────────────────────────────────────────────

    public void Open()
    {
        this.OpenPanel(rootPanel);
    }

    public void Close()
    {
        this.ClosePanel(rootPanel);
    }

    void HidePanel()
    {
        this.HidePanelImmediate(rootPanel);
    }

    // ── Button handlers ───────────────────────────────────────────────────────

    void OnRandomClicked()
    {
        Close();
        matchmakingManager?.OnClickFindMatch();
    }

    void OnChallengeClicked()
    {
        Close();
        friendTabManager?.Open();
    }

    void OnInviteClicked()
    {
        Close();
        friendTabManager?.Open();
    }
}
