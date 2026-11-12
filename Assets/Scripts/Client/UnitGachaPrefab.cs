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
    [Tooltip("Index 0 = Tier 1 (Gems), Index 1 = Tier 2 (Weapon/Trinket), Index 2 = Tier 3 (Unit/Class)")]
    [SerializeField] private TierEntry[] tiers = new TierEntry[3];

    [Header("UI References")]
    [SerializeField] private Image mainFrameImg;
    [SerializeField] private Image halfFrameImg;
    [SerializeField] private Image unitImage;
    [SerializeField] private TextMeshProUGUI nameText;

    // -------------------------------------------------------
    // Public API
    // -------------------------------------------------------

    /// <summary>
    /// Gán dữ liệu cho card.
    /// </summary>
    /// <param name="icon">Sprite của reward (portrait, weapon icon, gem icon...)</param>
    /// <param name="displayName">Tên hiển thị bên dưới card</param>
    /// <param name="tier">0 = Tier 1 (Gems), 1 = Tier 2 (Weapon/Trinket), 2 = Tier 3 (Unit/Class)</param>
    /// <param name="isDuplicate">Nếu true, tô tint xanh nhạt lên icon</param>
    public void Setup(Sprite icon, string displayName, int tier, bool isDuplicate = false)
    {
        // Apply frame sprites từ tier entry
        if (tier >= 0 && tier < tiers.Length)
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

        // Apply icon
        if (unitImage != null)
        {
            unitImage.sprite = icon;
            unitImage.color = icon != null
                ? (isDuplicate ? new Color(0.55f, 0.75f, 1.0f, 1.0f) : Color.white)
                : new Color(1f, 1f, 1f, 0f);
        }

        // Apply name
        if (nameText != null)
            nameText.text = displayName;
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
