using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Chapter 0 Scene 0 ("0.0 - Signing the First Pact").
/// Phase 1: Mandatory Name Input ("YOUR NAME?" + "CONFIRM") for newly logged-in Google users.
/// Phase 2: Immediately transitions into the 4-slide Visual Novel intro (Varek & the Heavenly Eye)
///          before advancing to C0_S1.
/// </summary>
public class C0_S0 : MonoBehaviour
{
    [Header("Config")]
    [SerializeField] private BackendConfig config;
    [SerializeField] private SceneConfig sceneConfig;

    [Header("Phase 1 — Mandatory Name Input UI (Auto-resolved if left null)")]
    [SerializeField] private TMP_InputField nameInputField;
    [SerializeField] private Button confirmButton;
    [SerializeField] private TMP_Text feedbackText;

    [Header("Phase 2 — Optional Slide Sprites (Fallback: Resources/StorySlides/<slideId>)")]
    [SerializeField] private Sprite bg_c0_ambush;
    [SerializeField] private Sprite bg_c0_heavenly_eye;
    [SerializeField] private Sprite bg_c0_pactkeeper_awaken;
    [SerializeField] private Sprite bg_c0_reinhardt_briefing;

    private struct IntroSlide
    {
        public string SlideId;
        public string Speaker;
        public string Text;
        public Color FallbackTint;
        public Sprite OverrideSprite;
    }

    private IntroSlide[] _slides;
    private int _currentSlideIndex = 0;
    private bool _waitingForClick = false;
    private bool _isSubmitting = false;
    private bool _slidesStarted = false;
    private bool _isFinishing = false;

    private Image _backdropImage;
    private TMP_Text _placeholderBadgeText;
    private TMP_Text _speakerText;
    private TMP_Text _dialogueText;
    private TMP_Text _stepCounterText;

    void Start()
    {
        PlotSequencer.HasShownChapter0IntroSlides = false;

        // Preserve existing TutorialReplayUIController integration
        var replayController = FindAnyObjectByType<TutorialReplayUIController>();
        if (replayController == null)
        {
            var go = new GameObject("TutorialReplayUIController");
            replayController = go.AddComponent<TutorialReplayUIController>();
        }
        replayController.ConfigureDependencies(config, null);
        replayController.InitializeUI();

        // Phase 1: Resolve mandatory Name Input UI
        if (nameInputField == null)
            nameInputField = FindAnyObjectByType<TMP_InputField>();

        if (confirmButton == null && nameInputField != null && nameInputField.transform.parent != null)
            confirmButton = nameInputField.transform.parent.GetComponentInChildren<Button>();

        if (nameInputField != null)
        {
            nameInputField.onValueChanged.AddListener(OnNameInputValueChanged);
            UpdateConfirmButtonState(nameInputField.text);
        }

        _slides = new IntroSlide[]
        {
            new IntroSlide
            {
                SlideId = "bg_c0_ambush",
                Speaker = "THE AMBUSH",
                Text = "An endless war between two factions has bled the realm dry. Caught in a merciless ambush, over half of Tactician Varek's squad lies fallen—yet he refuses to retreat.",
                FallbackTint = new Color(0.14f, 0.07f, 0.08f, 1f),
                OverrideSprite = bg_c0_ambush
            },
            new IntroSlide
            {
                SlideId = "bg_c0_heavenly_eye",
                Speaker = "THE HEAVENLY EYE",
                Text = "At the brink of death, the sky splits open. The Heavenly Eye gazes down, offering a forbidden contract: power to halt time at the edge of fate, in exchange for an oath to end this war.",
                FallbackTint = new Color(0.08f, 0.11f, 0.20f, 1f),
                OverrideSprite = bg_c0_heavenly_eye
            },
            new IntroSlide
            {
                SlideId = "bg_c0_pactkeeper_awaken",
                Speaker = "VAREK — THE PACTKEEPER",
                Text = "\"I accept the Pact. If my blade cannot save them, my command will.\" Varek's consciousness rises above the battlefield, seeing every movement frozen in time.",
                FallbackTint = new Color(0.12f, 0.09f, 0.18f, 1f),
                OverrideSprite = bg_c0_pactkeeper_awaken
            },
            new IntroSlide
            {
                SlideId = "bg_c0_reinhardt_briefing",
                Speaker = "COMMANDER REINHARDT",
                Text = "\"Varek! You're still breathing! That golden mark in your eyes... So the legends of the Pactkeeper are true. Enemy scouts are closing in—test your Heavenly Eye and guide my blade!\"",
                FallbackTint = new Color(0.10f, 0.14f, 0.12f, 1f),
                OverrideSprite = bg_c0_reinhardt_briefing
            }
        };
    }

    private void OnNameInputValueChanged(string currentValue)
    {
        if (feedbackText != null)
            feedbackText.gameObject.SetActive(false);

        UpdateConfirmButtonState(currentValue);
    }

    private void UpdateConfirmButtonState(string rawText)
    {
        if (confirmButton != null && !_isSubmitting)
            confirmButton.interactable = !string.IsNullOrWhiteSpace(rawText);
    }

    /// <summary>
    /// Bound to the CONFIRM button in C0_S0.unity.
    /// Phase 1: Validates and saves the mandatory player name, then starts Phase 2 (4-slide intro).
    /// Phase 2: If called again during slides, skips to C0_S1.
    /// </summary>
    public void CompleteAndContinue()
    {
        if (_slidesStarted)
        {
            FinishSceneAndAdvance();
            return;
        }

        if (_isSubmitting) return;

        if (nameInputField == null)
            nameInputField = FindAnyObjectByType<TMP_InputField>();

        string enteredName = nameInputField != null ? nameInputField.text.Trim() : string.Empty;
        if (string.IsNullOrEmpty(enteredName))
        {
            ShowFeedback("Please enter your name before continuing.");
            if (nameInputField != null && nameInputField.placeholder is TMP_Text placeholderTmp)
            {
                placeholderTmp.text = "Name is required!";
                placeholderTmp.color = new Color(1f, 0.45f, 0.45f, 0.9f);
            }
            return;
        }

        StartCoroutine(SubmitNameAndPlaySlidesRoutine(enteredName));
    }

    private IEnumerator SubmitNameAndPlaySlidesRoutine(string enteredName)
    {
        _isSubmitting = true;
        if (confirmButton != null)
            confirmButton.interactable = false;

        // Persist the chosen username to the backend if changed
        if (config != null && !string.IsNullOrEmpty(PlayerSession.Token) && enteredName != PlayerSession.Username)
        {
            string payload = JsonUtility.ToJson(new ChangeUsernamePayload { newUsername = enteredName });
            byte[] bodyRaw = Encoding.UTF8.GetBytes(payload);

            using var req = new UnityWebRequest($"{config.backendUrl}/api/auth/username", "PATCH");
            req.uploadHandler = new UploadHandlerRaw(bodyRaw);
            req.downloadHandler = new DownloadHandlerBuffer();
            config.SetHeaders(req, PlayerSession.Token);
            req.SetRequestHeader("Content-Type", "application/json");

            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                string serverMsg = req.downloadHandler != null && !string.IsNullOrWhiteSpace(req.downloadHandler.text)
                    ? req.downloadHandler.text.Trim('"')
                    : "Could not save name. Please try another name.";
                Debug.LogWarning($"[C0_S0] Failed to update username: {serverMsg}");
                ShowFeedback(serverMsg);
                _isSubmitting = false;
                UpdateConfirmButtonState(enteredName);
                yield break;
            }
        }

        PlayerSession.Username = enteredName;

        // Transition immediately from Phase 1 (Name Input) to Phase 2 (4-Slide Visual Novel Intro)
        _slidesStarted = true;
        BuildVisualNovelStage();
        yield return StartCoroutine(PlayIntroSlidesRoutine());
    }

    private IEnumerator PlayIntroSlidesRoutine()
    {
        for (_currentSlideIndex = 0; _currentSlideIndex < _slides.Length; _currentSlideIndex++)
        {
            RenderSlide(_slides[_currentSlideIndex], _currentSlideIndex, _slides.Length);
            _waitingForClick = true;

            while (_waitingForClick && !_isFinishing)
                yield return null;

            if (_isFinishing)
                yield break;
        }

        FinishSceneAndAdvance();
    }

    private void RenderSlide(IntroSlide slide, int index, int total)
    {
        Sprite loaded = slide.OverrideSprite;
        if (loaded == null && !string.IsNullOrEmpty(slide.SlideId))
            loaded = Resources.Load<Sprite>($"StorySlides/{slide.SlideId}");

        if (_backdropImage != null)
        {
            _backdropImage.sprite = loaded;
            _backdropImage.color = loaded != null ? Color.white : slide.FallbackTint;
        }

        if (_placeholderBadgeText != null)
        {
            _placeholderBadgeText.gameObject.SetActive(loaded == null);
            if (loaded == null)
                _placeholderBadgeText.text = $"[ Story Illustration: {slide.SlideId} ]";
        }

        if (_speakerText != null)
            _speakerText.text = slide.Speaker;

        if (_dialogueText != null)
            _dialogueText.text = slide.Text;

        if (_stepCounterText != null)
            _stepCounterText.text = $"Click to continue ({index + 1}/{total}) >>";
    }

    private void OnSlideClickAdvance()
    {
        _waitingForClick = false;
    }

    private void FinishSceneAndAdvance()
    {
        if (_isFinishing) return;
        _isFinishing = true;
        _waitingForClick = false;

        // Mark that C0_S0 has already displayed the 4 intro slides so C0_S1 goes straight to gameplay
        PlotSequencer.HasShownChapter0IntroSlides = true;

        StartCoroutine(StoryClient.CompleteAndAdvance(
            config, this,
            next => StoryClient.RouteToStoryScene(next, config, this),
            () =>
            {
                Debug.LogWarning("[C0_S0] Story advance failed.");
                _isFinishing = false;
            },
            chapterId: 0,
            sceneId: 0
        ));
    }

    private void BuildVisualNovelStage()
    {
        Canvas canvas = FindAnyObjectByType<Canvas>();
        if (canvas == null) return;

        TMP_FontAsset sharedFont = nameInputField != null && nameInputField.textComponent != null
            ? nameInputField.textComponent.font
            : null;
        if (sharedFont == null)
        {
            var existingTmp = canvas.GetComponentInChildren<TMP_Text>(true);
            if (existingTmp != null)
                sharedFont = existingTmp.font;
        }

        // Hide the Name Input "Panel" once confirmed
        for (int i = 0; i < canvas.transform.childCount; i++)
        {
            Transform child = canvas.transform.GetChild(i);
            if (child.name == "Panel")
                child.gameObject.SetActive(false);
        }

        // Root full-screen stage
        var stageGo = new GameObject("C0_S0_VisualNovelStage", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        stageGo.transform.SetParent(canvas.transform, false);
        var stageRt = stageGo.GetComponent<RectTransform>();
        stageRt.anchorMin = Vector2.zero;
        stageRt.anchorMax = Vector2.one;
        stageRt.offsetMin = Vector2.zero;
        stageRt.offsetMax = Vector2.zero;

        _backdropImage = stageGo.GetComponent<Image>();
        _backdropImage.color = new Color(0.1f, 0.08f, 0.14f, 1f);
        _backdropImage.preserveAspect = false;

        // Subtle placeholder badge when artwork asset isn't imported yet
        var badgeGo = new GameObject("ArtPlaceholderBadge", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        badgeGo.transform.SetParent(stageGo.transform, false);
        var badgeRt = badgeGo.GetComponent<RectTransform>();
        badgeRt.anchorMin = new Vector2(0.5f, 0.62f);
        badgeRt.anchorMax = new Vector2(0.5f, 0.62f);
        badgeRt.sizeDelta = new Vector2(900f, 80f);
        _placeholderBadgeText = badgeGo.GetComponent<TextMeshProUGUI>();
        if (sharedFont != null) _placeholderBadgeText.font = sharedFont;
        _placeholderBadgeText.fontSize = 26f;
        _placeholderBadgeText.alignment = TextAlignmentOptions.Center;
        _placeholderBadgeText.color = new Color(0.85f, 0.78f, 0.60f, 0.45f);
        _placeholderBadgeText.raycastTarget = false;

        // Bottom Dialogue Panel
        var boxGo = new GameObject("DialogueBox", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        boxGo.transform.SetParent(stageGo.transform, false);
        var boxRt = boxGo.GetComponent<RectTransform>();
        boxRt.anchorMin = new Vector2(0.06f, 0.05f);
        boxRt.anchorMax = new Vector2(0.94f, 0.34f);
        boxRt.offsetMin = Vector2.zero;
        boxRt.offsetMax = Vector2.zero;

        var boxImg = boxGo.GetComponent<Image>();
        boxImg.color = new Color(0.04f, 0.04f, 0.07f, 0.90f);

        // Speaker Header
        var speakerGo = new GameObject("SpeakerText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        speakerGo.transform.SetParent(boxGo.transform, false);
        var speakerRt = speakerGo.GetComponent<RectTransform>();
        speakerRt.anchorMin = new Vector2(0.04f, 0.72f);
        speakerRt.anchorMax = new Vector2(0.75f, 0.94f);
        speakerRt.offsetMin = Vector2.zero;
        speakerRt.offsetMax = Vector2.zero;

        _speakerText = speakerGo.GetComponent<TextMeshProUGUI>();
        if (sharedFont != null) _speakerText.font = sharedFont;
        _speakerText.fontSize = 30f;
        _speakerText.fontStyle = FontStyles.Bold;
        _speakerText.alignment = TextAlignmentOptions.BottomLeft;
        _speakerText.color = new Color(0.96f, 0.80f, 0.42f, 1f);
        _speakerText.raycastTarget = false;

        // Body Dialogue Text
        var bodyGo = new GameObject("DialogueText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        bodyGo.transform.SetParent(boxGo.transform, false);
        var bodyRt = bodyGo.GetComponent<RectTransform>();
        bodyRt.anchorMin = new Vector2(0.04f, 0.20f);
        bodyRt.anchorMax = new Vector2(0.96f, 0.68f);
        bodyRt.offsetMin = Vector2.zero;
        bodyRt.offsetMax = Vector2.zero;

        _dialogueText = bodyGo.GetComponent<TextMeshProUGUI>();
        if (sharedFont != null) _dialogueText.font = sharedFont;
        _dialogueText.fontSize = 28f;
        _dialogueText.alignment = TextAlignmentOptions.TopLeft;
        _dialogueText.color = new Color(0.94f, 0.94f, 0.96f, 1f);
        _dialogueText.textWrappingMode = TextWrappingModes.Normal;
        _dialogueText.raycastTarget = false;

        // Step prompt (bottom-right)
        var stepGo = new GameObject("StepPromptText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        stepGo.transform.SetParent(boxGo.transform, false);
        var stepRt = stepGo.GetComponent<RectTransform>();
        stepRt.anchorMin = new Vector2(0.60f, 0.05f);
        stepRt.anchorMax = new Vector2(0.96f, 0.20f);
        stepRt.offsetMin = Vector2.zero;
        stepRt.offsetMax = Vector2.zero;

        _stepCounterText = stepGo.GetComponent<TextMeshProUGUI>();
        if (sharedFont != null) _stepCounterText.font = sharedFont;
        _stepCounterText.fontSize = 22f;
        _stepCounterText.alignment = TextAlignmentOptions.BottomRight;
        _stepCounterText.color = new Color(0.80f, 0.75f, 0.62f, 0.85f);
        _stepCounterText.raycastTarget = false;

        // Full-screen Click-to-Advance invisible button
        var clickGo = new GameObject("ClickToAdvanceButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        clickGo.transform.SetParent(stageGo.transform, false);
        var clickRt = clickGo.GetComponent<RectTransform>();
        clickRt.anchorMin = Vector2.zero;
        clickRt.anchorMax = Vector2.one;
        clickRt.offsetMin = Vector2.zero;
        clickRt.offsetMax = Vector2.zero;

        var clickImg = clickGo.GetComponent<Image>();
        clickImg.color = new Color(0f, 0f, 0f, 0f);
        var clickBtn = clickGo.GetComponent<Button>();
        clickBtn.onClick.AddListener(OnSlideClickAdvance);

        // Top-right Skip Intro button (available only in Phase 2 after name has been saved)
        var skipGo = new GameObject("SkipIntroButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        skipGo.transform.SetParent(stageGo.transform, false);
        var skipRt = skipGo.GetComponent<RectTransform>();
        skipRt.anchorMin = new Vector2(1f, 1f);
        skipRt.anchorMax = new Vector2(1f, 1f);
        skipRt.pivot = new Vector2(1f, 1f);
        skipRt.anchoredPosition = new Vector2(-36f, -32f);
        skipRt.sizeDelta = new Vector2(200f, 52f);

        var skipImg = skipGo.GetComponent<Image>();
        skipImg.color = new Color(0.12f, 0.12f, 0.16f, 0.88f);
        var skipBtn = skipGo.GetComponent<Button>();
        skipBtn.onClick.AddListener(FinishSceneAndAdvance);

        var skipTextGo = new GameObject("SkipLabel", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        skipTextGo.transform.SetParent(skipGo.transform, false);
        var skipTextRt = skipTextGo.GetComponent<RectTransform>();
        skipTextRt.anchorMin = Vector2.zero;
        skipTextRt.anchorMax = Vector2.one;
        skipTextRt.offsetMin = Vector2.zero;
        skipTextRt.offsetMax = Vector2.zero;

        var skipTmp = skipTextGo.GetComponent<TextMeshProUGUI>();
        if (sharedFont != null) skipTmp.font = sharedFont;
        skipTmp.text = "Skip Intro >>";
        skipTmp.fontSize = 22f;
        skipTmp.alignment = TextAlignmentOptions.Center;
        skipTmp.color = new Color(0.95f, 0.88f, 0.72f, 1f);
        skipTmp.raycastTarget = false;
    }

    private void ShowFeedback(string message)
    {
        if (feedbackText == null && nameInputField != null && nameInputField.transform.parent != null)
        {
            var go = new GameObject("ValidationFeedbackText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            go.transform.SetParent(nameInputField.transform.parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0f, -90f);
            rt.sizeDelta = new Vector2(900f, 50f);

            feedbackText = go.GetComponent<TextMeshProUGUI>();
            if (nameInputField.textComponent != null)
                feedbackText.font = nameInputField.textComponent.font;
            feedbackText.fontSize = 28f;
            feedbackText.alignment = TextAlignmentOptions.Center;
            feedbackText.color = new Color(1f, 0.4f, 0.4f, 1f);
            feedbackText.raycastTarget = false;
        }

        if (feedbackText != null)
        {
            feedbackText.text = message;
            feedbackText.gameObject.SetActive(true);
        }
    }

    [System.Serializable]
    private class ChangeUsernamePayload
    {
        public string newUsername;
    }
}
