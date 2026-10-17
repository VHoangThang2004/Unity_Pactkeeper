using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Prefab script for a single leaderboard entry in the ranking list.
/// Displays: rank position, username, level, rank badge icon, and score.
/// Clicking shows player detail in the right panel via RankingManager.
/// </summary>
public class LeaderboardEntryCard : MonoBehaviour
{
    [Header("UI Elements")]
    [SerializeField] private TMP_Text rankPositionText;  // "1", "2", "3", etc.
    [SerializeField] private TMP_Text usernameText;      // Player username
    [SerializeField] private TMP_Text levelText;         // "Lv 12"
    [SerializeField] private Image    rankBadgeImage;    // Sprite tương ứng với rank tier (7 bậc + Unranked)
    [SerializeField] private TMP_Text rankNameText;      // "Beginner", "Master", "Pactkeeper", etc.
    [SerializeField] private TMP_Text scoreText;         // Điểm số (rating/MMR)

    [Header("Visual Feedback")]
    [SerializeField] private Image    backgroundImage;   // Card background
    [SerializeField] private Color    normalColor = new Color(0.2f, 0.2f, 0.2f, 0.8f);
    [SerializeField] private Color    highlightColor = new Color(0.3f, 0.3f, 0.4f, 0.9f);
    [SerializeField] private Color    topThreeColor = new Color(0.4f, 0.3f, 0.1f, 0.9f); // Gold tint for top 3

    // ── Runtime ───────────────────────────────────────────────────────────────

    private RankingManager.LeaderboardEntryData data;
    private RankingManager manager;

    // ── Setup ─────────────────────────────────────────────────────────────────

    public void Setup(RankingManager.LeaderboardEntryData entryData, RankingManager rankingManager)
    {
        data    = entryData;
        manager = rankingManager;

        // Rank position
        if (rankPositionText != null)
            rankPositionText.text = entryData.position.ToString();

        // Username
        if (usernameText != null)
            usernameText.text = entryData.username;

        // Level
        if (levelText != null)
            levelText.text = $"Lv {entryData.level}";

        // Rank badge sprite — RankingManager sẽ cung cấp sprite array theo tier
        if (rankBadgeImage != null && entryData.rankBadgeSprite != null)
            rankBadgeImage.sprite = entryData.rankBadgeSprite;

        // Rank name
        if (rankNameText != null)
            rankNameText.text = entryData.rankName;

        // Score
        if (scoreText != null)
            scoreText.text = entryData.score.ToString();

        // Background color — highlight top 3
        if (backgroundImage != null)
        {
            if (entryData.position <= 3)
                backgroundImage.color = topThreeColor;
            else
                backgroundImage.color = normalColor;
        }
    }

    // ── Click ─────────────────────────────────────────────────────────────────

    public void OnClick()
    {
        if (data == null || manager == null) return;
        manager.OnEntryCardClicked(data);
    }

    // ── Hover (optional visual feedback) ──────────────────────────────────────

    public void OnPointerEnter()
    {
        if (backgroundImage != null && data.position > 3)
            backgroundImage.color = highlightColor;
    }

    public void OnPointerExit()
    {
        if (backgroundImage != null && data.position > 3)
            backgroundImage.color = normalColor;
    }
}
