using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Quản lý các UI element trong Login scene.
/// Attach vào GameObject LoginManager.
/// </summary>
public class LoginPanelController : MonoBehaviour
{
    [Header("Buttons")]
    [SerializeField] private Button topUpButton;

    [Header("Top Up")]
    [Tooltip("URL trang top up đã deploy — chỉnh sửa trực tiếp trong Inspector.")]
    [SerializeField] private string topUpUrl = "https://your-topup-site.vercel.app";

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    void Awake()
    {
        if (topUpButton != null)
            topUpButton.onClick.AddListener(OnTopUpClicked);
    }

    void OnDestroy()
    {
        if (topUpButton != null)
            topUpButton.onClick.RemoveListener(OnTopUpClicked);
    }

    // ── Handlers ──────────────────────────────────────────────────────────────

    void OnTopUpClicked()
    {
        if (string.IsNullOrWhiteSpace(topUpUrl))
        {
            Debug.LogWarning("[LoginPanelController] Top Up URL chưa được cấu hình.");
            return;
        }

        Application.OpenURL(topUpUrl);
    }
}
