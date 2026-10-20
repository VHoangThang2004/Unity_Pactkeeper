using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Manages the match mode selection panel that appears when the player
/// presses the BATTLE button.
///
/// Layout:
///   ┌─────────────────────────────────────────┐
///   │  [Random]   Random Matching Opponent    │
///   │  [Challenge] Choose an Opponent         │
///   │  [Invite]   Invite a Friend to play     │
///   └─────────────────────────────────────────┘
///
/// Close behaviour: clicking anywhere on the dark backdrop (outside the
/// inner panel) dismisses the popup — same UX as mobile bottom-sheets.
///
/// Setup (Inspector):
///   rootPanel         — the full-screen semi-transparent backdrop
///                        (Image with alpha ~0.4, Raycast Target ON)
///   innerPanel        — the decorated card in the centre
///   randomButton      — calls MatchmakingManager.OnClickFindMatch()
///   challengeButton   — opens Friend Tab (Challenge tab)
///   inviteButton      — opens Friend Tab (Invite / Friend list)
/// </summary>
public class MatchmodeMenuManager : MonoBehaviour
{
    [Header("Panels")]
    [SerializeField] private GameObject rootPanel;   // full-screen backdrop, click to close
    [SerializeField] private GameObject innerPanel;  // the decorated menu card

    [Header("Mode Buttons")]
    [SerializeField] private Button randomButton;    // Random matchmaking
    [SerializeField] private Button challengeButton; // Challenge a specific opponent
    [SerializeField] private Button inviteButton;    // Invite a friend

    [Header("References")]
    [SerializeField] private MatchmakingManager matchmakingManager;
    [SerializeField] private FriendTabManager   friendTabManager;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    void Awake()
    {
        // Mode buttons
        if (randomButton    != null) randomButton.onClick.AddListener(OnRandomClicked);
        if (challengeButton != null) challengeButton.onClick.AddListener(OnChallengeClicked);
        if (inviteButton    != null) inviteButton.onClick.AddListener(OnInviteClicked);

        // Backdrop click-to-close
        // We attach a Button to the rootPanel itself so any click that
        // does NOT hit innerPanel (which blocks raycasts) will bubble up
        // to the backdrop and close the panel.
        if (rootPanel != null)
        {
            var backdropBtn = rootPanel.GetComponent<Button>();
            if (backdropBtn == null) backdropBtn = rootPanel.AddComponent<Button>();

            // Make the button invisible — no transition, no sprite changes
            backdropBtn.transition = Selectable.Transition.None;
            backdropBtn.onClick.AddListener(Close);
        }

        // Start hidden
        if (rootPanel != null) rootPanel.SetActive(false);
    }

    void OnDestroy()
    {
        if (randomButton    != null) randomButton.onClick.RemoveListener(OnRandomClicked);
        if (challengeButton != null) challengeButton.onClick.RemoveListener(OnChallengeClicked);
        if (inviteButton    != null) inviteButton.onClick.RemoveListener(OnInviteClicked);

        if (rootPanel != null)
        {
            var backdropBtn = rootPanel.GetComponent<Button>();
            if (backdropBtn != null) backdropBtn.onClick.RemoveListener(Close);
        }
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>Called by the BATTLE button in MainMenu.</summary>
    public void Open()
    {
        if (rootPanel != null) rootPanel.SetActive(true);
    }

    /// <summary>Hide the panel without triggering any mode.</summary>
    public void Close()
    {
        if (rootPanel != null) rootPanel.SetActive(false);
    }

    // ── Button handlers ───────────────────────────────────────────────────────

    void OnRandomClicked()
    {
        Close();
        matchmakingManager?.OnClickFindMatch();
    }

    void OnChallengeClicked()
    {
        // Open Friend Tab — player picks a friend to challenge from there
        Close();
        friendTabManager?.Open();
    }

    void OnInviteClicked()
    {
        // Open Friend Tab to the invite / friend list view
        Close();
        friendTabManager?.Open();
    }
}
