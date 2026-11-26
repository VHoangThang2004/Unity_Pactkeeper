using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Prefab-backed Victory overlay for Story PvE battles.
/// Attach to the root Canvas of Assets/Prefabs/UI/StoryWinOverlay.prefab
/// so designers can customize background/card/button sprites directly in the Prefab.
/// </summary>
public class StoryWinOverlayUI : MonoBehaviour
{
    [Header("Text References")]
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text subtitleText;
    [SerializeField] private TMP_Text clearConditionsText;
    [SerializeField] private TMP_Text statsText;

    [Header("Action Buttons")]
    [SerializeField] private Button continueButton;

    [Header("Entrance")]
    [SerializeField] private RectTransform resultContentPanel;
    [SerializeField] private RectTransform winLoseImage;
    [SerializeField] private float slideDistance = 700f;
    [SerializeField] private float slideDuration = 0.45f;

    Coroutine entrance;

    public void Show(
        string subtitle,
        int instantsUsed,
        int unitsLost,
        UnityAction onContinue,
        string clearConditionSummary = null,
        int maxInstants = 150,
        int maxAllyDeathsAllowed = -1)
    {
        gameObject.SetActive(true);
        if (entrance != null)
            StopCoroutine(entrance);
        entrance = StartCoroutine(ResultEntrance.Play(resultContentPanel, winLoseImage, slideDistance, slideDuration));

        if (titleText != null && string.IsNullOrEmpty(titleText.text))
            titleText.text = "VICTORY";

        if (subtitleText != null)
        {
            subtitleText.text = string.IsNullOrEmpty(subtitle)
                ? "All enemies defeated! Stage cleared."
                : subtitle;
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
            string instantPart = $"Instants Elapsed: {instantsUsed} / {effectiveMaxInstants}";
            string fallenPart = maxAllyDeathsAllowed >= 0
                ? $"Allies Fallen: {unitsLost} / {maxAllyDeathsAllowed}"
                : $"Allies Fallen: {unitsLost}";

            statsText.text = $"{instantPart}   |   {fallenPart}";
        }

        if (continueButton != null)
        {
            continueButton.onClick.RemoveAllListeners();
            if (onContinue != null)
                continueButton.onClick.AddListener(onContinue);
        }
    }
}
