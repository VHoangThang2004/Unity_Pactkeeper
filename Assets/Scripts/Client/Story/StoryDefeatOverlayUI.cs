using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Prefab-backed Defeat / Retry / Exit overlay for Story PvE battles.
/// Attach to the root Canvas of Assets/Prefabs/UI/StoryDefeatOverlay.prefab
/// so designers can swap background/card/button sprites directly in the Prefab.
/// </summary>
public class StoryDefeatOverlayUI : MonoBehaviour
{
    [Header("Text References")]
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text reasonText;
    [SerializeField] private TMP_Text statsText;

    [Header("Action Buttons")]
    [SerializeField] private Button retryButton;
    [SerializeField] private Button exitButton;

    public void Show(
        string failReason,
        int instantsUsed,
        int unitsLost,
        UnityAction onRetry,
        UnityAction onExit)
    {
        gameObject.SetActive(true);

        if (titleText != null && string.IsNullOrEmpty(titleText.text))
            titleText.text = "DEFEAT";

        if (reasonText != null)
        {
            reasonText.text = string.IsNullOrEmpty(failReason)
                ? "Your squad could not overcome the enemy."
                : failReason;
        }

        if (statsText != null)
            statsText.text = $"Instants Elapsed: {instantsUsed}   |   Allies Fallen: {unitsLost}";

        if (retryButton != null)
        {
            retryButton.onClick.RemoveAllListeners();
            if (onRetry != null)
                retryButton.onClick.AddListener(onRetry);
        }

        if (exitButton != null)
        {
            exitButton.onClick.RemoveAllListeners();
            if (onExit != null)
                exitButton.onClick.AddListener(onExit);
        }
    }
}
