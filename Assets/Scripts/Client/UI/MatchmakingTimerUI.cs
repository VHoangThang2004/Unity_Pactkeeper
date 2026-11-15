using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// Bộ đếm thời gian tìm trận.
/// Script này đặt trên một GO luôn ACTIVE.
/// Chỉ timerDisplay (UI) mới được ẩn/hiện — script GO không bao giờ inactive.
/// </summary>
public class MatchmakingTimerUI : MonoBehaviour
{
    [Header("UI")]
    [Tooltip("GameObject chứa toàn bộ timer UI — sẽ được ẩn/hiện. Phải là GO KHÁC với GO chứa script này.")]
    [SerializeField] private GameObject timerDisplay;

    [Tooltip("TMP_Text hiển thị thời gian, ví dụ: 00:42")]
    [SerializeField] private TMP_Text timerText;

    // ── Runtime ───────────────────────────────────────────────────────────────

    private Coroutine _timerCoroutine;
    private float     _elapsed;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    void Awake()
    {
        // Ẩn UI ngay từ đầu — KHÔNG SetActive(false) trên chính GO này
        HideDisplay();
        UpdateDisplay(0f);
    }

    // ── Public API ────────────────────────────────────────────────────────────

    public void StartTimer()
    {
        // Reset trước
        if (_timerCoroutine != null)
        {
            StopCoroutine(_timerCoroutine);
            _timerCoroutine = null;
        }

        _elapsed = 0f;
        UpdateDisplay(0f);
        ShowDisplay();

        // Coroutine chạy trên GO này — GO này luôn active nên không lỗi
        _timerCoroutine = StartCoroutine(TimerLoop());
    }

    public void StopTimer()
    {
        if (_timerCoroutine != null)
        {
            StopCoroutine(_timerCoroutine);
            _timerCoroutine = null;
        }

        HideDisplay();
        UpdateDisplay(0f);
    }

    // ── Internal ──────────────────────────────────────────────────────────────

    IEnumerator TimerLoop()
    {
        while (true)
        {
            yield return new WaitForSeconds(1f);
            _elapsed += 1f;
            UpdateDisplay(_elapsed);
        }
    }

    void UpdateDisplay(float seconds)
    {
        if (timerText == null) return;
        int m = (int)seconds / 60;
        int s = (int)seconds % 60;
        timerText.text = $"{m:00}:{s:00}";
    }

    void ShowDisplay()
    {
        if (timerDisplay != null) timerDisplay.SetActive(true);
    }

    void HideDisplay()
    {
        if (timerDisplay != null) timerDisplay.SetActive(false);
    }
}
