using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// Prefab view component attached to Assets/Prefabs/UI/ReplayBarPrefab.prefab.
/// Holds references to the container and its "Skip Scene" / "Exit to Map" buttons
/// so layout, sprites, colors, and positioning can be edited directly in Prefab Mode.
/// </summary>
public class StoryReplayBarUI : MonoBehaviour
{
    [SerializeField] private RectTransform barContainer;
    [SerializeField] private Button skipSceneButton;
    [SerializeField] private Button exitToMapButton;

    public void Configure(bool showSkipButton, UnityAction onSkipClicked, UnityAction onExitClicked)
    {
        if (skipSceneButton != null)
        {
            skipSceneButton.onClick.RemoveAllListeners();
            if (onSkipClicked != null)
                skipSceneButton.onClick.AddListener(onSkipClicked);
            skipSceneButton.gameObject.SetActive(showSkipButton);
        }

        if (exitToMapButton != null)
        {
            exitToMapButton.onClick.RemoveAllListeners();
            if (onExitClicked != null)
                exitToMapButton.onClick.AddListener(onExitClicked);
            exitToMapButton.gameObject.SetActive(true);
        }

        if (barContainer != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(barContainer);
    }

    public void SetButtonsInteractable(bool interactable)
    {
        if (skipSceneButton != null)
            skipSceneButton.interactable = interactable;
        if (exitToMapButton != null)
            exitToMapButton.interactable = interactable;
    }

    public void SetVisible(bool visible)
    {
        GameObject target = barContainer != null ? barContainer.gameObject : gameObject;
        if (target.activeSelf != visible)
            target.SetActive(visible);
    }
}
