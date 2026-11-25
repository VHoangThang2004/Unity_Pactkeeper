using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SettingRemakeManager : MonoBehaviour
{
    [Header("Panel")]
    [SerializeField] private GameObject panel;
    [SerializeField] private Button closeButton;
    [SerializeField] private Button saveButton;
    [SerializeField] private Button quitButton;
    [SerializeField] private Button logoutButton;
    [SerializeField] private Button surrenderButton;

    [Header("Confirm")]
    [SerializeField] private GameObject confirmPanel;
    [SerializeField] private TMP_Text confirmText;
    [SerializeField] private Button confirmYesButton;
    [SerializeField] private Button confirmNoButton;

    [Header("Left Tabs")]
    [SerializeField] private Button audioButton;
    [SerializeField] private Button controlsButton;
    [SerializeField] private GameObject audioPanel;
    [SerializeField] private GameObject controlsPanel;

    [Header("Audio")]
    [SerializeField] private Slider masterVolumeSlider;
    [SerializeField] private Slider musicVolumeSlider;
    [SerializeField] private Slider sfxVolumeSlider;
    [SerializeField] private Scrollbar muteAllScrollbar;
    [SerializeField] private Image muteBackground;
    [SerializeField] private Sprite muteOffSprite;
    [SerializeField] private Sprite muteOnSprite;

    [Header("Gameplay")]
    [SerializeField] private Button battleSpeedButton;
    [SerializeField] private TMP_Text battleSpeedText;
    [SerializeField] private Scrollbar fullscreenScrollbar;
    [SerializeField] private Image fullscreenBackground;
    [SerializeField] private Sprite fullscreenOffSprite;
    [SerializeField] private Sprite fullscreenOnSprite;

    const int ResolutionWidth = 1920;
    const int ResolutionHeight = 1080;
    static readonly Color32 ActiveTabColor = new Color32(0xDC, 0xDC, 0xDC, 0xFF);

    bool settingsChanged;
    bool isForfeiting;
    bool applyingUi;
    bool audioTabSelected = true;
    Color audioDefaultColor = Color.white;
    Color controlsDefaultColor = Color.white;
    GameObject outsideClick;
    ConfirmAction pendingConfirm;

    enum ConfirmAction { None, Logout, Quit, Surrender }

    void Awake()
    {
        if (closeButton != null)
            closeButton.onClick.AddListener(Close);
        if (saveButton != null)
            saveButton.onClick.AddListener(SaveAndClose);
        if (quitButton != null)
            quitButton.onClick.AddListener(() => ShowConfirm(ConfirmAction.Quit));
        if (logoutButton != null)
            logoutButton.onClick.AddListener(() => ShowConfirm(ConfirmAction.Logout));
        if (surrenderButton != null)
            surrenderButton.onClick.AddListener(() => ShowConfirm(ConfirmAction.Surrender));
        if (confirmYesButton != null)
            confirmYesButton.onClick.AddListener(ConfirmYes);
        if (confirmNoButton != null)
            confirmNoButton.onClick.AddListener(HideConfirm);

        if (audioButton != null)
            audioButton.onClick.AddListener(ShowAudio);
        if (controlsButton != null)
            controlsButton.onClick.AddListener(ShowControls);

        if (masterVolumeSlider != null)
            masterVolumeSlider.onValueChanged.AddListener(OnVolumeChanged);
        if (musicVolumeSlider != null)
            musicVolumeSlider.onValueChanged.AddListener(OnVolumeChanged);
        if (sfxVolumeSlider != null)
            sfxVolumeSlider.onValueChanged.AddListener(OnVolumeChanged);
        if (muteAllScrollbar != null)
            muteAllScrollbar.onValueChanged.AddListener(OnMuteChanged);

        if (battleSpeedButton != null)
            battleSpeedButton.onClick.AddListener(ToggleBattleSpeed);
        if (fullscreenScrollbar != null)
            fullscreenScrollbar.onValueChanged.AddListener(OnFullscreenChanged);

        PrepareTabButton(audioButton, ref audioDefaultColor);
        PrepareTabButton(controlsButton, ref controlsDefaultColor);
        if (battleSpeedText == null && battleSpeedButton != null)
            battleSpeedText = battleSpeedButton.GetComponentInChildren<TMP_Text>(true);
        if (battleSpeedText != null)
            battleSpeedText.raycastTarget = false;

        if (muteBackground == null && muteAllScrollbar != null)
            muteBackground = muteAllScrollbar.GetComponent<Image>();
        if (fullscreenBackground == null && fullscreenScrollbar != null)
            fullscreenBackground = fullscreenScrollbar.GetComponent<Image>();
        if (muteOffSprite == null && muteBackground != null)
            muteOffSprite = muteBackground.sprite;
        if (fullscreenOffSprite == null && fullscreenBackground != null)
            fullscreenOffSprite = fullscreenBackground.sprite;

        EnsureOutsideClick();
        HideConfirm();
        this.HidePanelImmediate(panel);
    }

    void Start()
    {
        LoadSettings();
    }

    void OnDestroy()
    {
        if (closeButton != null)
            closeButton.onClick.RemoveListener(Close);
        if (saveButton != null)
            saveButton.onClick.RemoveListener(SaveAndClose);
        if (quitButton != null)
            quitButton.onClick.RemoveAllListeners();
        if (logoutButton != null)
            logoutButton.onClick.RemoveAllListeners();
        if (surrenderButton != null)
            surrenderButton.onClick.RemoveAllListeners();
        if (confirmYesButton != null)
            confirmYesButton.onClick.RemoveListener(ConfirmYes);
        if (confirmNoButton != null)
            confirmNoButton.onClick.RemoveListener(HideConfirm);
    }

    public void Open()
    {
        SetOutsideClick(true);
        this.OpenPanel(panel);
        LoadSettings();
        ShowAudio();
    }

    public void Close()
    {
        if (settingsChanged)
            SaveSettings();
        SetOutsideClick(false);
        HideConfirm();
        this.ClosePanel(panel);
    }

    void ShowAudio()
    {
        audioTabSelected = true;
        if (audioPanel != null)
            audioPanel.SetActive(true);
        if (controlsPanel != null)
            controlsPanel.SetActive(false);
        ApplyTabColors();
    }

    void ShowControls()
    {
        audioTabSelected = false;
        if (audioPanel != null)
            audioPanel.SetActive(controlsPanel == null);
        if (controlsPanel != null)
            controlsPanel.SetActive(true);
        ApplyTabColors();
    }

    void OnVolumeChanged(float _)
    {
        if (applyingUi)
            return;
        settingsChanged = true;
        ApplyAudio();
    }

    void OnMuteChanged(float _)
    {
        if (applyingUi)
            return;
        SnapSwitch(muteAllScrollbar);
        ApplySwitchSprite(muteBackground, IsOn(muteAllScrollbar), muteOffSprite, muteOnSprite);
        settingsChanged = true;
        ApplyAudio();
    }

    void ToggleBattleSpeed()
    {
        UnitAnimationSpeed.SetFast(!UnitAnimationSpeed.IsFast);
        RefreshBattleSpeedLabel();
        settingsChanged = true;
    }

    void OnFullscreenChanged(float _)
    {
        if (applyingUi)
            return;
        SnapSwitch(fullscreenScrollbar);
        ApplySwitchSprite(fullscreenBackground, IsOn(fullscreenScrollbar), fullscreenOffSprite, fullscreenOnSprite);
        settingsChanged = true;
        ApplyFullscreen();
    }

    void SaveAndClose()
    {
        SaveSettings();
        Close();
    }

    public void SaveSettings()
    {
        if (masterVolumeSlider != null)
            PlayerPrefs.SetFloat("MasterVolume", masterVolumeSlider.value);
        if (musicVolumeSlider != null)
            PlayerPrefs.SetFloat("MusicVolume", musicVolumeSlider.value);
        if (sfxVolumeSlider != null)
            PlayerPrefs.SetFloat("SfxVolume", sfxVolumeSlider.value);
        PlayerPrefs.SetInt("MuteAll", IsOn(muteAllScrollbar) ? 1 : 0);
        PlayerPrefs.SetInt("Fullscreen", IsOn(fullscreenScrollbar) ? 1 : 0);
        PlayerPrefs.SetInt("BattleSpeedFast", UnitAnimationSpeed.IsFast ? 1 : 0);
        PlayerPrefs.Save();
        settingsChanged = false;
    }

    public void LoadSettings()
    {
        applyingUi = true;

        if (masterVolumeSlider != null)
            masterVolumeSlider.value = PlayerPrefs.GetFloat("MasterVolume", 1f);
        if (musicVolumeSlider != null)
            musicVolumeSlider.value = PlayerPrefs.GetFloat("MusicVolume", 1f);
        if (sfxVolumeSlider != null)
            sfxVolumeSlider.value = PlayerPrefs.GetFloat("SfxVolume", 1f);

        SetSwitch(muteAllScrollbar, PlayerPrefs.GetInt("MuteAll", 0) == 1);
        SetSwitch(fullscreenScrollbar, PlayerPrefs.GetInt("Fullscreen", Screen.fullScreen ? 1 : 0) == 1);
        UnitAnimationSpeed.SetFast(PlayerPrefs.GetInt("BattleSpeedFast", 0) == 1);
        RefreshBattleSpeedLabel();

        applyingUi = false;
        ApplyAudio();
        ApplyFullscreen();
        ApplySwitchSprite(muteBackground, IsOn(muteAllScrollbar), muteOffSprite, muteOnSprite);
        ApplySwitchSprite(fullscreenBackground, IsOn(fullscreenScrollbar), fullscreenOffSprite, fullscreenOnSprite);
        settingsChanged = false;
    }

    void ApplyAudio()
    {
        if (AudioManager.Instance == null)
            return;

        float master = masterVolumeSlider != null ? masterVolumeSlider.value : 1f;
        float music = musicVolumeSlider != null ? musicVolumeSlider.value : 1f;
        float sfx = sfxVolumeSlider != null ? sfxVolumeSlider.value : 1f;

        AudioManager.Instance.SetVolume("MasterVolume", IsOn(muteAllScrollbar) ? 0.0001f : master);
        AudioManager.Instance.SetVolume("MusicVolume", music);
        AudioManager.Instance.SetVolume("SfxVolume", sfx);
    }

    void ApplyFullscreen()
    {
        bool fullscreen = IsOn(fullscreenScrollbar);
        Screen.SetResolution(
            ResolutionWidth,
            ResolutionHeight,
            fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed);
    }

    void ShowConfirm(ConfirmAction action)
    {
        pendingConfirm = action;
        if (confirmText != null)
        {
            confirmText.text = action switch
            {
                ConfirmAction.Logout => "Are you sure to log out?",
                ConfirmAction.Quit => "Are you sure to quit the game?",
                ConfirmAction.Surrender => "Are you sure to surrender?",
                _ => string.Empty
            };
        }
        if (confirmPanel != null)
            confirmPanel.SetActive(true);
    }

    void HideConfirm()
    {
        pendingConfirm = ConfirmAction.None;
        if (confirmPanel != null)
            confirmPanel.SetActive(false);
    }

    void ConfirmYes()
    {
        ConfirmAction action = pendingConfirm;
        HideConfirm();
        switch (action)
        {
            case ConfirmAction.Logout:
                PlayerSession.Clear();
                UnityEngine.SceneManagement.SceneManager.LoadScene("1_Login");
                break;
            case ConfirmAction.Quit:
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#else
                Application.Quit();
#endif
                break;
            case ConfirmAction.Surrender:
                Surrender();
                break;
        }
    }

    void Surrender()
    {
        if (isForfeiting)
            return;
        isForfeiting = true;

        if (surrenderButton != null)
            surrenderButton.interactable = false;

        var clientController = FindAnyObjectByType<ClientController>();
        if (clientController != null)
        {
            clientController.RequestForfeit();
        }
        else
        {
            Debug.LogWarning("[SettingRemake] ClientController not found for surrender.");
            isForfeiting = false;
            if (surrenderButton != null)
                surrenderButton.interactable = true;
        }

        Close();
    }

    static void ApplySwitchSprite(Image background, bool on, Sprite offSprite, Sprite onSprite)
    {
        if (background == null)
            return;
        Sprite sprite = on ? onSprite : offSprite;
        if (sprite != null)
            background.sprite = sprite;
    }

    static bool IsOn(Scrollbar bar)
    {
        return bar != null && bar.value >= 0.5f;
    }

    static void SetSwitch(Scrollbar bar, bool on)
    {
        if (bar == null)
            return;
        bar.SetValueWithoutNotify(on ? 1f : 0f);
    }

    static void SnapSwitch(Scrollbar bar)
    {
        if (bar == null)
            return;
        bar.SetValueWithoutNotify(bar.value >= 0.5f ? 1f : 0f);
    }

    void RefreshBattleSpeedLabel()
    {
        if (battleSpeedText == null)
            return;
        battleSpeedText.text = UnitAnimationSpeed.IsFast ? "2x" : "1x";
    }

    static void PrepareTabButton(Button button, ref Color defaultColor)
    {
        if (button == null)
            return;
        button.transition = Selectable.Transition.None;
        Image image = button.targetGraphic as Image;
        if (image != null)
            defaultColor = image.color;
    }

    void ApplyTabColors()
    {
        SetTabColor(audioButton, audioTabSelected ? ActiveTabColor : audioDefaultColor);
        SetTabColor(controlsButton, audioTabSelected ? controlsDefaultColor : ActiveTabColor);
    }

    static void SetTabColor(Button button, Color color)
    {
        if (button == null)
            return;
        Image image = button.targetGraphic as Image;
        if (image != null)
            image.color = color;
    }

    void EnsureOutsideClick()
    {
        if (outsideClick != null || panel == null || panel.transform.parent == null)
            return;

        outsideClick = new GameObject("SettingsOutsideClick", typeof(RectTransform));
        outsideClick.transform.SetParent(panel.transform.parent, false);

        RectTransform rect = outsideClick.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Image image = outsideClick.AddComponent<Image>();
        image.color = new Color(0f, 0f, 0f, 0f);

        Button button = outsideClick.AddComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.onClick.AddListener(Close);

        CanvasGroup group = outsideClick.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = false;
        group.interactable = false;

        outsideClick.transform.SetSiblingIndex(panel.transform.GetSiblingIndex());
    }

    void SetOutsideClick(bool open)
    {
        if (outsideClick == null || panel == null)
            return;

        outsideClick.transform.SetSiblingIndex(panel.transform.GetSiblingIndex());
        CanvasGroup group = outsideClick.GetComponent<CanvasGroup>();
        if (group == null)
            return;
        group.blocksRaycasts = open;
        group.interactable = open;
    }
}
