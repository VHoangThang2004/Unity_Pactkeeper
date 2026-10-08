using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Quest panel manager — left list + right detail.
/// API:
///   GET  /api/rewardrule/active          — active quest definitions
///   GET  /api/rewardprogress             — player progress records
///   POST /api/rewardprogress/{id}/claim  — claim a completed quest reward
/// </summary>
public class QuestManager : MonoBehaviour
{
    // ── Inner data class ──────────────────────────────────────────────────────

    public class QuestDisplayData
    {
        public RewardRuleDto     rule;
        public RewardProgressDto progress; // null = player never touched this quest
    }

    // ── DTOs ─────────────────────────────────────────────────────────────────

    [System.Serializable]
    public class RewardConditionDto
    {
        public string field;
        public string op;
        public string value;
    }

    [System.Serializable]
    public class RewardDto
    {
        public string type;         // "Unit" | "Weapon" | "Trinket" | "Gems"
        public int    definitionId; // uId / weaponId / trinketId; 0 for Gems
        public int    classId;      // Unit only
        public int    amount;       // gem count or 1 for items
    }

    [System.Serializable]
    public class RewardRuleDto
    {
        public string               id;
        public string               actionType;
        public RewardConditionDto[] conditions;
        public string               accumulateField;
        public int                  targetCount;
        public RewardDto            reward;
        public bool                 isActive;
    }

    [System.Serializable]
    public class RewardProgressDto
    {
        public string id;
        public string ruleId;
        public int    currentCount;
        public bool   isCompleted;
        public string completedAt;
        public bool   isReceived;
        public string receivedAt;
    }

    [System.Serializable] private class RuleArrayWrapper     { public RewardRuleDto[]     items; }
    [System.Serializable] private class ProgressArrayWrapper { public RewardProgressDto[] items; }

    // ── Inspector ─────────────────────────────────────────────────────────────

    [Header("Config")]
    [SerializeField] private BackendConfig config;

    [Header("Registries — for reward icons")]
    [SerializeField] private WeaponDefinitionRegistry  weaponRegistry;
    [SerializeField] private TrinketDefinitionRegistry trinketRegistry;
    [SerializeField] private ClassDefinitionRegistry   classRegistry;
    [SerializeField] private UnitPrefabRegistry        unitPrefabRegistry;

    [Tooltip("Default icon shown when reward type is Gems (or icon not found).")]
    [SerializeField] private Sprite gemDefaultSprite;

    [Header("Panel")]
    [SerializeField] private GameObject panel;
    [SerializeField] private Button     closeButton;

    [Header("Left Panel — Quest List")]
    [SerializeField] private Transform  questListContainer;
    [SerializeField] private GameObject questItemPrefab;
    [SerializeField] private GameObject emptyStatePanel;

    [Header("Right Panel — Quest Detail")]
    [SerializeField] private GameObject detailPanel;
    [SerializeField] private GameObject detailEmptyState;

    // Header
    [SerializeField] private TMP_Text detailQuestNameText;   // "Quest #3"
    [SerializeField] private TMP_Text detailActionTypeText;  // "Match Won"
    [SerializeField] private TMP_Text detailDescriptionText; // human-readable description

    // Progress
    [SerializeField] private TMP_Text detailProgressText;    // "Progress: 2 / 5"
    [SerializeField] private Slider   detailProgressBar;

    // Conditions — hidden from user, kept for debug only
    // [SerializeField] private TMP_Text detailConditionsText;  // disabled

    // Reward box
    [SerializeField] private TMP_Text detailRewardText;    // merged: "Gems  x 200" / "Weapon  x 1"
    [SerializeField] private Image    detailRewardIcon;    // unit body sprite (unitImg) or item icon
    [SerializeField] private Image    detailRewardFrame;   // unit class frame (ClassDefinitionRegistry) — Unit rewards only

    // Status + Claim
    [SerializeField] private TMP_Text detailStatusText;
    [SerializeField] private Button   claimButton;
    [SerializeField] private TMP_Text claimButtonText;

    // ── Runtime ───────────────────────────────────────────────────────────────

    private List<QuestDisplayData> allQuests   = new();
    private QuestItem              selectedItem;
    private QuestDisplayData       selectedData;

    // Maps quest rule id → display order index (1-based), built once after load
    private Dictionary<string, int> questNumberMap = new();

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    void Awake()
    {
        if (closeButton != null) closeButton.onClick.AddListener(Close);
        if (claimButton  != null) claimButton.onClick.AddListener(OnClickClaim);
        HidePanel();
        HideDetailPanel();
    }

    void OnDestroy()
    {
        if (closeButton != null) closeButton.onClick.RemoveListener(Close);
        if (claimButton  != null) claimButton.onClick.RemoveListener(OnClickClaim);
    }

    // ── Public API ────────────────────────────────────────────────────────────

    public void Open()
    {
        if (panel != null) panel.SetActive(true);
        HideDetailPanel();
        selectedItem = null;
        selectedData = null;
        StartCoroutine(LoadQuestsCoroutine());
    }

    public void Close()
    {
        HidePanel();
        HideDetailPanel();
        ClearList();
        selectedItem = null;
        selectedData = null;
    }

    // ── Called by QuestItem ───────────────────────────────────────────────────

    public void OnQuestSelected(QuestItem item, QuestDisplayData data)
    {
        if (selectedItem != null) selectedItem.SetSelected(false);
        selectedItem = item;
        selectedData = data;
        selectedItem.SetSelected(true);
        ShowDetail(data);
    }

    public int GetQuestNumber(string ruleId)
    {
        if (string.IsNullOrEmpty(ruleId)) return 0;
        questNumberMap.TryGetValue(ruleId, out int n);
        return n;
    }

    // ── Data loading ──────────────────────────────────────────────────────────

    IEnumerator LoadQuestsCoroutine()
    {
        ClearList();
        allQuests.Clear();
        questNumberMap.Clear();

        // Init registries
        weaponRegistry?.Init();
        trinketRegistry?.Init();
        classRegistry?.Init();
        unitPrefabRegistry?.Init();

        // 1. Active rules
        RewardRuleDto[] rules = null;
        using (var req = UnityWebRequest.Get($"{config.backendUrl}/api/rewardrule/active"))
        {
            config.SetHeaders(req, PlayerSession.Token);
            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[Quest] Failed to load rules: {req.error}");
                ShowEmptyState();
                yield break;
            }

            var w = JsonUtility.FromJson<RuleArrayWrapper>(
                "{\"items\":" + req.downloadHandler.text + "}");
            rules = w.items;
        }

        if (rules == null || rules.Length == 0) { ShowEmptyState(); yield break; }

        // 2. Player progress
        RewardProgressDto[] progressList = null;
        using (var req = UnityWebRequest.Get($"{config.backendUrl}/api/rewardprogress"))
        {
            config.SetHeaders(req, PlayerSession.Token);
            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success)
            {
                var w = JsonUtility.FromJson<ProgressArrayWrapper>(
                    "{\"items\":" + req.downloadHandler.text + "}");
                progressList = w.items;
            }
            else
            {
                Debug.LogWarning($"[Quest] Progress load failed: {req.error}");
            }
        }

        // 3. Join
        var progressMap = new Dictionary<string, RewardProgressDto>();
        if (progressList != null)
            foreach (var p in progressList)
                if (!string.IsNullOrEmpty(p.ruleId))
                    progressMap[p.ruleId] = p;

        int number = 1;
        foreach (var rule in rules)
        {
            RewardProgressDto progress = null;
            if (!string.IsNullOrEmpty(rule.id))
            {
                progressMap.TryGetValue(rule.id, out progress);
                questNumberMap[rule.id] = number++;
            }
            allQuests.Add(new QuestDisplayData { rule = rule, progress = progress });
        }

        RenderList();
    }

    void RenderList()
    {
        ClearList();

        if (allQuests.Count == 0) { ShowEmptyState(); return; }
        HideEmptyState();

        foreach (var questData in allQuests)
        {
            var obj  = Instantiate(questItemPrefab, questListContainer);
            var item = obj.GetComponent<QuestItem>();
            item?.Setup(questData, this);
        }
    }

    // ── Detail panel ──────────────────────────────────────────────────────────

    void ShowDetail(QuestDisplayData data)
    {
        Debug.Log($"[Quest] ShowDetail called. detailPanel={detailPanel}, detailEmptyState={detailEmptyState}");

        if (detailPanel      != null) detailPanel.SetActive(true);
        if (detailEmptyState != null) detailEmptyState.SetActive(false);

        var rule     = data.rule;
        var progress = data.progress;

        // ── Quest name: "Quest #3" ────────────────────────────────────────────
        int num = GetQuestNumber(rule.id);
        if (detailQuestNameText != null)
            detailQuestNameText.text = num > 0 ? $"Quest #{num}" : "Quest";

        // ── Action type subtitle: "Match Won" ─────────────────────────────────
        if (detailActionTypeText != null)
            detailActionTypeText.text = FormatActionType(rule.actionType);

        // ── Description — human-readable quest objective ──────────────────────
        if (detailDescriptionText != null)
            detailDescriptionText.text = BuildDescription(rule);

        // ── Progress ──────────────────────────────────────────────────────────
        int current = progress?.currentCount ?? 0;
        int target  = rule.targetCount;

        if (detailProgressText != null)
            detailProgressText.text = $"Progress: {current} / {target}";

        if (detailProgressBar != null)
        {
            detailProgressBar.minValue = 0;
            detailProgressBar.maxValue = Mathf.Max(target, 1);
            detailProgressBar.value    = current;
        }

        // ── Reward ────────────────────────────────────────────────────────────
        if (rule.reward != null)
        {
            if (detailRewardText != null)
                detailRewardText.text = BuildRewardText(rule.reward);

            ApplyRewardVisuals(rule.reward);
        }

        // ── Status ────────────────────────────────────────────────────────────
        if (detailStatusText != null)
            detailStatusText.text = GetStatusLabel(progress);

        RefreshClaimButton(progress);
    }

    void RefreshClaimButton(RewardProgressDto progress)
    {
        if (claimButton == null) return;

        bool canClaim = progress != null && progress.isCompleted && !progress.isReceived;
        claimButton.interactable = canClaim;

        if (claimButtonText != null)
        {
            if (progress == null || !progress.isCompleted)
                claimButtonText.text = "Not Completed";
            else if (progress.isReceived)
                claimButtonText.text = "Claimed";
            else
                claimButtonText.text = "Claim Reward";
        }
    }

    // ── Claim ─────────────────────────────────────────────────────────────────

    void OnClickClaim()
    {
        if (selectedData?.progress == null) return;
        if (!selectedData.progress.isCompleted || selectedData.progress.isReceived) return;
        StartCoroutine(ClaimCoroutine(selectedData));
    }

    IEnumerator ClaimCoroutine(QuestDisplayData data)
    {
        if (claimButton != null) claimButton.interactable = false;

        using var req = new UnityWebRequest(
            $"{config.backendUrl}/api/rewardprogress/{data.progress.id}/claim", "POST");
        req.uploadHandler   = new UploadHandlerRaw(new byte[0]);
        req.downloadHandler = new DownloadHandlerBuffer();
        config.SetHeaders(req, PlayerSession.Token);

        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError($"[Quest] Claim failed: {req.error}");
            if (claimButton != null) claimButton.interactable = true;
            yield break;
        }

        Debug.Log($"[Quest] Claimed: {data.progress.id}");
        data.progress.isReceived = true;

        ShowDetail(data);
        RenderList();
    }

    // ── Reward visuals ────────────────────────────────────────────────────────

    /// <summary>
    /// Sets detailRewardIcon and detailRewardFrame based on reward type.
    ///
    /// Unit  → icon = unitImg from prefab (ClientUnit component)
    ///         frame = ClassDefinitionRegistry.GetFullFrame(classId)
    /// Weapon/Trinket → icon = registry icon, frame hidden
    /// Gems  → icon = gemDefaultSprite, frame hidden
    /// </summary>
    void ApplyRewardVisuals(RewardDto reward)
    {
        // Hide frame by default; only shown for Unit rewards
        if (detailRewardFrame != null)
            detailRewardFrame.gameObject.SetActive(false);

        if (reward == null)
        {
            if (detailRewardIcon != null) detailRewardIcon.sprite = gemDefaultSprite;
            return;
        }

        switch (reward.type)
        {
            case "Unit":
            {
                // Body sprite from prefab's ClientUnit component
                Sprite bodySprite = null;
                if (unitPrefabRegistry != null)
                {
                    var prefab = unitPrefabRegistry.Get(reward.definitionId);
                    bodySprite = prefab?.GetComponent<ClientUnit>()?.unitImg;
                }
                if (detailRewardIcon != null)
                    detailRewardIcon.sprite = bodySprite != null ? bodySprite : gemDefaultSprite;

                // Frame sprite from ClassDefinitionRegistry
                if (detailRewardFrame != null && classRegistry != null)
                {
                    var frameSprite = classRegistry.GetFullFrame(reward.classId)
                                   ?? classRegistry.GetHalfFrame(reward.classId);
                    if (frameSprite != null)
                    {
                        detailRewardFrame.sprite = frameSprite;
                        detailRewardFrame.gameObject.SetActive(true);
                    }
                }
                break;
            }

            case "Weapon":
            {
                var s = weaponRegistry?.GetIcon(reward.definitionId);
                if (detailRewardIcon != null)
                    detailRewardIcon.sprite = s != null ? s : gemDefaultSprite;
                break;
            }

            case "Trinket":
            {
                var s = trinketRegistry?.GetIcon(reward.definitionId);
                if (detailRewardIcon != null)
                    detailRewardIcon.sprite = s != null ? s : gemDefaultSprite;
                break;
            }

            default: // Gems
                if (detailRewardIcon != null)
                    detailRewardIcon.sprite = gemDefaultSprite;
                break;
        }
    }

    // ── Description builder ───────────────────────────────────────────────────

    /// <summary>
    /// Converts the machine-readable rule (actionType + conditions + targetCount + accumulateField)
    /// into a single human-readable sentence the player can understand.
    /// </summary>
    static string BuildDescription(RewardRuleDto rule)
    {
        if (rule == null) return string.Empty;

        string action      = rule.actionType  ?? string.Empty;
        int    target      = rule.targetCount;
        var    conds       = rule.conditions;
        string accumField  = rule.accumulateField ?? string.Empty;

        // Helper: find a condition value by field name
        string CondVal(string field)
        {
            if (conds == null) return null;
            foreach (var c in conds)
                if (string.Equals(c.field, field, System.StringComparison.OrdinalIgnoreCase))
                    return c.value;
            return null;
        }

        switch (action)
        {
            // ── MatchResult ──────────────────────────────────────────────────
            case "MatchResult":
            {
                string wonVal      = CondVal("won");
                string aliveVal    = CondVal("unitsAlive");
                bool   mustWin     = string.Equals(wonVal, "true", System.StringComparison.OrdinalIgnoreCase);
                bool   hasAlive    = !string.IsNullOrEmpty(aliveVal);

                if (mustWin && hasAlive)
                    return $"Win {target} match{(target > 1 ? "es" : "")} with at least {aliveVal} units surviving.";

                if (mustWin)
                    return $"Win {target} match{(target > 1 ? "es" : "")}.";

                return $"Complete {target} match{(target > 1 ? "es" : "")}.";
            }

            // ── GachaPull ────────────────────────────────────────────────────
            case "GachaPull":
            {
                string tierVal = CondVal("rarestTier");

                // Accumulate pull count
                if (string.Equals(accumField, "pullCount", System.StringComparison.OrdinalIgnoreCase))
                    return $"Perform a total of {target} gacha pull{(target > 1 ? "s" : "")}.";

                // Must get a specific rarity tier in one pull
                if (!string.IsNullOrEmpty(tierVal))
                {
                    string rarityName = tierVal switch { "3" => "unit (Tier 3)", "2" => "Tier 2 item", _ => $"Tier {tierVal} item" };
                    return $"Pull a {rarityName} from the gacha banner.";
                }

                return $"Perform {target} gacha pull{(target > 1 ? "s" : "")}.";
            }

            // ── Purchase ─────────────────────────────────────────────────────
            case "Purchase":
            {
                if (string.Equals(accumField, "gemsAmount", System.StringComparison.OrdinalIgnoreCase))
                    return $"Spend a total of {target} gems on top-up purchases.";

                return $"Complete {target} purchase{(target > 1 ? "s" : "")}.";
            }

            // ── StoryProgress ────────────────────────────────────────────────
            case "StoryProgress":
            {
                string chapterVal = CondVal("chapterId");
                if (!string.IsNullOrEmpty(chapterVal))
                    return $"Complete Chapter {chapterVal} of the story mode.";

                return $"Reach story progress milestone {target}.";
            }

            // ── Generic fallback ─────────────────────────────────────────────
            default:
                return $"Complete this quest {target} time{(target > 1 ? "s" : "")}.";
        }
    }

    // ── Text helpers ──────────────────────────────────────────────────────────

    /// <summary>
    /// Builds the merged reward text shown in the reward box.
    /// Gems: "Gems  x 200"
    /// Others: "{Item Name}  x {amount}"  (name resolved from registry)
    /// </summary>
    string BuildRewardText(RewardDto reward)
    {
        if (reward == null) return string.Empty;
        return $"{GetRewardName(reward)}  x {reward.amount}";
    }

    /// <summary>Resolves the display name of a reward from the appropriate registry.</summary>
    string GetRewardName(RewardDto reward)
    {
        switch (reward.type)
        {
            case "Weapon":
                if (weaponRegistry != null)
                {
                    var def = weaponRegistry.GetDefinition(reward.definitionId);
                    if (def != null && !string.IsNullOrEmpty(def.name)) return def.name;
                }
                return "Weapon";

            case "Trinket":
                if (trinketRegistry != null)
                {
                    var def = trinketRegistry.GetDefinition(reward.definitionId);
                    if (def != null && !string.IsNullOrEmpty(def.name)) return def.name;
                }
                return "Trinket";

            case "Unit":
                if (unitPrefabRegistry != null)
                {
                    string uName = unitPrefabRegistry.GetName(reward.definitionId);
                    if (!string.IsNullOrEmpty(uName)) return uName;
                }
                return "Unit";

            default: // Gems
                return "Gems";
        }
    }

    /// <summary>"MatchWon" → "Match Won", "storyProgress" → "Story Progress"</summary>
    static string FormatActionType(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return string.Empty;

        var sb = new System.Text.StringBuilder();
        // Handle first char: always capitalise
        sb.Append(char.ToUpper(raw[0]));
        for (int i = 1; i < raw.Length; i++)
        {
            char c = raw[i];
            if (char.IsUpper(c) && !char.IsUpper(raw[i - 1]))
                sb.Append(' ');
            sb.Append(c);
        }
        return sb.ToString();
    }


    static string GetStatusLabel(RewardProgressDto progress)
    {
        if (progress == null)          return "Status: Not Started";
        if (progress.isReceived)       return "Status: Claimed";
        if (progress.isCompleted)      return "Status: Completed — Ready to Claim";
        return "Status: In Progress";
    }

    // ── UI helpers ────────────────────────────────────────────────────────────

    void HidePanel()         { if (panel != null) panel.SetActive(false); }
    void HideDetailPanel()
    {
        if (detailPanel      != null) detailPanel.SetActive(false);
        if (detailEmptyState != null) detailEmptyState.SetActive(true);
    }
    void ClearList()         { foreach (Transform t in questListContainer) Destroy(t.gameObject); }
    void ShowEmptyState()    { if (emptyStatePanel != null) emptyStatePanel.SetActive(true); }
    void HideEmptyState()    { if (emptyStatePanel != null) emptyStatePanel.SetActive(false); }
}
