using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Gacha result card — lighter than UnitContainer, supports 3 tier frames.
/// Tier 1 = Gems (common), Tier 2 = Weapon/Trinket (rare), Tier 3 = Unit/Class (epic).
/// Wire the 3 prefabs in GachaManager Inspector instead of the old unitContainerPrefab.
/// </summary>
public class GachaResultCard : MonoBehaviour
{
    [Header("Art")]
    [SerializeField] private Image frameImage;      // background frame sprite (tier-specific)
    [SerializeField] private Image itemImage;       // the reward icon / portrait
    [SerializeField] private Image glowImage;       // optional glow/shine overlay (can be null)

    [Header("Text")]
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI subText;   // e.g. "DUP" badge or type label (optional)

    [Header("Duplicate Overlay")]
    [SerializeField] private GameObject dupBadge;   // shown when isDuplicate = true (optional)
    [SerializeField] private Color dupTint = new Color(0.55f, 0.85f, 1f, 1f);

    // -------------------------------------------------------
    // Public API
    // -------------------------------------------------------

    /// <param name="icon">Reward sprite (unit portrait, weapon icon, gem icon...)</param>
    /// <param name="displayName">Main label under the card</param>
    /// <param name="isDuplicate">Show dup badge and apply tint</param>
    /// <param name="typeLabel">Optional sub-label (e.g. "WEAPON", "50 GEMS")</param>
    public void Setup(Sprite icon, string displayName, bool isDuplicate = false, string typeLabel = "")
    {
        if (itemImage != null)
        {
            itemImage.sprite = icon;
            itemImage.color  = icon != null ? Color.white : new Color(1, 1, 1, 0);
        }

        if (nameText != null)
            nameText.text = displayName;

        if (subText != null)
            subText.text = typeLabel;

        if (dupBadge != null)
            dupBadge.SetActive(isDuplicate);

        if (isDuplicate && itemImage != null)
            itemImage.color = dupTint;
    }

    /// <summary>Override the frame sprite at runtime (used to swap tier frames).</summary>
    public void SetFrame(Sprite frame)
    {
        if (frameImage != null && frame != null)
            frameImage.sprite = frame;
    }

    public void SetGlow(bool show)
    {
        if (glowImage != null)
            glowImage.gameObject.SetActive(show);
    }
}
