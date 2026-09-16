using UnityEngine;

public class SettingsCanvasManager : MonoBehaviour
{
    [SerializeField] private SettingsManager settingsManager;

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
}
