using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Match mode selection popup — hiện khi player bấm BATTLE.
/// Hover nhấc thẻ. Bấm thẻ lần đầu chỉ chọn và hiện nút tương ứng.
/// Bấm lại thẻ đó, hoặc bấm nút Random / Ranked / Invite, mới vào trận.
/// </summary>
public class MatchmodeMenuManager : MonoBehaviour
{
    enum Mode { None, Random, Ranked, Invite }

    [Header("Panels")]
    [SerializeField] private GameObject rootPanel;
    [SerializeField] private GameObject innerPanel;

    [Header("Mode Buttons")]
    [SerializeField] private Button randomButton;
    [SerializeField] private Button rankedButton;
    [SerializeField] private Button challengeButton;
    [SerializeField] private Button inviteButton;
    [SerializeField] private Button closeButton;

    [Header("Mode Cards")]
    [SerializeField] private RectTransform randomCard;
    [SerializeField] private RectTransform rankedCard;
    [SerializeField] private RectTransform inviteCard;
    [SerializeField] private Image randomFrameImage;
    [SerializeField] private Image rankedFrameImage;
    [SerializeField] private Image inviteFrameImage;
    [SerializeField] private Sprite randomSelectedFrame;
    [SerializeField] private Sprite rankedSelectedFrame;
    [SerializeField] private Sprite inviteSelectedFrame;
    [SerializeField] private float hoverLift = 28f;

    [Header("References (để trống nếu cùng GameObject)")]
    [SerializeField] private MatchmakingManager matchmakingManager;
    [SerializeField] private FriendTabManager   friendTabManager;

    Mode selected;
    GameObject outsideClick;
    Sprite randomNormalFrame;
    Sprite rankedNormalFrame;
    Sprite inviteNormalFrame;

    void Awake()
    {
        if (matchmakingManager == null)
            matchmakingManager = GetComponent<MatchmakingManager>();
        if (friendTabManager == null)
            friendTabManager = FindAnyObjectByType<FriendTabManager>();

        if (randomButton    != null) randomButton.onClick.AddListener(OnRandomClicked);
        if (rankedButton    != null) rankedButton.onClick.AddListener(OnRankedClicked);
        if (challengeButton != null) challengeButton.onClick.AddListener(OnChallengeClicked);
        if (inviteButton    != null) inviteButton.onClick.AddListener(OnInviteClicked);
        if (closeButton     != null) closeButton.onClick.AddListener(Close);

        randomNormalFrame = randomFrameImage != null ? randomFrameImage.sprite : null;
        rankedNormalFrame = rankedFrameImage != null ? rankedFrameImage.sprite : null;
        inviteNormalFrame = inviteFrameImage != null ? inviteFrameImage.sprite : null;

        BindCard(randomCard, Mode.Random);
        BindCard(rankedCard, Mode.Ranked);
        BindCard(inviteCard, Mode.Invite);
        EnsureOutsideClick();
        ClearSelection();
        HidePanel();
    }

    void OnDestroy()
    {
        if (randomButton    != null) randomButton.onClick.RemoveListener(OnRandomClicked);
        if (rankedButton    != null) rankedButton.onClick.RemoveListener(OnRankedClicked);
        if (challengeButton != null) challengeButton.onClick.RemoveListener(OnChallengeClicked);
        if (inviteButton    != null) inviteButton.onClick.RemoveListener(OnInviteClicked);
        if (closeButton     != null) closeButton.onClick.RemoveListener(Close);
    }

    public void Open()
    {
        ClearSelection();
        this.OpenPanel(rootPanel);
        SetOutsideClick(true);
    }

    public void Close()
    {
        SetOutsideClick(false);
        ClearSelection();
        this.ClosePanel(rootPanel);
    }

    void HidePanel()
    {
        SetOutsideClick(false);
        this.HidePanelImmediate(rootPanel);
    }

    void BindCard(RectTransform card, Mode mode)
    {
        if (card == null)
            return;

        foreach (Graphic graphic in card.GetComponentsInChildren<Graphic>(true))
        {
            if (graphic.gameObject != card.gameObject)
                graphic.raycastTarget = false;
        }

        float restY = card.anchoredPosition.y;
        EventTrigger trigger = card.GetComponent<EventTrigger>();
        if (trigger == null)
            trigger = card.gameObject.AddComponent<EventTrigger>();
        trigger.triggers.Clear();

        AddTrigger(trigger, EventTriggerType.PointerEnter, () =>
        {
            Vector2 position = card.anchoredPosition;
            position.y = restY + hoverLift;
            card.anchoredPosition = position;
        });
        AddTrigger(trigger, EventTriggerType.PointerExit, () =>
        {
            Vector2 position = card.anchoredPosition;
            position.y = restY;
            card.anchoredPosition = position;
        });
        AddTrigger(trigger, EventTriggerType.PointerClick, () => OnCardClicked(mode));
    }

    static void AddTrigger(EventTrigger trigger, EventTriggerType type, Action action)
    {
        var entry = new EventTrigger.Entry { eventID = type };
        entry.callback.AddListener(_ => action());
        trigger.triggers.Add(entry);
    }

    void OnCardClicked(Mode mode)
    {
        if (selected == mode)
        {
            StartSelected();
            return;
        }

        selected = mode;
        ApplyFrames();
        ShowSelectedButton();
    }

    void StartSelected()
    {
        switch (selected)
        {
            case Mode.Random: OnRandomClicked(); break;
            case Mode.Ranked: OnRankedClicked(); break;
            case Mode.Invite: OnInviteClicked(); break;
        }
    }

    void ClearSelection()
    {
        selected = Mode.None;
        ApplyFrames();
        if (randomButton != null) randomButton.gameObject.SetActive(false);
        if (rankedButton != null) rankedButton.gameObject.SetActive(false);
        if (inviteButton != null) inviteButton.gameObject.SetActive(false);
    }

    void ShowSelectedButton()
    {
        if (randomButton != null) randomButton.gameObject.SetActive(selected == Mode.Random);
        if (rankedButton != null) rankedButton.gameObject.SetActive(selected == Mode.Ranked);
        if (inviteButton != null) inviteButton.gameObject.SetActive(selected == Mode.Invite);
    }

    void ApplyFrames()
    {
        SetFrame(randomFrameImage, randomNormalFrame, randomSelectedFrame, selected == Mode.Random);
        SetFrame(rankedFrameImage, rankedNormalFrame, rankedSelectedFrame, selected == Mode.Ranked);
        SetFrame(inviteFrameImage, inviteNormalFrame, inviteSelectedFrame, selected == Mode.Invite);
    }

    static void SetFrame(Image image, Sprite normal, Sprite selectedSprite, bool selected)
    {
        if (image == null)
            return;
        if (selected && selectedSprite != null)
            image.sprite = selectedSprite;
        else if (normal != null)
            image.sprite = normal;
    }

    void EnsureOutsideClick()
    {
        if (outsideClick != null || rootPanel == null)
            return;

        Transform parent = rootPanel.transform.parent != null ? rootPanel.transform.parent : rootPanel.transform;
        outsideClick = new GameObject("MatchmodeOutsideClick", typeof(RectTransform));
        outsideClick.transform.SetParent(parent, false);

        RectTransform rect = outsideClick.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Image image = outsideClick.AddComponent<Image>();
        Texture2D tex = Texture2D.whiteTexture;
        image.sprite = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
        image.color = new Color(0f, 0f, 0f, 0f);

        Button button = outsideClick.AddComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.onClick.AddListener(Close);

        CanvasGroup group = outsideClick.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = false;
        group.interactable = false;
        outsideClick.transform.SetSiblingIndex(rootPanel.transform.GetSiblingIndex());
    }

    void SetOutsideClick(bool open)
    {
        if (outsideClick == null || rootPanel == null)
            return;

        outsideClick.transform.SetSiblingIndex(rootPanel.transform.GetSiblingIndex());
        CanvasGroup group = outsideClick.GetComponent<CanvasGroup>();
        if (group == null)
            return;
        group.blocksRaycasts = open;
        group.interactable = open;
    }

    void OnRandomClicked()
    {
        Close();
        if (matchmakingManager != null)
            matchmakingManager.OnClickFindMatch();
    }

    void OnRankedClicked()
    {
        Close();
        if (matchmakingManager != null)
            matchmakingManager.OnClickFindRankedMatch();
    }

    void OnChallengeClicked()
    {
        Close();
        if (friendTabManager != null)
            friendTabManager.Open();
    }

    void OnInviteClicked()
    {
        Close();
        if (friendTabManager != null)
            friendTabManager.Open();
    }
}
