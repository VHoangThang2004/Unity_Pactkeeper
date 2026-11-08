using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Represents a single selectable scene row inside a ChapterCard's Accordion Dropdown.
/// Displays "[Chapter.Scene] - [English Title]" and enforces unlock interactability.
/// </summary>
public class SceneDropdownItemUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Button   selectButton;
    [SerializeField] private TMP_Text sceneTitleText;     // e.g. "0.1 - The Patience Lesson"
    [SerializeField] private TMP_Text statusIconText;     // "COMPLETED" | "PLAY" | "LOCKED"
    [SerializeField] private Image    backgroundImage;
    [SerializeField] private GameObject lockIconObject;

    [Header("Row Colors")]
    [SerializeField] private Color colorCompleted = new Color(0.16f, 0.38f, 0.24f, 0.90f);
    [SerializeField] private Color colorAvailable = new Color(0.28f, 0.24f, 0.14f, 0.95f);
    [SerializeField] private Color colorLocked    = new Color(0.15f, 0.15f, 0.15f, 0.70f);

    private int _chapterId;
    private int _sceneId;
    private Action<int, int> _onSceneClicked;

    public void Setup(
        int chapterId,
        int sceneId,
        string englishDisplayTitle,
        bool isCompleted,
        bool isUnlocked,
        Action<int, int> onSceneClicked)
    {
        _chapterId = chapterId;
        _sceneId = sceneId;
        _onSceneClicked = onSceneClicked;

        if (sceneTitleText != null)
            sceneTitleText.text = englishDisplayTitle;

        if (selectButton == null)
            selectButton = GetComponent<Button>();

        if (selectButton != null)
        {
            selectButton.interactable = isUnlocked;
            selectButton.onClick.RemoveAllListeners();
            selectButton.onClick.AddListener(HandleClick);
        }

        if (lockIconObject != null)
            lockIconObject.SetActive(!isUnlocked);

        if (statusIconText != null)
        {
            if (!isUnlocked)
                statusIconText.text = "LOCKED";
            else if (isCompleted)
                statusIconText.text = "COMPLETED";
            else
                statusIconText.text = "PLAY";
        }

        if (backgroundImage != null)
        {
            if (!isUnlocked)
                backgroundImage.color = colorLocked;
            else if (isCompleted)
                backgroundImage.color = colorCompleted;
            else
                backgroundImage.color = colorAvailable;
        }
    }

    private void HandleClick()
    {
        _onSceneClicked?.Invoke(_chapterId, _sceneId);
    }
}
