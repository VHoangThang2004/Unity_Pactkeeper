using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Chapter 0 Scene 0 ("0.0 - Signing the First Pact").
/// Plays the 4-slide dark-fantasy Visual Novel opening intro for Varek (Pactkeeper)
/// with custom illustration Sprite placeholders before transitioning directly into C0_S1.
/// </summary>
public class C0_S0 : MonoBehaviour
{
    [Header("Config")]
    [SerializeField] private BackendConfig config;
    [SerializeField] private SceneConfig sceneConfig;

    [Header("4-Slide Visual Novel Intro Illustrations (Optional)")]
    [Tooltip("Slide 1 (bg_c0_ambush): The shattered world & the ambush wiping out half of Varek's squad.")]
    [SerializeField] private Sprite bg_c0_ambush;

    [Tooltip("Slide 2 (bg_c0_heavenly_eye): The Heavenly Eye opens in the sky, freezing time and offering the Pact.")]
    [SerializeField] private Sprite bg_c0_heavenly_eye;

    [Tooltip("Slide 3 (bg_c0_pactkeeper_awaken): Varek accepts the oath to end the war, awakening as a Pactkeeper.")]
    [SerializeField] private Sprite bg_c0_pactkeeper_awaken;

    [Tooltip("Slide 4 (bg_c0_reinhardt_briefing): Commander Reinhardt rallies beside Varek to test the Eye's power.")]
    [SerializeField] private Sprite bg_c0_reinhardt_briefing;

    private struct IntroSlide
    {
        public string slideId;
        public Sprite sprite;
        public string speaker;
        public string dialogue;
    }

    private int _currentSlideIndex = 0;
    private bool _advanceRequested = false;
    private bool _isCompleting = false;

    private Image _backdropImage;
    private TMP_Text _placeholderBadgeText;
    private TMP_Text _speakerText;
    private TMP_Text _dialogueText;
    private TMP_Text _stepCounterText;

    void Start()
    {
        var replayUI = GetComponent<TutorialReplayUIController>() ?? FindAnyObjectByType<TutorialReplayUIController>();
        if (replayUI == null)
            replayUI = gameObject.AddComponent<TutorialReplayUIController>();
        replayUI.ConfigureDependencies(config, null);
        replayUI.InitializeUI();

        BuildVisualNovelStage();
        StartCoroutine(PlayIntroSlidesRoutine());
    }

    private void BuildVisualNovelStage()
    {
        var canvas = FindAnyObjectByType<Canvas>();
        if (canvas == null) return;

        // Hide legacy placeholder children ("YOUR NAME?" input panel)
        for (int i = 0; i < canvas.transform.childCount; i++)
        {
            var child = canvas.transform.GetChild(i);
            if (child != null && child.name == "Panel")
                child.gameObject.SetActive(false);
        }

        // Root full-screen container
        var root = new GameObject("C0_S0_VisualNovelStage", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        root.transform.SetParent(canvas.transform, false);
        var rootRt = root.GetComponent<RectTransform>();
        rootRt.anchorMin = Vector2.zero;
        rootRt.anchorMax = Vector2.one;
        rootRt.offsetMin = Vector2.zero;
        rootRt.offsetMax = Vector2.zero;

        _backdropImage = root.GetComponent<Image>();
        _backdropImage.color = new Color(0.06f, 0.05f, 0.09f, 1f);

        // Placeholder illustration badge (visible until custom Sprite is assigned)
        var badgeObj = new GameObject("IllustrationPlaceholderBadge", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        badgeObj.transform.SetParent(root.transform, false);
        var badgeRt = badgeObj.GetComponent<RectTransform>();
        badgeRt.anchorMin = new Vector2(0.5f, 0.62f);
        badgeRt.anchorMax = new Vector2(0.5f, 0.62f);
        badgeRt.sizeDelta = new Vector2(960f, 90f);
        badgeRt.anchoredPosition = Vector2.zero;

        _placeholderBadgeText = badgeObj.GetComponent<TextMeshProUGUI>();
        _placeholderBadgeText.fontSize = 28f;
        _placeholderBadgeText.alignment = TextAlignmentOptions.Center;
        _placeholderBadgeText.color = new Color(0.86f, 0.76f, 0.52f, 0.85f);
        _placeholderBadgeText.raycastTarget = false;

        // Dialogue box panel at the bottom
        var boxObj = new GameObject("DialogueBox", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        boxObj.transform.SetParent(root.transform, false);
        var boxRt = boxObj.GetComponent<RectTransform>();
        boxRt.anchorMin = new Vector2(0.08f, 0.05f);
        boxRt.anchorMax = new Vector2(0.92f, 0.31f);
        boxRt.offsetMin = Vector2.zero;
        boxRt.offsetMax = Vector2.zero;

        var boxImg = boxObj.GetComponent<Image>();
        boxImg.color = new Color(0.04f, 0.04f, 0.07f, 0.92f);
        boxImg.raycastTarget = false;

        // Speaker header
        var speakerObj = new GameObject("SpeakerText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        speakerObj.transform.SetParent(boxObj.transform, false);
        var speakerRt = speakerObj.GetComponent<RectTransform>();
        speakerRt.anchorMin = new Vector2(0.04f, 0.70f);
        speakerRt.anchorMax = new Vector2(0.75f, 0.94f);
        speakerRt.offsetMin = Vector2.zero;
        speakerRt.offsetMax = Vector2.zero;

        _speakerText = speakerObj.GetComponent<TextMeshProUGUI>();
        _speakerText.fontSize = 26f;
        _speakerText.fontStyle = FontStyles.Bold;
        _speakerText.alignment = TextAlignmentOptions.BottomLeft;
        _speakerText.color = new Color(0.95f, 0.80f, 0.45f, 1f);
        _speakerText.raycastTarget = false;

        // Slide counter + click prompt
        var counterObj = new GameObject("StepCounterText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        counterObj.transform.SetParent(boxObj.transform, false);
        var counterRt = counterObj.GetComponent<RectTransform>();
        counterRt.anchorMin = new Vector2(0.65f, 0.08f);
        counterRt.anchorMax = new Vector2(0.96f, 0.28f);
        counterRt.offsetMin = Vector2.zero;
        counterRt.offsetMax = Vector2.zero;

        _stepCounterText = counterObj.GetComponent<TextMeshProUGUI>();
        _stepCounterText.fontSize = 20f;
        _stepCounterText.alignment = TextAlignmentOptions.BottomRight;
        _stepCounterText.color = new Color(0.75f, 0.75f, 0.82f, 0.85f);
        _stepCounterText.raycastTarget = false;

        // Body dialogue text
        var bodyObj = new GameObject("DialogueText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        bodyObj.transform.SetParent(boxObj.transform, false);
        var bodyRt = bodyObj.GetComponent<RectTransform>();
        bodyRt.anchorMin = new Vector2(0.04f, 0.18f);
        bodyRt.anchorMax = new Vector2(0.96f, 0.68f);
        bodyRt.offsetMin = Vector2.zero;
        bodyRt.offsetMax = Vector2.zero;

        _dialogueText = bodyObj.GetComponent<TextMeshProUGUI>();
        _dialogueText.fontSize = 30f;
        _dialogueText.alignment = TextAlignmentOptions.TopLeft;
        _dialogueText.color = Color.white;
        _dialogueText.raycastTarget = false;

        // Full-screen click catcher to advance slides
        var clickObj = new GameObject("ClickToAdvanceButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        clickObj.transform.SetParent(root.transform, false);
        var clickRt = clickObj.GetComponent<RectTransform>();
        clickRt.anchorMin = Vector2.zero;
        clickRt.anchorMax = Vector2.one;
        clickRt.offsetMin = Vector2.zero;
        clickRt.offsetMax = Vector2.zero;

        var clickImg = clickObj.GetComponent<Image>();
        clickImg.color = new Color(0f, 0f, 0f, 0f);
        var clickBtn = clickObj.GetComponent<Button>();
        clickBtn.onClick.AddListener(() => _advanceRequested = true);

        // Skip Intro button (top-right corner)
        var skipObj = new GameObject("SkipIntroButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        skipObj.transform.SetParent(root.transform, false);
        var skipRt = skipObj.GetComponent<RectTransform>();
        skipRt.anchorMin = new Vector2(1f, 1f);
        skipRt.anchorMax = new Vector2(1f, 1f);
        skipRt.pivot = new Vector2(1f, 1f);
        skipRt.anchoredPosition = new Vector2(-36f, -28f);
        skipRt.sizeDelta = new Vector2(170f, 48f);

        var skipImg = skipObj.GetComponent<Image>();
        skipImg.color = new Color(0.14f, 0.14f, 0.20f, 0.90f);
        var skipBtn = skipObj.GetComponent<Button>();
        skipBtn.onClick.AddListener(CompleteAndContinue);

        var skipLblObj = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        skipLblObj.transform.SetParent(skipObj.transform, false);
        var skipLblRt = skipLblObj.GetComponent<RectTransform>();
        skipLblRt.anchorMin = Vector2.zero;
        skipLblRt.anchorMax = Vector2.one;
        skipLblRt.offsetMin = Vector2.zero;
        skipLblRt.offsetMax = Vector2.zero;

        var skipTmp = skipLblObj.GetComponent<TextMeshProUGUI>();
        skipTmp.text = "Skip Intro >>";
        skipTmp.fontSize = 20f;
        skipTmp.alignment = TextAlignmentOptions.Center;
        skipTmp.color = Color.white;
        skipTmp.raycastTarget = false;
    }

    private IEnumerator PlayIntroSlidesRoutine()
    {
        var slides = new IntroSlide[]
        {
            new IntroSlide
            {
                slideId = "bg_c0_ambush",
                sprite = ResolveSlideSprite(bg_c0_ambush, "bg_c0_ambush"),
                speaker = "SLIDE 1 — THE AMBUSH",
                dialogue = "Endless war has bled the realm dry, and a brutal ambush just slaughtered half your squad. Though unarmed, you—Tactician Varek—refuse to flee."
            },
            new IntroSlide
            {
                slideId = "bg_c0_heavenly_eye",
                sprite = ResolveSlideSprite(bg_c0_heavenly_eye, "bg_c0_heavenly_eye"),
                speaker = "SLIDE 2 — THE HEAVENLY EYE",
                dialogue = "At death's door, the Heavenly Eye opens above, freezing the falling blades. It offers a Pact: command time itself in exchange for ending this war."
            },
            new IntroSlide
            {
                slideId = "bg_c0_pactkeeper_awaken",
                sprite = ResolveSlideSprite(bg_c0_pactkeeper_awaken, "bg_c0_pactkeeper_awaken"),
                speaker = "SLIDE 3 — AWAKENING OF THE PACTKEEPER",
                dialogue = "You accept the oath and awaken as a Pactkeeper. From the sky above, your gaze now halts time at critical moments to issue absolute commands."
            },
            new IntroSlide
            {
                slideId = "bg_c0_reinhardt_briefing",
                sprite = ResolveSlideSprite(bg_c0_reinhardt_briefing, "bg_c0_reinhardt_briefing"),
                speaker = "SLIDE 4 — COMMANDER REINHARDT",
                dialogue = "Your surviving vanguard, Commander Reinhardt, rallies beside you against the remaining ambushers. Open the Heavenly Eye—it is time to test your newfound power!"
            }
        };

        for (_currentSlideIndex = 0; _currentSlideIndex < slides.Length; _currentSlideIndex++)
        {
            var s = slides[_currentSlideIndex];
            if (_backdropImage != null)
            {
                _backdropImage.sprite = s.sprite;
                _backdropImage.color = s.sprite != null ? Color.white : new Color(0.06f, 0.05f, 0.09f, 1f);
            }
            if (_placeholderBadgeText != null)
            {
                bool showPlaceholder = s.sprite == null;
                _placeholderBadgeText.gameObject.SetActive(showPlaceholder);
                if (showPlaceholder)
                    _placeholderBadgeText.text = $"[ Illustration Placeholder: {s.slideId} ]";
            }
            if (_speakerText != null)
                _speakerText.text = s.speaker;
            if (_dialogueText != null)
                _dialogueText.text = s.dialogue;
            if (_stepCounterText != null)
                _stepCounterText.text = $"Slide {_currentSlideIndex + 1} / {slides.Length}  —  Click anywhere to continue";

            _advanceRequested = false;
            yield return new WaitUntil(() => _advanceRequested || _isCompleting);
            if (_isCompleting) yield break;
        }

        CompleteAndContinue();
    }

    private static Sprite ResolveSlideSprite(Sprite serializedSprite, string slideId)
    {
        if (serializedSprite != null) return serializedSprite;
        return Resources.Load<Sprite>($"StorySlides/{slideId}") ?? Resources.Load<Sprite>(slideId);
    }

    public void CompleteAndContinue()
    {
        if (_isCompleting) return;
        _isCompleting = true;
        PlotSequencer.HasShownChapter0IntroSlides = true;

        StartCoroutine(StoryClient.CompleteAndAdvance(
            config, this,
            next => StoryClient.RouteToStoryScene(next, config, this),
            () => Debug.LogWarning("[C0_S0] Story advance failed."),
            chapterId: 0,
            sceneId: 0
        ));
    }
}
