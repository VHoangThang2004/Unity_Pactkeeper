using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Scene manager for 11_Ranking.
/// Fetches all player profiles to build leaderboard, sorted by gems (temporary score).
/// When backend implements rating system, replace gems with rating field.
///
/// Rank tiers based on Pactkeeper lore (7 bậc):
///   0: Beginner — score 0-999
///   1: Master — score 1000-1999
///   2: Captain — score 2000-2999
///   3: Pactkeeper — score 3000-3999
///   4: Warlord — score 4000-4999
///   5: Grand Commander — score 5000-5999
///   6: Union — score 6000+
///
/// API calls:
///   GET /api/PlayerProfile — current player data
///   GET /api/players/all — all players (for leaderboard) — BACKEND TODO
/// </summary>
public class RankingManager : MonoBehaviour
{
    // ── Data classes ──────────────────────────────────────────────────────────

    public class LeaderboardEntryData
    {
        public int    position;         // 1, 2, 3, ...
        public string playerId;
        public string username;
        public int    level;
        public int    score;            // Rating/MMR
        public int    rankTier;         // 0-6 (maps to rank badges), -1 = Unranked
        public string rankName;         // "Beginner", "Master", etc.
        public Sprite rankBadgeSprite;  // Assigned from rankBadgeSprites array
        public bool   isCurrentPlayer;  // Highlight in UI
    }

    public class RankTierDisplayData
    {
        public int    tier;             // 0-6, -1 = Unranked
        public string rankName;         // English name
        public int    minScore;         // Score required to reach this tier
        public string loreDescription;  // Story description (hidden until player completes story)
        public Sprite badgeSprite;
    }

    // ── Rank tier names (English for UI display) ─────────────────────────────

    private static readonly string[] RankTierNamesEN = new string[]
    {
        "Beginner",                 // 0
        "Master",                   // 1
        "Captain",                  // 2
        "Pactkeeper",               // 3
        "Warlord",                  // 4
        "Grand Commander",          // 5
        "Union"                     // 6
    };

    // ── Rank tier lore descriptions (from Pactkeeper story) ───────────────────

    private static readonly string[] RankTierLore = new string[]
    {
        "Varek in his youth, growing up amidst war, with nothing in hand.",                                     // 0 - Beginner
        "Varek gradually becomes battle-hardened, accumulating combat experience.",                             // 1 - Master
        "At 22 years old, becomes the leader of a small warband.",                                              // 2 - Captain
        "The moment of life and death — forging a pact with the gods, receiving the power of Time Sight.",     // 3 - Pactkeeper
        "Using Time Sight to win continuously, beginning to recruit legendary generals from defeated tribes.",  // 4 - Warlord
        "The warband has gathered all 6 classes of champions, nearly invincible across East and West.",        // 5 - Grand Commander
        "Unified the entire Thiên Nhãn Giới into one — fulfilling the divine pact."                            // 6 - Union
    };

    // ── Rank tier score thresholds (100 LP per tier) ─────────────────────────

    private static readonly int[] RankTierMinScores = new int[]
    {
        0,      // 0 - Beginner
        100,    // 1 - Master
        200,    // 2 - Captain
        300,    // 3 - Pactkeeper
        400,    // 4 - Warlord
        500,    // 5 - Grand Commander
        600     // 6 - Union
    };

    // ── Inspector ─────────────────────────────────────────────────────────────

    [Header("Config")]
    [SerializeField] private BackendConfig config;
    [SerializeField] private SceneConfig   sceneConfig;

    [Header("Left Panel — Leaderboard List")]
    [SerializeField] private Transform  leaderboardContainer;   // VerticalLayoutGroup Content
    [SerializeField] private GameObject entryCardPrefab;
    [SerializeField] private ScrollRect leaderboardScrollRect;
    [SerializeField] private GameObject loadingIndicator;
    [SerializeField] private GameObject emptyState;             // "No leaderboard data"

    [Header("Right Panel — Player Detail")]
    [SerializeField] private GameObject     playerDetailPanel;
    [SerializeField] private Image          detailRankBadgeImage;   // Large rank badge display (clickable)
    [SerializeField] private TMP_Text       detailRankNameText;     // "Pactkeeper", "Beginner", etc.
    [SerializeField] private TMP_Text       detailScoreText;        // Score display in frame
    [SerializeField] private TMP_Text       detailPositionText;     // "#1 / #15 / #234"

    [Header("Rank Badge Sprites (7 tiers + Unranked)")]
    [Tooltip("Index 0 = Beginner, 1 = Master, ..., 6 = Union")]
    [SerializeField] private Sprite[] rankBadgeSprites;
    
    [Tooltip("Sprite for Unranked players (no rating yet)")]
    [SerializeField] private Sprite unrankedBadgeSprite;

    [Header("Buttons")]
    [SerializeField] private Button backButton;
    [SerializeField] private Button viewHistoryButton;      // "Xem lại lịch sử đấu" (not implemented yet)

    [Header("Rank Tier Info Popup")]
    [SerializeField] private GameObject rankTierPopupPanel;
    [SerializeField] private Transform  rankTierCardContainer;  // HorizontalLayoutGroup Content
    [SerializeField] private GameObject rankTierCardPrefab;
    [SerializeField] private Button     rankTierPopupCloseButton;

    [Header("Story Completion Check")]
    [SerializeField] private bool hasCompletedStory = false;  // Set via API check (future)

    // ── Runtime ───────────────────────────────────────────────────────────────

    private List<LeaderboardEntryData> leaderboardEntries = new();
    private PlayerProfileResponse currentPlayerProfile;
    private LeaderboardResponse leaderboardResponse;
    private LeaderboardEntryData selectedEntry;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    void Awake()
    {
        if (backButton              != null) backButton.onClick.AddListener(OnBackClick);
        if (viewHistoryButton       != null) viewHistoryButton.onClick.AddListener(OnViewHistoryClick);
        if (rankTierPopupCloseButton != null) rankTierPopupCloseButton.onClick.AddListener(CloseRankTierPopup);

        // Make detail rank badge clickable
        if (detailRankBadgeImage != null)
        {
            var button = detailRankBadgeImage.gameObject.AddComponent<Button>();
            button.onClick.AddListener(ShowRankTierPopup);
        }

        if (playerDetailPanel != null) playerDetailPanel.SetActive(false);
        if (rankTierPopupPanel != null) rankTierPopupPanel.SetActive(false);
        if (loadingIndicator  != null) loadingIndicator.SetActive(true);
        if (emptyState        != null) emptyState.SetActive(false);
    }

    void OnDestroy()
    {
        if (backButton              != null) backButton.onClick.RemoveListener(OnBackClick);
        if (viewHistoryButton       != null) viewHistoryButton.onClick.RemoveListener(OnViewHistoryClick);
        if (rankTierPopupCloseButton != null) rankTierPopupCloseButton.onClick.RemoveListener(CloseRankTierPopup);
    }

    void Start()
    {
        if (!PlayerSession.IsLoggedIn)
        {
            SceneManager.LoadScene(sceneConfig.mainMenuScene);
            return;
        }
        StartCoroutine(LoadDataCoroutine());
    }

    // ── Data loading ──────────────────────────────────────────────────────────

    IEnumerator LoadDataCoroutine()
    {
        // Fetch current player profile
        yield return FetchPlayerProfile();

        if (currentPlayerProfile == null)
        {
            Debug.LogError("[RankingManager] Failed to load player profile");
            if (loadingIndicator != null) loadingIndicator.SetActive(false);
            if (emptyState       != null) emptyState.SetActive(true);
            yield break;
        }

        // Fetch leaderboard from backend (snapshot on entering scene)
        yield return FetchLeaderboard();

        // Build leaderboard from response
        BuildLeaderboardFromResponse();

        if (loadingIndicator != null) loadingIndicator.SetActive(false);

        if (leaderboardEntries.Count == 0)
        {
            if (emptyState != null) emptyState.SetActive(true);
            yield break;
        }

        RenderLeaderboard();

        // Auto-select current player entry in right panel
        var currentEntry = leaderboardEntries.Find(e => e.isCurrentPlayer);
        if (currentEntry != null)
        {
            ShowPlayerDetail(currentEntry);
        }
        else if (leaderboardResponse?.currentPlayer != null)
        {
            var cp = leaderboardResponse.currentPlayer;
            ShowPlayerDetail(new LeaderboardEntryData
            {
                position        = cp.position,
                playerId        = cp.playerId,
                username        = cp.username,
                level           = cp.level,
                score           = cp.rating,
                rankTier        = cp.rankTier,
                rankName        = GetRankTierName(cp.rankTier),
                rankBadgeSprite = GetRankBadgeSprite(cp.rankTier),
                isCurrentPlayer = true
            });
        }
        else if (leaderboardEntries.Count > 0)
        {
            ShowPlayerDetail(leaderboardEntries[0]);
        }
    }

    IEnumerator FetchPlayerProfile()
    {
        using var req = UnityWebRequest.Get($"{config.backendUrl}/api/PlayerProfile");
        config.SetHeaders(req, PlayerSession.Token);
        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError($"[RankingManager] Failed to load player profile: {req.error}");
            currentPlayerProfile = null;
            yield break;
        }

        currentPlayerProfile = JsonUtility.FromJson<PlayerProfileResponse>(req.downloadHandler.text);
    }

    IEnumerator FetchLeaderboard()
    {
        using var req = UnityWebRequest.Get($"{config.backendUrl}/api/ranking/leaderboard?limit=100");
        config.SetHeaders(req, PlayerSession.Token);
        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            Debug.LogWarning($"[RankingManager] Failed to load leaderboard: {req.error}");
            leaderboardResponse = null;
            yield break;
        }

        string json = req.downloadHandler.text;
        leaderboardResponse = JsonUtility.FromJson<LeaderboardResponse>(json);
    }

    // ── Build leaderboard from response ───────────────────────────────────────

    void BuildLeaderboardFromResponse()
    {
        leaderboardEntries.Clear();

        if (leaderboardResponse?.items == null || leaderboardResponse.items.Length == 0)
        {
            // Fallback: show current player as single entry
            if (currentPlayerProfile != null)
            {
                int tier = CalculateRankTier(currentPlayerProfile.rating);
                leaderboardEntries.Add(new LeaderboardEntryData
                {
                    position        = 1,
                    playerId        = currentPlayerProfile.playerId,
                    username        = currentPlayerProfile.username,
                    level           = currentPlayerProfile.level,
                    score           = currentPlayerProfile.rating,
                    rankTier        = tier,
                    rankName        = GetRankTierName(tier),
                    rankBadgeSprite = GetRankBadgeSprite(tier),
                    isCurrentPlayer = true
                });
            }
            return;
        }

        foreach (var item in leaderboardResponse.items)
        {
            int tier = item.rankTier;
            var entry = new LeaderboardEntryData
            {
                position        = item.position,
                playerId        = item.playerId,
                username        = item.username,
                level           = item.level,
                score           = item.rating,
                rankTier        = tier,
                rankName        = !string.IsNullOrEmpty(item.rankName) ? item.rankName : GetRankTierName(tier),
                rankBadgeSprite = GetRankBadgeSprite(tier),
                isCurrentPlayer = item.isCurrentPlayer || (currentPlayerProfile != null && item.playerId == currentPlayerProfile.playerId)
            };

            leaderboardEntries.Add(entry);
        }
    }

    // ── Rank tier calculation ─────────────────────────────────────────────────

    int CalculateRankTier(int score)
    {
        if (score >= 600) return 6; // Union
        if (score >= 500) return 5; // Grand Commander
        if (score >= 400) return 4; // Warlord
        if (score >= 300) return 3; // Pactkeeper
        if (score >= 200) return 2; // Captain
        if (score >= 100) return 1; // Master
        return 0;                   // Beginner
    }

    Sprite GetRankBadgeSprite(int tier)
    {
        if (tier == -1) // Unranked
            return unrankedBadgeSprite;
            
        if (rankBadgeSprites == null || tier < 0 || tier >= rankBadgeSprites.Length)
            return unrankedBadgeSprite;
            
        return rankBadgeSprites[tier];
    }

    string GetRankTierName(int tier)
    {
        if (tier == -1)
            return "Unranked";
            
        if (tier < 0 || tier >= RankTierNamesEN.Length)
            return "Unknown";
            
        return RankTierNamesEN[tier];
    }

    string GetRankTierLore(int tier)
    {
        if (tier < 0 || tier >= RankTierLore.Length)
            return "";
        return RankTierLore[tier];
    }

    // ── Render leaderboard ────────────────────────────────────────────────────

    void RenderLeaderboard()
    {
        // Clear old entries
        foreach (Transform child in leaderboardContainer)
            Destroy(child.gameObject);

        foreach (var entry in leaderboardEntries)
        {
            var obj  = Instantiate(entryCardPrefab, leaderboardContainer);
            var card = obj.GetComponent<LeaderboardEntryCard>();
            card?.Setup(entry, this);

            // Highlight current player entry
            if (entry.isCurrentPlayer)
            {
                // Add visual indicator (optional) — e.g., different color border
                var outline = obj.GetComponent<Outline>();
                if (outline != null)
                {
                    outline.enabled = true;
                    outline.effectColor = Color.yellow;
                }
            }
        }

        // Scroll to current player position
        if (leaderboardScrollRect != null)
        {
            var currentEntry = leaderboardEntries.Find(e => e.isCurrentPlayer);
            if (currentEntry != null)
            {
                // Scroll to position (approximate)
                float normalizedPosition = 1f - ((float)currentEntry.position / leaderboardEntries.Count);
                leaderboardScrollRect.verticalNormalizedPosition = Mathf.Clamp01(normalizedPosition);
            }
        }
    }

    // ── Called by LeaderboardEntryCard on click ──────────────────────────────

    public void OnEntryCardClicked(LeaderboardEntryData entryData)
    {
        ShowPlayerDetail(entryData);
    }

    // ── Show player detail in right panel ────────────────────────────────────

    void ShowPlayerDetail(LeaderboardEntryData entry)
    {
        selectedEntry = entry;

        if (playerDetailPanel != null)
            playerDetailPanel.SetActive(true);

        // Rank badge (large display) — clickable to show tier info
        if (detailRankBadgeImage != null && entry.rankBadgeSprite != null)
            detailRankBadgeImage.sprite = entry.rankBadgeSprite;

        // Rank name
        if (detailRankNameText != null)
            detailRankNameText.text = entry.rankName;

        // Score (trong frame)
        if (detailScoreText != null)
            detailScoreText.text = entry.score.ToString();

        // Position
        if (detailPositionText != null)
            detailPositionText.text = $"#{entry.position}";
    }

    // ── Button callbacks ──────────────────────────────────────────────────────

    void OnBackClick()
    {
        SceneManager.LoadScene(sceneConfig.mainMenuScene);
    }

    void OnViewHistoryClick()
    {
        // TODO: implement match history scene
        Debug.Log("[RankingManager] View history not implemented yet");
    }

    // ── Rank Tier Info Popup ──────────────────────────────────────────────────

    void ShowRankTierPopup()
    {
        if (rankTierPopupPanel == null) return;

        rankTierPopupPanel.SetActive(true);
        RenderRankTierCards();
    }

    void CloseRankTierPopup()
    {
        if (rankTierPopupPanel != null)
            rankTierPopupPanel.SetActive(false);
    }

    void RenderRankTierCards()
    {
        if (rankTierCardContainer == null || rankTierCardPrefab == null) return;

        // Clear old cards
        foreach (Transform child in rankTierCardContainer)
            Destroy(child.gameObject);

        // Create card for Unranked
        CreateRankTierCard(new RankTierDisplayData
        {
            tier            = -1,
            rankName        = "Unranked",
            minScore        = 0,
            loreDescription = "No rank yet. Start playing to earn your rating!",
            badgeSprite     = unrankedBadgeSprite
        });

        // Create cards for each rank tier (0-6)
        for (int i = 0; i < RankTierNamesEN.Length; i++)
        {
            CreateRankTierCard(new RankTierDisplayData
            {
                tier            = i,
                rankName        = RankTierNamesEN[i],
                minScore        = RankTierMinScores[i],
                loreDescription = RankTierLore[i],
                badgeSprite     = GetRankBadgeSprite(i)
            });
        }
    }

    void CreateRankTierCard(RankTierDisplayData data)
    {
        var obj  = Instantiate(rankTierCardPrefab, rankTierCardContainer);
        var card = obj.GetComponent<RankTierCard>();
        
        if (card != null)
        {
            // Pass story completion status to card
            card.Setup(data, hasCompletedStory);
        }
    }
}
