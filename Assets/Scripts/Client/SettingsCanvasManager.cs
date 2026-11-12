using UnityEngine;
using UnityEngine.UI;

public class SettingsCanvasManager : MonoBehaviour
{
    [SerializeField] private SettingsManager settingsManager;
    [SerializeField] private Button forfeitButton;
    private bool isForfeiting = false;

    void Awake()
    {
        if (forfeitButton == null)
        {
            var buttons = GetComponentsInChildren<Button>(true);
            foreach (var b in buttons)
            {
                if (b.gameObject.name == "ForfeitButton")
                {
                    forfeitButton = b;
                    break;
                }
            }
        }

        if (forfeitButton != null)
        {
            forfeitButton.onClick.RemoveListener(OnForfeitClicked);
            forfeitButton.onClick.AddListener(OnForfeitClicked);
        }
    }

    public void OpenSettings()
    {
        if (settingsManager != null)
            settingsManager.Open();
    }

    public void CloseSettings()
    {
        if (settingsManager != null)
            settingsManager.Close();
    }

    public void OnForfeitClicked()
    {
        if (isForfeiting) return;
        isForfeiting = true;

        Debug.Log("[SettingsCanvasManager] Forfeit button clicked.");
        if (forfeitButton != null)
            forfeitButton.interactable = false;

        var clientController = FindAnyObjectByType<ClientController>();
        if (clientController != null)
        {
            clientController.RequestForfeit();
        }
        else
        {
            Debug.LogWarning("[SettingsCanvasManager] ClientController not found for forfeit.");
            isForfeiting = false;
            if (forfeitButton != null)
                forfeitButton.interactable = true;
        }

        CloseSettings();
    }
}
