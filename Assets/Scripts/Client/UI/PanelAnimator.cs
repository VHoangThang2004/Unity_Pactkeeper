using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Reusable panel open/close animator.
/// Attach this component directly to a panel GameObject.
///
/// SETUP: Add this component → Unity auto-adds CanvasGroup via [RequireComponent].
/// Panel does NOT need to be active at edit time.
/// Visibility is controlled via CanvasGroup (alpha/blocksRaycasts) so
/// the GameObject stays active and coroutines always work.
/// </summary>
[RequireComponent(typeof(CanvasGroup))]  // Unity auto-adds CanvasGroup, solves MissingComponentException
public class PanelAnimator : MonoBehaviour
{
    // ── Types ─────────────────────────────────────────────────────────────────

    public enum AnimationType { Fade, Rise, Pan }
    public enum PanDirection  { Left, Right, Up, Down }

    // ── Inspector ─────────────────────────────────────────────────────────────

    [Header("Animation")]
    [SerializeField] public AnimationType openAnimation  = AnimationType.Rise;
    [SerializeField] public AnimationType closeAnimation = AnimationType.Fade;
    [SerializeField] private PanDirection panDirection   = PanDirection.Down;

    [Header("Timing")]
    [SerializeField] private float openDuration  = 0.25f;
    [SerializeField] private float closeDuration = 0.18f;

    [Header("Rise / Pan offset (pixels)")]
    [SerializeField] private float slideDistance = 40f;

    // ── Runtime ───────────────────────────────────────────────────────────────

    private CanvasGroup   _cg;
    private RectTransform _rect;
    private Coroutine     _current;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    void Awake()
    {
        _rect = GetComponent<RectTransform>();
        _cg   = GetComponent<CanvasGroup>(); // guaranteed by [RequireComponent]

        // Snap hidden immediately so panel is invisible but GO stays active
        SnapHidden();
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>Show panel with open animation.</summary>
    public void Open(Action onComplete = null)
    {
        // Ensure GO is active — needed only the very first call
        // after scene load when panel starts inactive in the Editor.
        if (!gameObject.activeSelf)
            gameObject.SetActive(true);

        // Re-fetch in case Awake hasn't run yet on this GO
        // (possible if Open() is called from another script's Awake)
        if (_cg == null)
        {
            _rect = GetComponent<RectTransform>();
            _cg   = GetComponent<CanvasGroup>();
        }

        StopCurrent();
        SnapHidden();

        _current = StartCoroutine(AnimateIn(openAnimation, openDuration, () =>
        {
            _cg.interactable   = true;
            _cg.blocksRaycasts = true;
            onComplete?.Invoke();
        }));
    }

    /// <summary>
    /// Hide panel with close animation.
    /// GO stays active — alpha + blocksRaycasts handle visibility.
    /// </summary>
    public void Close(MonoBehaviour owner = null, Action onComplete = null)
    {
        // Re-fetch in case Awake hasn't run yet
        if (_cg == null)
        {
            _rect = GetComponent<RectTransform>();
            _cg   = GetComponent<CanvasGroup>();
        }

        if (_cg == null)
        {
            // Absolute fallback: no CanvasGroup at all
            gameObject.SetActive(false);
            onComplete?.Invoke();
            return;
        }

        StopCurrent();
        _cg.interactable   = false;
        _cg.blocksRaycasts = false;

        _current = StartCoroutine(AnimateOut(closeAnimation, closeDuration, () =>
        {
            SnapHidden();
            _current = null;
            onComplete?.Invoke();
        }));
    }

    /// <summary>Instant hide — safe to call from any Awake().</summary>
    public void HideImmediate()
    {
        if (_cg == null)
        {
            _rect = GetComponent<RectTransform>();
            _cg   = GetComponent<CanvasGroup>();
        }

        StopCurrent();
        SnapHidden();
        // Never SetActive(false) — keep GO active for coroutines
    }

    public bool IsVisible => _cg != null && _cg.alpha > 0f;

    // ── Helpers ───────────────────────────────────────────────────────────────

    void SnapHidden()
    {
        if (_cg == null) return;
        _cg.alpha          = 0f;
        _cg.interactable   = false;
        _cg.blocksRaycasts = false;
        if (_rect != null) _rect.anchoredPosition = Vector2.zero;
    }

    void StopCurrent()
    {
        if (_current == null) return;
        StopCoroutine(_current);
        _current = null;
    }

    // ── Coroutines ────────────────────────────────────────────────────────────

    IEnumerator AnimateIn(AnimationType type, float duration, Action onComplete)
    {
        Vector2 startPos = StartOffset(type);
        float elapsed = 0f;

        _cg.alpha = 0f;
        if (_rect != null) _rect.anchoredPosition = startPos;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
            _cg.alpha = t;
            if (_rect != null && type != AnimationType.Fade)
                _rect.anchoredPosition = Vector2.LerpUnclamped(startPos, Vector2.zero, t);
            yield return null;
        }

        _cg.alpha = 1f;
        if (_rect != null) _rect.anchoredPosition = Vector2.zero;
        onComplete?.Invoke();
    }

    IEnumerator AnimateOut(AnimationType type, float duration, Action onComplete)
    {
        Vector2 endPos = EndOffset(type);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
            _cg.alpha = 1f - t;
            if (_rect != null && type != AnimationType.Fade)
                _rect.anchoredPosition = Vector2.LerpUnclamped(Vector2.zero, endPos, t);
            yield return null;
        }

        SnapHidden();
        onComplete?.Invoke();
    }

    // ── Offset helpers ────────────────────────────────────────────────────────

    Vector2 StartOffset(AnimationType type)
    {
        switch (type)
        {
            case AnimationType.Rise: return new Vector2(0f, -slideDistance);
            case AnimationType.Pan:  return PanOffset(-slideDistance);
            default:                 return Vector2.zero;
        }
    }

    Vector2 EndOffset(AnimationType type)
    {
        switch (type)
        {
            case AnimationType.Rise: return new Vector2(0f, -slideDistance);
            case AnimationType.Pan:  return PanOffset(slideDistance);
            default:                 return Vector2.zero;
        }
    }

    Vector2 PanOffset(float distance)
    {
        switch (panDirection)
        {
            case PanDirection.Left:  return new Vector2(-distance, 0f);
            case PanDirection.Right: return new Vector2( distance, 0f);
            case PanDirection.Up:    return new Vector2(0f,  distance);
            default:                 return new Vector2(0f, -distance);
        }
    }
}
