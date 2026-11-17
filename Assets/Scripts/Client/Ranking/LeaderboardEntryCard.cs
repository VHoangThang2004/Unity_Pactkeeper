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
    [SerializeField] private Image    numberFrame;
    [Tooltip("0 = Top 1, 1 = Top 2, 2 = Top 3, 3 = Rank 4+")]
    [SerializeField] private Sprite[] backgroundSprites = new Sprite[4];
    [Tooltip("0 = Top 1, 1 = Top 2, 2 = Top 3, 3 = Rank 4+")]
    [SerializeField] private Sprite[] numberFrameSprites = new Sprite[4];
    [SerializeField] private Color    normalColor = new Color(0.2f, 0.2f, 0.2f, 0.8f);
    [SerializeField] private Color    highlightColor = new Color(0.3f, 0.3f, 0.4f, 0.9f);
    [SerializeField] private Color    topThreeColor = new Color(0.4f, 0.3f, 0.1f, 0.9f);

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

        ApplyPlacementArt(entryData.position);
    }

    static int PlacementIndex(int position)
    {
        if (position <= 1) return 0;
        if (position == 2) return 1;
        if (position == 3) return 2;
        return 3;
    }

    static Sprite SpriteAt(Sprite[] sprites, int index)
    {
        if (sprites == null || index < 0 || index >= sprites.Length) return null;
        return sprites[index];
    }

    void ApplyPlacementArt(int position)
    {
        int index = PlacementIndex(position);
        Sprite background = SpriteAt(backgroundSprites, index);
        if (backgroundImage != null)
        {
            if (background != null)
            {
                backgroundImage.sprite = background;
                backgroundImage.color = Color.white;
            }
            else
            {
                backgroundImage.color = position <= 3 ? topThreeColor : normalColor;
            }
        }

        Sprite frame = SpriteAt(numberFrameSprites, index);
        if (numberFrame != null && frame != null)
        {
            numberFrame.sprite = frame;
            numberFrame.color = Color.white;
        }
    }

    bool HasPlacementSprite()
    {
        return data != null && SpriteAt(backgroundSprites, PlacementIndex(data.position)) != null;
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
        if (backgroundImage != null && data.position > 3 && !HasPlacementSprite())
            backgroundImage.color = highlightColor;
    }

    public void OnPointerExit()
    {
        if (backgroundImage != null && data.position > 3 && !HasPlacementSprite())
            backgroundImage.color = normalColor;
    }
}
