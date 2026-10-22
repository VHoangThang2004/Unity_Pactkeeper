using System;
using UnityEngine;

/// <summary>
/// Extension helpers so any MonoBehaviour can open/close a panel
/// with animation in one line.
///
/// Automatically falls back to plain SetActive when no PanelAnimator
/// component is present — so adding the component is fully optional.
///
/// Usage:
///   this.OpenPanel(panel);
///   this.ClosePanel(panel);
///   this.ClosePanel(panel, () => StartCoroutine(Reload()));
///   this.HidePanelImmediate(panel);  // use in Awake, safe with no ordering dep
/// </summary>
public static class PanelAnimatorExtensions
{
    /// <summary>
    /// Activate and animate a panel open.
    /// Panel does NOT need to be active beforehand.
    /// </summary>
    public static void OpenPanel(this MonoBehaviour _, GameObject panel, Action onComplete = null)
    {
        if (panel == null) return;

        var animator = panel.GetComponent<PanelAnimator>();
        if (animator != null)
        {
            animator.Open(onComplete);
        }
        else
        {
            panel.SetActive(true);
            onComplete?.Invoke();
        }
    }

    /// <summary>
    /// Animate a panel closed, then deactivate it.
    /// The coroutine runs on `this` (the calling manager) so it survives
    /// the panel being deactivated mid-animation.
    /// </summary>
    public static void ClosePanel(this MonoBehaviour owner, GameObject panel, Action onComplete = null)
    {
        if (panel == null) return;

        var animator = panel.GetComponent<PanelAnimator>();
        if (animator != null)
        {
            animator.Close(owner, onComplete);
        }
        else
        {
            panel.SetActive(false);
            onComplete?.Invoke();
        }
    }

    /// <summary>
    /// Instantly hide without animation.
    /// Safe to call from Awake() — no coroutine, no ordering dependency.
    /// </summary>
    public static void HidePanelImmediate(this MonoBehaviour _, GameObject panel)
    {
        if (panel == null) return;

        var animator = panel.GetComponent<PanelAnimator>();
        if (animator != null)
            animator.HideImmediate();
        else
            panel.SetActive(false);
    }
}
