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
    [SerializeField] private TMP_Text clearConditionsText;
    [SerializeField] private TMP_Text statsText;

    [Header("Action Buttons")]
    [SerializeField] private Button retryButton;
    [SerializeField] private Button exitButton;

    [Header("Entrance")]
    [SerializeField] private RectTransform resultContentPanel;
    [SerializeField] private RectTransform winLoseImage;
    [SerializeField] private float slideDistance = 700f;
    [SerializeField] private float slideDuration = 0.45f;

    Coroutine entrance;

    public void Show(
        string failReason,
        int instantsUsed,
        int unitsLost,
        UnityAction onRetry,
        UnityAction onExit,
        string clearConditionSummary = null,
        int maxInstants = 150,
        int maxAllyDeathsAllowed = -1)
    {
        gameObject.SetActive(true);
        if (entrance != null)
            StopCoroutine(entrance);
        entrance = StartCoroutine(ResultEntrance.Play(resultContentPanel, winLoseImage, slideDistance, slideDuration));

        if (titleText != null && string.IsNullOrEmpty(titleText.text))
            titleText.text = "DEFEAT";

        if (reasonText != null)
        {
            reasonText.text = string.IsNullOrEmpty(failReason)
                ? "Your squad could not overcome the enemy."
                : failReason;
        }

        if (clearConditionsText != null)
        {
            if (string.IsNullOrEmpty(clearConditionSummary))
            {
                int limit = maxInstants > 0 ? maxInstants : 150;
                clearConditionSummary = maxAllyDeathsAllowed >= 0
                    ? $"Clear Condition: Defeat all enemies  •  Max Allies Fallen: <= {maxAllyDeathsAllowed}  •  Limit: {limit} Instants"
                    : $"Clear Condition: Defeat all enemies within {limit} Instants";
            }
            clearConditionsText.text = clearConditionSummary;
        }

        if (statsText != null)
        {
            int effectiveMaxInstants = maxInstants > 0 ? maxInstants : 150;
            bool instantExceeded = instantsUsed > effectiveMaxInstants;
            string instantPart = instantExceeded
                ? $"<color=#FF6B6B>Instants Elapsed: {instantsUsed} / {effectiveMaxInstants}</color>"
                : $"Instants Elapsed: {instantsUsed} / {effectiveMaxInstants}";

            string fallenPart;
            if (maxAllyDeathsAllowed >= 0)
            {
                bool fallenExceeded = unitsLost > maxAllyDeathsAllowed;
                fallenPart = fallenExceeded
                    ? $"<color=#FF6B6B>Allies Fallen: {unitsLost} / {maxAllyDeathsAllowed}</color>"
                    : $"Allies Fallen: {unitsLost} / {maxAllyDeathsAllowed}";
            }
            else
            {
                fallenPart = $"Allies Fallen: {unitsLost}";
            }

            statsText.text = $"{instantPart}   |   {fallenPart}";
        }

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
