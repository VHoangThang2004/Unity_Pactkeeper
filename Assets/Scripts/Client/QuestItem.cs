using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Represents a single quest entry in the left-panel list of QuestManager.
/// Pattern mirrors NotificationItem / InventoryItem.
/// </summary>
public class QuestItem : MonoBehaviour
{
    [Header("UI Elements")]
    [SerializeField] private TMP_Text questNameText;
    [SerializeField] private TMP_Text progressText;       // e.g. "2 / 5"
    [SerializeField] private Image statusIcon;            // optional dot/badge
    [SerializeField] private Image selectedBackground;

    [Header("Status Colors")]
    [SerializeField] private Color colorInProgress  = new Color(0.9f, 0.9f, 0.9f);   // white-ish
    [SerializeField] private Color colorCompleted   = new Color(0.95f, 0.25f, 0.25f); // bright red — completed, not yet claimed
    [SerializeField] private Color colorClaimed     = new Color(0.2f, 0.85f, 0.2f);   // bright green — claimed

    // ── Runtime data ──────────────────────────────────────────────────────────
    private QuestManager.QuestDisplayData data;
    private QuestManager manager;

    // ── Setup ─────────────────────────────────────────────────────────────────

    public void Setup(QuestManager.QuestDisplayData questData, QuestManager questManager)
    {
        data    = questData;
        manager = questManager;

        // Quest name: "Quest #3" using the order number assigned by QuestManager
        if (questNameText != null)
        {
            int num = questManager.GetQuestNumber(questData.rule.id);
            questNameText.text = num > 0 ? $"Quest #{num}" : "Quest";
        }

        // Progress "currentCount / targetCount"
        if (progressText != null)
        {
            int current = questData.progress?.currentCount ?? 0;
            progressText.text = $"{current} / {questData.rule.targetCount}";
        }

        // Status colour on the name label
        if (questNameText != null)
            questNameText.color = GetStatusColor(questData);

        SetSelected(false);
    }

    // ── Click ─────────────────────────────────────────────────────────────────

    public void OnClick()
    {
        if (data == null || manager == null) return;
        manager.OnQuestSelected(this, data);
    }

    // ── Selection highlight ───────────────────────────────────────────────────

    public void SetSelected(bool isSelected)
    {
        if (selectedBackground != null)
            selectedBackground.gameObject.SetActive(isSelected);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    Color GetStatusColor(QuestManager.QuestDisplayData questData)
    {
        if (questData.progress == null)              return colorInProgress;
        if (questData.progress.isReceived)           return colorClaimed;
        if (questData.progress.isCompleted)          return colorCompleted;
        return colorInProgress;
    }
}
