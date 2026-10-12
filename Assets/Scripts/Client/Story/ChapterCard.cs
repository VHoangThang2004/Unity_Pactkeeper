using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Prefab script for a single chapter card in the horizontal scroll list.
/// Displays chapter thumbnail, title, and status badge.
/// Clicking opens the detail popup via StoryProgressManager.
/// </summary>
public class ChapterCard : MonoBehaviour
{
    [Header("UI Elements")]
    [SerializeField] private Image     thumbnailImage;       // Chapter artwork / cover image
    [SerializeField] private TMP_Text  chapterTitleText;     // "Chapter 0 — The First Pact"
    [SerializeField] private GameObject statusBadge;         // Root of the status badge
    [SerializeField] private TMP_Text  statusBadgeText;      // "COMPLETED" / "IN PROGRESS" / "NOT STARTED"
    [SerializeField] private Image     statusBadgeBackground;

    [Header("Status Colors")]
    [SerializeField] private Color colorCompleted   = new Color(0.18f, 0.72f, 0.35f); // green
    [SerializeField] private Color colorInProgress  = new Color(0.93f, 0.65f, 0.10f); // amber
    [SerializeField] private Color colorNotStarted  = new Color(0.45f, 0.45f, 0.45f); // grey

    [Header("Lock Overlay")]
    [SerializeField] private GameObject lockOverlay; // shown when chapter is not yet reachable

    // ── Runtime ───────────────────────────────────────────────────────────────

    private StoryProgressManager.ChapterDisplayData data;
    private StoryProgressManager manager;

    // ── Setup ─────────────────────────────────────────────────────────────────

    public void Setup(StoryProgressManager.ChapterDisplayData chapterData,
                      StoryProgressManager progressManager)
    {
        data    = chapterData;
        manager = progressManager;

        // Title — show "Coming Soon" for placeholder chapters
        if (chapterTitleText != null)
        {
            if (chapterData.config == null)
                chapterTitleText.text = $"Chapter {chapterData.chapterId} — Coming Soon";
            else
                chapterTitleText.text = chapterData.config.title;
        }

        // Status badge
        ApplyStatus(chapterData.status);

        // Thumbnail — use chapter-specific sprite if supplied, else keep default
        if (thumbnailImage != null && chapterData.thumbnail != null)
            thumbnailImage.sprite = chapterData.thumbnail;

        // Lock overlay — show for chapters the player hasn't reached yet
        bool locked = chapterData.status == StoryProgressManager.ChapterStatus.Locked;
        if (lockOverlay != null) lockOverlay.SetActive(locked);
    }

    // ── Click ─────────────────────────────────────────────────────────────────

    public void OnClick()
    {
        if (data == null || manager == null) return;
        manager.OnChapterCardClicked(data);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    void ApplyStatus(StoryProgressManager.ChapterStatus status)
    {
        if (statusBadge != null)
            statusBadge.SetActive(status != StoryProgressManager.ChapterStatus.Locked);

        string label;
        Color  color;

        switch (status)
        {
            case StoryProgressManager.ChapterStatus.Completed:
                label = "COMPLETED";
                color = colorCompleted;
                break;
            case StoryProgressManager.ChapterStatus.InProgress:
                label = "IN PROGRESS";
                color = colorInProgress;
                break;
            case StoryProgressManager.ChapterStatus.Locked:
                label = "";
                color = colorNotStarted;
                break;
            default: // NotStarted
                label = "NOT STARTED";
                color = colorNotStarted;
                break;
        }

        if (statusBadgeText       != null) statusBadgeText.text = label;
        if (statusBadgeBackground != null) statusBadgeBackground.color = color;
    }
}
