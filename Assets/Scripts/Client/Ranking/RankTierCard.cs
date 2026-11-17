using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Prefab script for a single rank tier card in the horizontal scroll popup.
/// Displays: rank badge, rank name, and min score required.
/// The lock overlay hides once the viewed player has reached this tier.
/// </summary>
public class RankTierCard : MonoBehaviour
{
    [Header("UI Elements")]
    [SerializeField] private Image    rankBadgeImage;     // Rank badge icon (large display)
    [SerializeField] private TMP_Text rankNameText;       // "Beginner", "Master", "Pactkeeper", etc.
    [SerializeField] private TMP_Text minScoreText;       // "Required: 3000 points"
    [SerializeField] private GameObject loreLockedOverlay;

    public void Setup(RankingManager.RankTierDisplayData data, int playerRankTier)
    {
        if (rankBadgeImage != null && data.badgeSprite != null)
            rankBadgeImage.sprite = data.badgeSprite;

        if (rankNameText != null)
            rankNameText.text = data.rankName;

        if (minScoreText != null)
        {
            if (data.tier == -1)
                minScoreText.text = "No requirement";
            else
                minScoreText.text = $"Required: {data.minScore}+ points";
        }

        bool unlocked = data.tier <= playerRankTier;
        if (loreLockedOverlay != null)
            loreLockedOverlay.SetActive(!unlocked);
    }
}
