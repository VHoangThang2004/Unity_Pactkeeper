using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Prefab script for a single chapter card in the horizontal scroll list.
/// Displays chapter thumbnail, title, status badge, and an expandable Accordion Dropdown
/// listing all scenes in the chapter ("[Chapter.Scene] - [English Title]").
/// </summary>
public class ChapterCard : MonoBehaviour
{
    [Header("UI Elements")]
    [SerializeField] private Image     thumbnailImage;       // Chapter artwork / cover image
    [SerializeField] private TMP_Text  chapterTitleText;     // "Chapter 0 - The Covenant of Time"
    [SerializeField] private GameObject statusBadge;         // Root of the status badge
    [SerializeField] private TMP_Text  statusBadgeText;      // "COMPLETED" / "IN PROGRESS" / "NOT STARTED"
    [SerializeField] private Image     statusBadgeBackground;

    [Header("Accordion Dropdown Elements")]
    [SerializeField] private RectTransform dropdownContainer; // Parent with VerticalLayoutGroup
    [SerializeField] private GameObject    sceneDropdownItemPrefab;
    [SerializeField] private RectTransform dropdownArrowIcon; // Rotates 180 deg when expanded

    [Header("Status Colors")]
    [SerializeField] private Color colorCompleted   = new Color(0.18f, 0.72f, 0.35f); // green
    [SerializeField] private Color colorInProgress  = new Color(0.93f, 0.65f, 0.10f); // amber
    [SerializeField] private Color colorNotStarted  = new Color(0.45f, 0.45f, 0.45f); // grey

    [Header("Lock Overlay")]
    [SerializeField] private GameObject lockOverlay; // shown when chapter is not yet reachable

    // ── Runtime ───────────────────────────────────────────────────────────────

    private StoryProgressManager.ChapterDisplayData data;
    private StoryProgressManager manager;
    private bool _isExpanded = false;
    private readonly List<GameObject> _spawnedSceneItems = new();

    // ── Setup ─────────────────────────────────────────────────────────────────

    public void Setup(StoryProgressManager.ChapterDisplayData chapterData, StoryProgressManager owner)
    {
        data    = chapterData;
        manager = owner;

        // Thumbnail
        if (thumbnailImage != null && chapterData.thumbnail != null)
            thumbnailImage.sprite = chapterData.thumbnail;

        // Title - fall back to generic name for coming soon chapters
        if (chapterTitleText != null)
        {
            chapterTitleText.text = chapterData.config != null && !string.IsNullOrEmpty(chapterData.config.title)
                ? chapterData.config.title
                : $"Chapter {chapterData.chapterId} - Coming Soon";
        }

        // Status badge
        ApplyStatus(chapterData.status);

        // Lock overlay - show for chapters the player hasn't reached yet
        bool locked = chapterData.status == StoryProgressManager.ChapterStatus.Locked;
        if (lockOverlay != null) lockOverlay.SetActive(locked);

        BuildSceneDropdownItems();
        SetDropdownExpanded(false);
    }

    private void BuildSceneDropdownItems()
    {
        foreach (var go in _spawnedSceneItems)
        {
            if (go != null) Destroy(go);
        }
        _spawnedSceneItems.Clear();

        if (dropdownContainer == null || sceneDropdownItemPrefab == null || data?.config?.scenes == null)
            return;

        bool isChapterCompleted = data.status == StoryProgressManager.ChapterStatus.Completed ||
                                  (data.progress != null && data.progress.isChapterCompleted);

        bool isChapterUnlocked = data.status != StoryProgressManager.ChapterStatus.Locked;

        int firstSceneId = data.config.scenes.Length > 0 ? data.config.scenes[0].sceneId : 0;
        int unlockedSceneHighWaterMark = firstSceneId;

        if (data.progress != null)
        {
            unlockedSceneHighWaterMark = data.progress.isCompleted
                ? data.progress.sceneId + 1
                : data.progress.sceneId;
        }

        foreach (var scene in data.config.scenes)
        {
            var itemObj = Instantiate(sceneDropdownItemPrefab, dropdownContainer);
            _spawnedSceneItems.Add(itemObj);

            var itemUI = itemObj.GetComponent<SceneDropdownItemUI>();
            if (itemUI == null)
                itemUI = itemObj.AddComponent<SceneDropdownItemUI>();

            string englishLabel = !string.IsNullOrEmpty(scene.description)
                ? scene.description
                : $"{data.chapterId}.{scene.sceneId} - {scene.type}";

            bool sceneCompleted = isChapterCompleted ||
                                  (data.progress != null &&
                                   (data.progress.sceneId > scene.sceneId ||
                                    (data.progress.sceneId == scene.sceneId && data.progress.isCompleted)));

            bool sceneUnlocked = isChapterUnlocked &&
                                 (isChapterCompleted || scene.sceneId <= unlockedSceneHighWaterMark);

            int capturedSceneId = scene.sceneId;
            itemUI.Setup(
                data.chapterId,
                capturedSceneId,
                englishLabel,
                sceneCompleted,
                sceneUnlocked,
                (chId, scId) => manager.OnSceneSelectedFromDropdown(chId, scId));
        }
    }

    // ── Click ─────────────────────────────────────────────────────────────────

    public void OnClick()
    {
        if (data == null || manager == null) return;
        if (data.config == null || data.status == StoryProgressManager.ChapterStatus.Locked)
            return;

        if (dropdownContainer != null && !manager.HasPopupPanel)
            SetDropdownExpanded(!_isExpanded);

        manager.OnChapterCardClicked(data);
    }

    public void SetDropdownExpanded(bool expanded)
    {
        _isExpanded = expanded;
        if (dropdownContainer != null)
        {
            dropdownContainer.gameObject.SetActive(_isExpanded);
            LayoutRebuilder.ForceRebuildLayoutImmediate(transform as RectTransform);
            if (transform.parent is RectTransform parentRect)
                LayoutRebuilder.ForceRebuildLayoutImmediate(parentRect);
        }

        if (dropdownArrowIcon != null)
            dropdownArrowIcon.localRotation = Quaternion.Euler(0f, 0f, _isExpanded ? 180f : 0f);
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
