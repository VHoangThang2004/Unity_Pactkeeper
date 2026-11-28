using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class StoryStageNode : MonoBehaviour
{
    [SerializeField] private TMP_Text numberText;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private Image indicator;
    [SerializeField] private Sprite normalIndicator;
    [SerializeField] private Sprite finalIndicator;
    [SerializeField] private Button button;

    public void Setup(int chapterId, int sceneId, string sceneName, bool isLastScene, bool clickable, Action<int, int> onClick)
    {
        if (numberText != null)
            numberText.text = $"{chapterId}.{sceneId}";
        if (nameText != null)
            nameText.text = sceneName ?? "";

        if (indicator != null)
        {
            Sprite sprite = isLastScene ? finalIndicator : normalIndicator;
            if (sprite != null)
                indicator.sprite = sprite;
        }

        if (button == null)
            button = GetComponent<Button>();
        if (button == null)
            return;

        button.onClick.RemoveAllListeners();
        button.interactable = clickable;
        if (clickable)
            button.onClick.AddListener(() => onClick?.Invoke(chapterId, sceneId));
    }
}
