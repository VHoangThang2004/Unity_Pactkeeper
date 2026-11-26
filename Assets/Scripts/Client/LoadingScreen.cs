using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class LoadingScreen : MonoBehaviour
{
    [SerializeField] private GameObject panel;
    [SerializeField] private TextMeshProUGUI loadingText;
    [SerializeField] private Image backgroundImage;
    [SerializeField] private Sprite loadingBackground;
    [SerializeField] private Sprite summoningBackground;
    [SerializeField] private Sprite connectingBackground;

    public bool IsVisible => panel != null && panel.activeInHierarchy;

    public void Show(string message = null)
    {
        if (panel != null) panel.SetActive(true);
        string text = message ?? Loc.Get(LocTables.Shared, LocKeys.Shared.Loading);
        if (loadingText != null)
            loadingText.text = text;
        ApplyBackground(text);
    }

    public void Hide()
    {
        if (panel != null) panel.SetActive(false);
    }

    void ApplyBackground(string message)
    {
        if (backgroundImage == null)
            return;

        Sprite sprite = loadingBackground;
        string text = string.IsNullOrEmpty(message) ? "" : message.ToLowerInvariant();
        if (text.Contains("summon"))
            sprite = summoningBackground;
        else if (text.Contains("connect"))
            sprite = connectingBackground;

        if (sprite != null)
            backgroundImage.sprite = sprite;
    }
}
