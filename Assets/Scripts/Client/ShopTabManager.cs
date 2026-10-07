using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ShopTabManager : MonoBehaviour
{
    [Header("Panel")]
    [SerializeField] private GameObject panel;
    [SerializeField] private Button closeButton;

    [Header("Currency Display")]
    [SerializeField] private TMP_Text currencyText;

    private TMP_Text mainMenuGemsText;

    void Awake()
    {
        if (closeButton != null)
            closeButton.onClick.AddListener(Close);

        HidePanel();
    }

    void OnDestroy()
    {
        if (closeButton != null)
            closeButton.onClick.RemoveListener(Close);
    }

    public void Open()
    {
        // Find gems text from MainMenu (same as MainMenuManager does)
        if (mainMenuGemsText == null)
        {
            var gemObj = GameObject.Find("Gem");
            if (gemObj != null)
                mainMenuGemsText = gemObj.GetComponentInChildren<TMP_Text>();
        }

        // Update currency display with gems from MainMenu
        if (currencyText != null && mainMenuGemsText != null)
        {
            currencyText.text = mainMenuGemsText.text;
        }

        if (panel != null)
            panel.SetActive(true);
    }

    public void Close()
    {
        HidePanel();
    }

    void HidePanel()
    {
        if (panel != null)
            panel.SetActive(false);
    }
}
