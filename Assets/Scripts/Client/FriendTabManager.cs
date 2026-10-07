using UnityEngine;
using UnityEngine.UI;

public class FriendTabManager : MonoBehaviour
{
    [Header("Panel")]
    [SerializeField] private GameObject panel;
    [SerializeField] private Button closeButton;

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
