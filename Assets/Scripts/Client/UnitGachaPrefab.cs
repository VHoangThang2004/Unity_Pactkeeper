using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Component gắn trên prefab UnitGachaPrefab dùng trong kết quả gacha pull.
/// Mỗi tier (Gems / Weapon+Trinket / Unit+Class) có mainFrame và halfFrame riêng
/// — bạn tự gán sprite vào TierEntry[] trong Inspector.
/// </summary>
public class UnitGachaPrefab : MonoBehaviour
{
    // -------------------------------------------------------
    // Tier Entry — định nghĩa frame cho từng tier
    // -------------------------------------------------------

    [System.Serializable]
    public struct TierEntry
    {
        [Tooltip("Tên nhận dạng (ví dụ: Tier1_Gems, Tier2_Item, Tier3_Unit)")]
        public string label;

        [Tooltip("Frame chính (background lớn)")]
        public Sprite mainFrame;

        [Tooltip("Half frame (overlay nhỏ phía trên, để null nếu không dùng)")]
        public Sprite halfFrame;
    }

    // -------------------------------------------------------
    // Inspector fields
    // -------------------------------------------------------

    [Header("Tier Frame Entries")]
    [Tooltip("Index 0 = Tier 1 (Gems), Index 1 = Tier 2 (Weapon/Trinket), Index 2 = Tier 3 (Unit/Class). Tier 2 prefab để applyTierFrames tắt để giữ sprite đã gắn sẵn.")]
    [SerializeField] private TierEntry[] tiers = new TierEntry[3];
    [SerializeField] private bool applyTierFrames = true;

    [Header("UI References")]
    [SerializeField] private Image mainFrameImg;
    [SerializeField] private Image halfFrameImg;
    [SerializeField] private Image unitImage;
    [SerializeField] private Image unitBackgroundImage;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private GameObject tagDup;
    [SerializeField] private GameObject tagNew;

    static readonly Color Tier1Background = new Color32(0x40, 0xE0, 0xD0, 0xFF);
    static readonly Color Tier3Background = new Color32(0xFF, 0xEB, 0x04, 0xFF);

    // -------------------------------------------------------
    // Public API
    // -------------------------------------------------------

    /// <summary>
    /// Gán dữ liệu cho card.
    /// </summary>
    /// <param name="icon">Sprite của reward (portrait, weapon icon, gem icon...)</param>
    /// <param name="displayName">Tên hiển thị bên dưới card</param>
    /// <param name="tier">0 = Tier 1 (Gems), 1 = Tier 2 (Weapon/Trinket), 2 = Tier 3 (Unit/Class)</param>
    /// <param name="isDuplicate">Item trùng. Chỉ hiện tag khi showOwnershipTag là true.</param>
    /// <param name="showOwnershipTag">Bật Tag_New hoặc Tag_Dup. Preview để false.</param>
    public void Setup(Sprite icon, string displayName, int tier, bool isDuplicate = false, bool showOwnershipTag = false)
    {
        // Apply frame sprites từ tier entry
        if (applyTierFrames && tier >= 0 && tier < tiers.Length)
        {
            TierEntry entry = tiers[tier];

            if (mainFrameImg != null && entry.mainFrame != null)
                mainFrameImg.sprite = entry.mainFrame;

            if (halfFrameImg != null)
            {
                if (entry.halfFrame != null)
                {
                    halfFrameImg.sprite = entry.halfFrame;
                    halfFrameImg.gameObject.SetActive(true);
                }
                else
                {
                    halfFrameImg.gameObject.SetActive(false);
                }
            }
        }

        if (unitBackgroundImage != null)
        {
            bool showBackground = tier == 0 || tier == 2;
            unitBackgroundImage.gameObject.SetActive(showBackground);
            if (tier == 0)
                unitBackgroundImage.color = Tier1Background;
            else if (tier == 2)
                unitBackgroundImage.color = Tier3Background;
        }

        if (unitImage != null)
        {
            unitImage.sprite = icon;
            unitImage.color = icon != null ? Color.white : new Color(1f, 1f, 1f, 0f);
        }

        if (nameText != null)
            nameText.text = displayName;

        bool showTag = showOwnershipTag && tier != 0;
        if (tagDup != null)
            tagDup.SetActive(showTag && isDuplicate);
        if (tagNew != null)
            tagNew.SetActive(showTag && !isDuplicate);
    }

    // -------------------------------------------------------
    // Tier helper (static) — chuyển reward type → tier index
    // -------------------------------------------------------

    /// <summary>
    /// Chuyển reward.type string → tier index (0/1/2).
    /// Dùng trong GachaManager khi gọi Setup().
    /// </summary>
    public static int TierFromRewardType(string rewardType)
    {
        return rewardType switch
        {
            "Unit"    => 2,
            "Class"   => 2,
            "Weapon"  => 1,
            "Trinket" => 1,
            _         => 0,   // Gems hoặc bất kỳ type khác
        };
    }
}
