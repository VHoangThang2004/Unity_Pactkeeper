using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Điều khiển animation mở/đóng cuộn giấy cho ScrollPanel.
///
/// Setup:
///   1. Attach script này vào GameObject ScrollPanel
///   2. Animator Controller phải có:
///      - Bool parameter tên "IsOpen"
///      - Transition: Closed_Idle → Scroll_Open (IsOpen = true)
///      - Transition: Scroll_Open → Scroll_Close (IsOpen = false, Has Exit Time = off)
///      - Transition: Scroll_Close → Scroll_Open (IsOpen = true, Has Exit Time = off)
///   3. Gán closeButton trong Inspector (optional)
///   4. Thêm Animation Event ở frame CUỐI của clip Scroll_Close,
///      gọi method: OnCloseAnimationFinished
/// </summary>
[RequireComponent(typeof(Animator))]
public class ScrollPanelController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Button closeButton;

    [Header("Children to hide after close animation")]
    [Tooltip("Các GO con sẽ bị SetActive(false) sau khi animation đóng xong. " +
             "Nếu để trống thì tắt luôn toàn bộ ScrollPanel GO.")]
    [SerializeField] private GameObject[] childrenToHide;

    private static readonly int IsOpenHash = Animator.StringToHash("IsOpen");
    private Animator _animator;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    void Awake()
    {
        _animator = GetComponent<Animator>();

        if (closeButton != null)
            closeButton.onClick.AddListener(Close);
    }

    void OnDestroy()
    {
        if (closeButton != null)
            closeButton.onClick.RemoveListener(Close);
    }

    void Start()
    {
        // Đảm bảo các children visible khi mở
        SetChildrenActive(true);
        Open();
    }

    // ── Public API ────────────────────────────────────────────────────────────

    public void Open()
    {
        // Bật GO cha và children trước khi animation mở chạy
        gameObject.SetActive(true);
        SetChildrenActive(true);
        _animator.SetBool(IsOpenHash, true);
    }

    public void Close()
    {
        // Children vẫn còn hiện trong lúc animation đóng chạy
        // OnCloseAnimationFinished() sẽ ẩn chúng sau khi animation xong
        _animator.SetBool(IsOpenHash, false);
    }

    public void Toggle()
    {
        if (_animator.GetBool(IsOpenHash))
            Close();
        else
            Open();
    }

    /// <summary>
    /// Gọi bằng Animation Event ở frame CUỐI của clip Scroll_Close.
    /// Ẩn các GO con và GO cha sau khi animation đóng hoàn tất.
    /// </summary>
    public void OnCloseAnimationFinished()
    {
        SetChildrenActive(false);
        // Tắt luôn GO cha sau khi children đã ẩn
        gameObject.SetActive(false);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    void SetChildrenActive(bool active)
    {
        if (childrenToHide != null && childrenToHide.Length > 0)
        {
            foreach (var child in childrenToHide)
                if (child != null) child.SetActive(active);
        }
        else
        {
            // Fallback: ẩn/hiện toàn bộ ScrollPanel
            gameObject.SetActive(active);
        }
    }
}
