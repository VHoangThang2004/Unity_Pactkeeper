using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Prefab script for a single rank tier card in the horizontal scroll popup.
/// Displays: rank badge, rank name, min score required, and lore description.
/// Lore is hidden until player completes story.
/// </summary>
public class RankTierCard : MonoBehaviour
{
    [Header("UI Elements")]
    [SerializeField] private Image    rankBadgeImage;     // Rank badge icon (large display)
    [SerializeField] private TMP_Text rankNameText;       // "Beginner", "Master", "Pactkeeper", etc.
    [SerializeField] private TMP_Text minScoreText;       // "Required: 3000 points"
    [SerializeField] private TMP_Text loreDescriptionText; // Story description
    [SerializeField] private GameObject loreLockedOverlay; // Shown when story not completed

    [Header("Visual Settings")]
    [SerializeField] private Color lockedTextColor = new Color(0.5f, 0.5f, 0.5f); // Grey for locked
    [SerializeField] private Color unlockedTextColor = Color.white;

    // ── Setup ─────────────────────────────────────────────────────────────────

    public void Setup(RankingManager.RankTierDisplayData data, bool hasCompletedStory)
    {
        // Rank badge
        if (rankBadgeImage != null && data.badgeSprite != null)
            rankBadgeImage.sprite = data.badgeSprite;

        // Rank name
        if (rankNameText != null)
            rankNameText.text = data.rankName;

        // Min score requirement
        if (minScoreText != null)
        {
            if (data.tier == -1) // Unranked
                minScoreText.text = "No requirement";
            else
                minScoreText.text = $"Required: {data.minScore}+ points";
        }

        // Lore description (locked until story completed)
        bool loreLocked = !hasCompletedStory && data.tier >= 0; // Unranked lore is always visible

        if (loreLockedOverlay != null)
            loreLockedOverlay.SetActive(loreLocked);

        if (loreDescriptionText != null)
        {
            if (loreLocked)
            {
                loreDescriptionText.text = "??? Complete the story to unlock lore ???";
                loreDescriptionText.color = lockedTextColor;
            }
            else
            {
                loreDescriptionText.text = data.loreDescription;
                loreDescriptionText.color = unlockedTextColor;
            }
        }
    }
}
