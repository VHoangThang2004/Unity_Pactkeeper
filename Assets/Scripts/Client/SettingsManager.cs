using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;
using TMPro;
using System;

public class SettingsManager : MonoBehaviour
{
    [Header("Panel")]
    [SerializeField] private GameObject panel;
    [SerializeField] private Button closeButton;
    [SerializeField] private Button saveButton;
    [SerializeField] private Button resetButton;

    [Header("Left Panel - Category Buttons")]
    [SerializeField] private Button audioButton;
    [SerializeField] private Button graphicsButton;
    [SerializeField] private Button accountButton;
    [SerializeField] private Button languageButton;

    [Header("Right Panel - Content Panels")]
    [SerializeField] private GameObject audioPanel;
    [SerializeField] private GameObject graphicsPanel;
    [SerializeField] private GameObject accountPanel;
    [SerializeField] private GameObject languagePanel;

    [Header("Audio Settings")]
    [SerializeField] private Slider masterVolumeSlider;
    [SerializeField] private Slider musicVolumeSlider;
    [SerializeField] private Slider sfxVolumeSlider;
    [SerializeField] private Toggle muteAllToggle;

    [Header("Graphics Settings")]
    [SerializeField] private TMP_Dropdown qualityDropdown;
    [SerializeField] private TMP_Dropdown resolutionDropdown;
    [SerializeField] private Toggle fullscreenToggle;
    [SerializeField] private Toggle vsyncToggle;
    [SerializeField] private Toggle fpsCounterToggle;

    [Header("Account Settings")]
    [SerializeField] private TMP_Text usernameText;
    [SerializeField] private Button logoutButton;

    [Header("Language Settings")]
    [SerializeField] private TMP_Dropdown languageDropdown;

    [Header("FPS Counter")]
    [SerializeField] private GameObject fpsCounterPanel;
    [SerializeField] private TMP_Text fpsText;

    private Resolution[] resolutions;
    private bool settingsChanged = false;
    private GameObject currentPanel = null;

    void Awake()
    {
        if (closeButton != null)
            closeButton.onClick.AddListener(Close);
        if (saveButton != null)
            saveButton.onClick.AddListener(SaveSettings);
        if (resetButton != null)
            resetButton.onClick.AddListener(ResetToDefault);
        if (logoutButton != null)
            logoutButton.onClick.AddListener(Logout);

        // Setup category buttons
        if (audioButton != null)
            audioButton.onClick.AddListener(() => ShowPanel(audioPanel));
        if (graphicsButton != null)
            graphicsButton.onClick.AddListener(() => ShowPanel(graphicsPanel));
        if (accountButton != null)
            accountButton.onClick.AddListener(() => ShowPanel(accountPanel));
        if (languageButton != null)
            languageButton.onClick.AddListener(() => ShowPanel(languagePanel));

        // Setup audio sliders
        if (masterVolumeSlider != null)
            masterVolumeSlider.onValueChanged.AddListener(OnMasterVolumeChanged);
        if (musicVolumeSlider != null)
            musicVolumeSlider.onValueChanged.AddListener(OnMusicVolumeChanged);
        if (sfxVolumeSlider != null)
            sfxVolumeSlider.onValueChanged.AddListener(OnSfxVolumeChanged);
        if (muteAllToggle != null)
            muteAllToggle.onValueChanged.AddListener(OnMuteAllChanged);

        // Setup graphics toggles
        if (fullscreenToggle != null)
            fullscreenToggle.onValueChanged.AddListener(OnFullscreenChanged);
        if (vsyncToggle != null)
            vsyncToggle.onValueChanged.AddListener(OnVSyncChanged);
        if (fpsCounterToggle != null)
            fpsCounterToggle.onValueChanged.AddListener(OnFpsCounterChanged);

        HidePanel();
    }

    void OnDestroy()
    {
        if (closeButton != null)
            closeButton.onClick.RemoveListener(Close);
        if (saveButton != null)
            saveButton.onClick.RemoveListener(SaveSettings);
        if (resetButton != null)
            resetButton.onClick.RemoveListener(ResetToDefault);
        if (logoutButton != null)
            logoutButton.onClick.RemoveListener(Logout);

        if (audioButton != null)
            audioButton.onClick.RemoveAllListeners();
        if (graphicsButton != null)
            graphicsButton.onClick.RemoveAllListeners();
        if (accountButton != null)
            accountButton.onClick.RemoveAllListeners();
        if (languageButton != null)
            languageButton.onClick.RemoveAllListeners();

        if (masterVolumeSlider != null)
            masterVolumeSlider.onValueChanged.RemoveListener(OnMasterVolumeChanged);
        if (musicVolumeSlider != null)
            musicVolumeSlider.onValueChanged.RemoveListener(OnMusicVolumeChanged);
        if (sfxVolumeSlider != null)
            sfxVolumeSlider.onValueChanged.RemoveListener(OnSfxVolumeChanged);
        if (muteAllToggle != null)
            muteAllToggle.onValueChanged.RemoveListener(OnMuteAllChanged);

        if (fullscreenToggle != null)
            fullscreenToggle.onValueChanged.RemoveListener(OnFullscreenChanged);
        if (vsyncToggle != null)
            vsyncToggle.onValueChanged.RemoveListener(OnVSyncChanged);
        if (fpsCounterToggle != null)
            fpsCounterToggle.onValueChanged.RemoveListener(OnFpsCounterChanged);
    }

    void Start()
    {
        LoadResolutions();
        LoadSettings();
        UpdateUsernameDisplay();
    }

    void Update()
    {
        if (fpsCounterPanel != null && fpsCounterPanel.activeSelf && fpsText != null)
        {
            fpsText.text = $"{(int)(1f / Time.deltaTime)} FPS";
        }
    }

    public void Open()
    {
        if (panel != null)
            panel.SetActive(true);
        LoadSettings();
        ShowPanel(audioPanel); // Default to audio panel
    }

    public void Close()
    {
        if (settingsChanged)
        {
            // Optional: Ask user to save before closing
            // For now, auto-save
            SaveSettings();
        }
        HidePanel();
        HideAllContentPanels();
    }

    void HidePanel()
    {
        if (panel != null)
            panel.SetActive(false);
    }

    void ShowPanel(GameObject targetPanel)
    {
        if (targetPanel == null)
            return;

        // Hide all panels
        HideAllContentPanels();

        // Show target panel
        targetPanel.SetActive(true);
        currentPanel = targetPanel;
    }

    void HideAllContentPanels()
    {
        if (audioPanel != null)
            audioPanel.SetActive(false);
        if (graphicsPanel != null)
            graphicsPanel.SetActive(false);
        if (accountPanel != null)
            accountPanel.SetActive(false);
        if (languagePanel != null)
            languagePanel.SetActive(false);
        currentPanel = null;
    }

    // -------------------------------------------------------
    // Audio Settings
    // -------------------------------------------------------

    void OnMasterVolumeChanged(float value)
    {
        settingsChanged = true;
        ApplyAudioSettings();
    }

    void OnMusicVolumeChanged(float value)
    {
        settingsChanged = true;
        ApplyAudioSettings();
    }

    void OnSfxVolumeChanged(float value)
    {
        settingsChanged = true;
        ApplyAudioSettings();
    }

    void OnMuteAllChanged(bool value)
    {
        settingsChanged = true;
        ApplyAudioSettings();
    }

    void ApplyAudioSettings()
    {
        if (AudioManager.Instance == null)
            return;

        bool isMuted = muteAllToggle != null && muteAllToggle.isOn;
        float masterVolume = masterVolumeSlider != null ? masterVolumeSlider.value : 1f;
        float musicVolume = musicVolumeSlider != null ? musicVolumeSlider.value : 1f;
        float sfxVolume = sfxVolumeSlider != null ? sfxVolumeSlider.value : 1f;

        if (isMuted)
        {
            AudioListener.volume = 0f;
        }
        else
        {
            AudioListener.volume = masterVolume;
        }

        // Note: AudioManager doesn't have volume controls exposed
        // You may need to add volume control to AudioManager
        // For now, we use AudioListener.volume as master
    }

    // -------------------------------------------------------
    // Graphics Settings
    // -------------------------------------------------------

    void LoadResolutions()
    {
        resolutions = Screen.resolutions;
        if (resolutionDropdown == null)
            return;

        resolutionDropdown.ClearOptions();

        var options = new System.Collections.Generic.List<string>();
        int currentResolutionIndex = 0;

        for (int i = 0; i < resolutions.Length; i++)
        {
            string option = $"{resolutions[i].width} x {resolutions[i].height}";
            options.Add(option);

            if (resolutions[i].width == Screen.currentResolution.width &&
                resolutions[i].height == Screen.currentResolution.height)
            {
                currentResolutionIndex = i;
            }
        }

        resolutionDropdown.AddOptions(options);
        resolutionDropdown.value = currentResolutionIndex;
        resolutionDropdown.RefreshShownValue();

        if (resolutionDropdown != null)
            resolutionDropdown.onValueChanged.AddListener(OnResolutionChanged);
    }

    void OnResolutionChanged(int index)
    {
        settingsChanged = true;
        Resolution resolution = resolutions[index];
        Screen.SetResolution(resolution.width, resolution.height, Screen.fullScreen);
    }

    void OnFullscreenChanged(bool value)
    {
        settingsChanged = true;
        Screen.fullScreen = value;
    }

    void OnVSyncChanged(bool value)
    {
        settingsChanged = true;
        QualitySettings.vSyncCount = value ? 1 : 0;
    }

    void OnFpsCounterChanged(bool value)
    {
        settingsChanged = true;
        if (fpsCounterPanel != null)
            fpsCounterPanel.SetActive(value);
    }

    void OnQualityChanged(int index)
    {
        settingsChanged = true;
        QualitySettings.SetQualityLevel(index);
    }

    // -------------------------------------------------------
    // Language Settings
    // -------------------------------------------------------

    void OnLanguageChanged(int index)
    {
        settingsChanged = true;
        // TODO: Implement language change logic
        // This would require a localization system
    }

    // -------------------------------------------------------
    // Account Settings
    // -------------------------------------------------------

    void UpdateUsernameDisplay()
    {
        if (usernameText != null)
            usernameText.text = PlayerSession.Username;
    }

    void Logout()
    {
        PlayerSession.Clear();
        UnityEngine.SceneManagement.SceneManager.LoadScene("1_Login");
    }

    // -------------------------------------------------------
    // Save/Load Settings
    // -------------------------------------------------------

    public void SaveSettings()
    {
        // Audio
        if (masterVolumeSlider != null)
            PlayerPrefs.SetFloat("MasterVolume", masterVolumeSlider.value);
        if (musicVolumeSlider != null)
            PlayerPrefs.SetFloat("MusicVolume", musicVolumeSlider.value);
        if (sfxVolumeSlider != null)
            PlayerPrefs.SetFloat("SfxVolume", sfxVolumeSlider.value);
        if (muteAllToggle != null)
            PlayerPrefs.SetInt("MuteAll", muteAllToggle.isOn ? 1 : 0);

        // Graphics
        if (qualityDropdown != null)
            PlayerPrefs.SetInt("QualityLevel", qualityDropdown.value);
        if (resolutionDropdown != null)
            PlayerPrefs.SetInt("ResolutionIndex", resolutionDropdown.value);
        if (fullscreenToggle != null)
            PlayerPrefs.SetInt("Fullscreen", fullscreenToggle.isOn ? 1 : 0);
        if (vsyncToggle != null)
            PlayerPrefs.SetInt("VSync", vsyncToggle.isOn ? 1 : 0);
        if (fpsCounterToggle != null)
            PlayerPrefs.SetInt("FpsCounter", fpsCounterToggle.isOn ? 1 : 0);

        // Language
        if (languageDropdown != null)
            PlayerPrefs.SetInt("LanguageIndex", languageDropdown.value);

        PlayerPrefs.Save();
        settingsChanged = false;
        Debug.Log("[Settings] Settings saved");
    }

    public void LoadSettings()
    {
        // Audio
        if (masterVolumeSlider != null)
            masterVolumeSlider.value = PlayerPrefs.GetFloat("MasterVolume", 1f);
        if (musicVolumeSlider != null)
            musicVolumeSlider.value = PlayerPrefs.GetFloat("MusicVolume", 1f);
        if (sfxVolumeSlider != null)
            sfxVolumeSlider.value = PlayerPrefs.GetFloat("SfxVolume", 1f);
        if (muteAllToggle != null)
            muteAllToggle.isOn = PlayerPrefs.GetInt("MuteAll", 0) == 1;

        // Graphics
        if (qualityDropdown != null)
            qualityDropdown.value = PlayerPrefs.GetInt("QualityLevel", QualitySettings.GetQualityLevel());
        if (resolutionDropdown != null)
            resolutionDropdown.value = PlayerPrefs.GetInt("ResolutionIndex", 0);
        if (fullscreenToggle != null)
            fullscreenToggle.isOn = PlayerPrefs.GetInt("Fullscreen", Screen.fullScreen ? 1 : 0) == 1;
        if (vsyncToggle != null)
            vsyncToggle.isOn = PlayerPrefs.GetInt("VSync", QualitySettings.vSyncCount > 0 ? 1 : 0) == 1;
        if (fpsCounterToggle != null)
            fpsCounterToggle.isOn = PlayerPrefs.GetInt("FpsCounter", 0) == 1;

        // Language
        if (languageDropdown != null)
            languageDropdown.value = PlayerPrefs.GetInt("LanguageIndex", 0);

        // Apply loaded settings
        ApplyAudioSettings();

        // Apply graphics settings
        if (qualityDropdown != null)
            OnQualityChanged(qualityDropdown.value);
        if (resolutionDropdown != null && resolutions != null && resolutionDropdown.value < resolutions.Length)
            OnResolutionChanged(resolutionDropdown.value);
        if (fullscreenToggle != null)
            OnFullscreenChanged(fullscreenToggle.isOn);
        if (vsyncToggle != null)
            OnVSyncChanged(vsyncToggle.isOn);
        if (fpsCounterToggle != null)
            OnFpsCounterChanged(fpsCounterToggle.isOn);

        // Setup language dropdown listener
        if (languageDropdown != null)
        {
            languageDropdown.onValueChanged.RemoveAllListeners();
            languageDropdown.onValueChanged.AddListener(OnLanguageChanged);
        }

        // Setup quality dropdown listener
        if (qualityDropdown != null)
        {
            qualityDropdown.onValueChanged.RemoveAllListeners();
            qualityDropdown.onValueChanged.AddListener(OnQualityChanged);
        }

        settingsChanged = false;
        Debug.Log("[Settings] Settings loaded");
    }

    public void ResetToDefault()
    {
        // Audio
        if (masterVolumeSlider != null)
            masterVolumeSlider.value = 1f;
        if (musicVolumeSlider != null)
            musicVolumeSlider.value = 1f;
        if (sfxVolumeSlider != null)
            sfxVolumeSlider.value = 1f;
        if (muteAllToggle != null)
            muteAllToggle.isOn = false;

        // Graphics
        if (qualityDropdown != null)
            qualityDropdown.value = QualitySettings.GetQualityLevel();
        if (resolutionDropdown != null)
            resolutionDropdown.value = 0;
        if (fullscreenToggle != null)
            fullscreenToggle.isOn = true;
        if (vsyncToggle != null)
            vsyncToggle.isOn = true;
        if (fpsCounterToggle != null)
            fpsCounterToggle.isOn = false;

        // Language
        if (languageDropdown != null)
            languageDropdown.value = 0;

        ApplyAudioSettings();
        settingsChanged = true;
        Debug.Log("[Settings] Settings reset to default");
    }
}
