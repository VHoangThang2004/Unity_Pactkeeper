using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class GachaChestAnimator : MonoBehaviour
{
    [Header("Skip")]
    [SerializeField] private Toggle skipToggle;

    [Header("Panels")]
    [SerializeField] private GameObject animationPanel;
    [SerializeField] private RectTransform chestPivot;
    [SerializeField] private GameObject closedLid;
    [SerializeField] private Image closedLidImage;
    [SerializeField] private Image chestBaseImage;
    [SerializeField] private Image landingDust;
    [SerializeField] private Image whiteFlash;

    [Header("Open Frames")]
    [SerializeField] private Sprite[] blueFrames = new Sprite[4];
    [SerializeField] private Sprite[] purpleFrames = new Sprite[4];
    [SerializeField] private Sprite[] goldFrames;
    [SerializeField] private float openDuration = 0.72f;

    [Header("Timing")]
    [SerializeField] private float fallDuration = 0.7f;
    [SerializeField] private float shakeDuration = 0.85f;
    [SerializeField] private float openHold = 0.9f;
    [SerializeField] private float zoomDuration = 0.55f;
    [SerializeField] private float flashFadeDuration = 0.35f;
    [SerializeField] private AudioClip thudClip;

    Vector2 chestRest;
    Vector3 chestRestScale = Vector3.one;
    bool built;

    public bool Skip => skipToggle != null && skipToggle.isOn;

    public void Ensure(RectTransform canvas)
    {
        if (canvas == null)
            return;

        if (!built && animationPanel == null)
            Build(canvas);

        if (animationPanel != null)
            animationPanel.SetActive(false);
        if (whiteFlash != null)
        {
            Color color = whiteFlash.color;
            color.a = 0f;
            whiteFlash.color = color;
            whiteFlash.raycastTarget = false;
        }
        EnsureSkipToggle(canvas);
        BindSkipToggle();
        if (chestPivot != null)
        {
            chestRest = chestPivot.anchoredPosition;
            chestRestScale = chestPivot.localScale;
        }
        built = true;
    }

    public IEnumerator PlayAndReveal(int maxTier, System.Action reveal)
    {
        if (animationPanel != null)
            animationPanel.SetActive(true);

        ResetChest();
        SetFlash(0f, false);

        Vector2 start = chestRest + new Vector2(0f, 800f);
        if (chestPivot != null)
            chestPivot.anchoredPosition = start;

        float elapsed = 0f;
        while (elapsed < fallDuration)
        {
            elapsed += Time.deltaTime;
            float t = EaseInQuad(Mathf.Clamp01(elapsed / fallDuration));
            if (chestPivot != null)
                chestPivot.anchoredPosition = Vector2.LerpUnclamped(start, chestRest, t);
            yield return null;
        }
        if (chestPivot != null)
            chestPivot.anchoredPosition = chestRest;

        if (thudClip != null)
            PlayThud();
        yield return LandShake();

        yield return Rattle();
        yield return PlayOpenFrames(maxTier);
        if (openHold > 0f)
            yield return new WaitForSeconds(openHold);

        elapsed = 0f;
        Vector3 fromScale = chestRestScale;
        Vector3 toScale = chestRestScale * 6f;
        while (elapsed < zoomDuration)
        {
            elapsed += Time.deltaTime;
            float t = EaseInExpo(Mathf.Clamp01(elapsed / zoomDuration));
            if (chestPivot != null)
                chestPivot.localScale = Vector3.LerpUnclamped(fromScale, toScale, t);
            SetFlash(t, true);
            yield return null;
        }

        if (animationPanel != null)
            animationPanel.SetActive(false);
        if (chestPivot != null)
        {
            chestPivot.anchoredPosition = chestRest;
            chestPivot.localScale = chestRestScale;
        }
        SetFlash(1f, true);
        if (whiteFlash != null)
            whiteFlash.transform.SetAsLastSibling();
        reveal?.Invoke();

        elapsed = 0f;
        while (elapsed < flashFadeDuration)
        {
            elapsed += Time.deltaTime;
            SetFlash(1f - Mathf.Clamp01(elapsed / flashFadeDuration), false);
            yield return null;
        }
        SetFlash(0f, false);
    }

    AudioSource thudSource;

    void PlayThud()
    {
        if (thudSource == null)
        {
            thudSource = gameObject.AddComponent<AudioSource>();
            thudSource.playOnAwake = false;
            thudSource.spatialBlend = 0f;
        }
        thudSource.PlayOneShot(thudClip);
    }

    Sprite restingChestSprite;
    bool restingChestCached;

    void ResetChest()
    {
        if (chestPivot != null)
        {
            chestPivot.anchoredPosition = chestRest;
            chestPivot.localScale = chestRestScale;
            chestPivot.localRotation = Quaternion.identity;
        }
        HideNamedChild(chestPivot, "Chest_OpenLid");
        HideNamedChild(chestPivot, "Inner_Rarity_Light");
        if (chestBaseImage != null)
            chestBaseImage.gameObject.SetActive(false);
        if (closedLid != null)
            closedLid.SetActive(true);
        Image chestImage = ClosedImage();
        if (!restingChestCached && chestImage != null)
        {
            restingChestSprite = chestImage.sprite;
            restingChestCached = true;
        }
        if (chestImage != null && restingChestSprite != null)
        {
            chestImage.sprite = restingChestSprite;
            chestImage.color = Color.white;
        }
        if (landingDust != null)
        {
            landingDust.gameObject.SetActive(false);
            landingDust.rectTransform.localScale = Vector3.one;
        }
    }

    IEnumerator LandShake()
    {
        if (landingDust != null)
        {
            landingDust.gameObject.SetActive(true);
            Color color = landingDust.color;
            color.a = 1f;
            landingDust.color = color;
        }

        float duration = 0.28f;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            if (chestPivot != null)
                chestPivot.anchoredPosition = chestRest + Random.insideUnitCircle * (8f * (1f - t));
            if (landingDust != null)
            {
                float scale = Mathf.Lerp(0.4f, 1.7f, t);
                landingDust.rectTransform.localScale = new Vector3(scale, scale, 1f);
                Color color = landingDust.color;
                color.a = 1f - t;
                landingDust.color = color;
            }
            yield return null;
        }
        if (chestPivot != null)
            chestPivot.anchoredPosition = chestRest;
        if (landingDust != null)
            landingDust.gameObject.SetActive(false);
    }

    IEnumerator Rattle()
    {
        float elapsed = 0f;
        while (elapsed < shakeDuration)
        {
            elapsed += Time.deltaTime;
            float fade = 1f - Mathf.Clamp01(elapsed / shakeDuration);
            if (chestPivot != null)
            {
                float angle = Mathf.Sin(elapsed * 48f) * 12f * fade;
                chestPivot.localRotation = Quaternion.Euler(0f, 0f, angle);
            }
            yield return null;
        }
        if (chestPivot != null)
            chestPivot.localRotation = Quaternion.identity;
    }

    Image ClosedImage()
    {
        if (closedLidImage != null)
            return closedLidImage;
        return closedLid != null ? closedLid.GetComponent<Image>() : null;
    }

    static void HideNamedChild(Transform parent, string childName)
    {
        if (parent == null)
            return;
        Transform child = parent.Find(childName);
        if (child != null)
            child.gameObject.SetActive(false);
    }

    IEnumerator PlayOpenFrames(int tier)
    {
        Sprite[] frames = FramesForTier(tier);
        int frameCount = CountSprites(frames);
        Image frameImage = chestBaseImage != null ? chestBaseImage : ClosedImage();
        HideNamedChild(chestPivot, "Chest_OpenLid");
        HideNamedChild(chestPivot, "Inner_Rarity_Light");

        if (frameImage == null || frameCount == 0)
            yield break;

        if (closedLid != null && frameImage != closedLidImage && frameImage.gameObject != closedLid)
            closedLid.SetActive(false);
        frameImage.gameObject.SetActive(true);
        frameImage.preserveAspect = false;
        frameImage.color = Color.white;

        float slice = Mathf.Max(0.01f, openDuration) / frameCount;
        for (int i = 0; i < frames.Length; i++)
        {
            if (frames[i] == null)
                continue;
            frameImage.sprite = frames[i];
            yield return new WaitForSeconds(slice);
        }
    }

    Sprite[] FramesForTier(int tier)
    {
        Sprite[] chosen = tier >= 2 ? goldFrames : tier == 1 ? purpleFrames : blueFrames;
        if (CountSprites(chosen) == 0)
            chosen = goldFrames;
        return chosen;
    }

    static int CountSprites(Sprite[] frames)
    {
        if (frames == null)
            return 0;
        int count = 0;
        for (int i = 0; i < frames.Length; i++)
        {
            if (frames[i] != null)
                count++;
        }
        return count;
    }

    void SetFlash(float alpha, bool blockRaycasts)
    {
        if (whiteFlash == null)
            return;
        Color color = whiteFlash.color;
        color.a = alpha;
        whiteFlash.color = color;
        whiteFlash.raycastTarget = blockRaycasts && alpha > 0.01f;
    }

    static float EaseInQuad(float t) => t * t;

    static float EaseInExpo(float t)
    {
        if (t <= 0f)
            return 0f;
        return Mathf.Pow(2f, 10f * (t - 1f));
    }

    void EnsureSkipToggle(RectTransform canvas)
    {
        if (skipToggle != null)
            return;

        var root = new GameObject("SkipAnimationToggle", typeof(RectTransform));
        var rect = root.GetComponent<RectTransform>();
        rect.SetParent(canvas, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.anchoredPosition = new Vector2(0f, 36f);
        rect.sizeDelta = new Vector2(280f, 40f);

        var background = CreateImage(rect, "Background", new Vector2(-90f, 0f), new Vector2(28f, 28f), new Color(0.15f, 0.12f, 0.1f, 0.9f));
        var check = CreateImage(background.rectTransform, "Checkmark", Vector2.zero, new Vector2(18f, 18f), new Color(0.95f, 0.82f, 0.35f, 1f));

        skipToggle = root.AddComponent<Toggle>();
        skipToggle.targetGraphic = background;
        skipToggle.graphic = check;
        skipToggle.isOn = false;

        var labelObject = new GameObject("Label", typeof(RectTransform));
        var labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.SetParent(rect, false);
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(48f, 0f);
        labelRect.offsetMax = Vector2.zero;
        var label = labelObject.AddComponent<TextMeshProUGUI>();
        label.text = "Skip Animation";
        label.fontSize = 22f;
        label.alignment = TextAlignmentOptions.MidlineLeft;
        label.color = Color.white;
        label.raycastTarget = false;
        TMP_Text existing = FindAnyObjectByType<TMP_Text>();
        if (existing != null)
            label.font = existing.font;
    }

    const string SkipPreferenceKey = "GachaSkipAnimation";

    void BindSkipToggle()
    {
        if (skipToggle == null)
            return;

        skipToggle.onValueChanged.RemoveListener(SaveSkip);
        skipToggle.SetIsOnWithoutNotify(PlayerPrefs.GetInt(SkipPreferenceKey, 0) == 1);
        skipToggle.onValueChanged.AddListener(SaveSkip);
    }

    void SaveSkip(bool on)
    {
        PlayerPrefs.SetInt(SkipPreferenceKey, on ? 1 : 0);
        PlayerPrefs.Save();
    }

    void Build(RectTransform canvas)
    {
        animationPanel = CreateStretch("GachaAnimationPanel", canvas);
        animationPanel.SetActive(false);

        var dim = CreateImage(animationPanel.GetComponent<RectTransform>(), "Dim_Background", Vector2.zero, Vector2.zero, new Color(0f, 0f, 0f, 0.85f));
        Stretch(dim.rectTransform);

        var pivotObject = new GameObject("Chest_Pivot", typeof(RectTransform));
        chestPivot = pivotObject.GetComponent<RectTransform>();
        chestPivot.SetParent(animationPanel.transform, false);
        chestPivot.anchorMin = chestPivot.anchorMax = new Vector2(0.5f, 0.5f);
        chestPivot.pivot = new Vector2(0.5f, 0.5f);
        chestPivot.sizeDelta = new Vector2(420f, 420f);
        chestRest = chestPivot.anchoredPosition;
        chestRestScale = chestPivot.localScale;

        closedLidImage = CreateImage(chestPivot, "Chest_Closed", Vector2.zero, new Vector2(460f, 320f), Color.white);
        closedLid = closedLidImage.gameObject;

        landingDust = CreateImage(animationPanel.GetComponent<RectTransform>(), "Landing_Dust", new Vector2(0f, -210f), new Vector2(320f, 110f), new Color(0.55f, 0.48f, 0.38f, 0f));
        landingDust.gameObject.SetActive(false);

        whiteFlash = CreateImage(canvas, "White_Flash_Overlay", Vector2.zero, Vector2.zero, new Color(1f, 1f, 1f, 0f));
        Stretch(whiteFlash.rectTransform);
        whiteFlash.raycastTarget = false;
        whiteFlash.transform.SetAsLastSibling();
    }

    static GameObject CreateStretch(string name, RectTransform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        Stretch(rect);
        return go;
    }

    static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    static Image CreateImage(RectTransform parent, string name, Vector2 position, Vector2 size, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        var rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        var image = go.GetComponent<Image>();
        image.sprite = SolidSprite();
        image.color = color;
        image.raycastTarget = name == "Dim_Background" || name == "White_Flash_Overlay";
        return image;
    }

    static Sprite solidSprite;

    static Sprite SolidSprite()
    {
        if (solidSprite == null)
        {
            Texture2D tex = Texture2D.whiteTexture;
            solidSprite = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
        }
        return solidSprite;
    }
}
