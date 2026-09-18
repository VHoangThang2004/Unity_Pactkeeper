using UnityEngine;
using TMPro;

public class LoadingScreen : MonoBehaviour
{
    [SerializeField] private GameObject panel;
    [SerializeField] private TextMeshProUGUI loadingText;

    public void Show(string message = "Loading...")
    {
        if (panel != null) panel.SetActive(true);
        if (loadingText != null) loadingText.text = message;
    }

    public void Hide()
    {
        if (panel != null) panel.SetActive(false);
    }
}