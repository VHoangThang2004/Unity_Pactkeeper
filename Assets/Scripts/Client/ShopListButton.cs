using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class ShopListButton : MonoBehaviour
{
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text timeText;
    [SerializeField] private Image typeImage;
    [SerializeField] private Image backgroundImage;
    [SerializeField] private Sprite[] backgroundSprites = new Sprite[4];
    [SerializeField] private Sprite[] typeSprites = new Sprite[4];
    [SerializeField] private float selectedOffset = -48f;
    [SerializeField] private float marqueeSpeed = 40f;

    ShopTabManager.PlayerShopDto shop;
    bool selected;
    float appliedOffset;
    bool hasWritten;
    Vector2 written;

    readonly ScrollingText nameScroll = new ScrollingText();
    float timeTick;

    void Awake()
    {
        if (backgroundImage == null)
            backgroundImage = GetComponent<Image>();
        if (nameText == null)
            nameText = FindText("SHopName");
        if (timeText == null)
            timeText = FindText("TimeRemaining");
        if (typeImage == null)
        {
            var found = transform.Find("TypeImage");
            if (found != null)
                typeImage = found.GetComponent<Image>();
        }
    }

    public void Setup(ShopTabManager.PlayerShopDto data, int index, UnityAction onClick)
    {
        shop = data;
        ApplySprite(backgroundImage, backgroundSprites, index);
        ApplySprite(typeImage, typeSprites, index);
        nameScroll.Play(nameText, data != null ? data.name : string.Empty, marqueeSpeed);
        timeTick = 0f;
        RefreshTime();

        var button = GetComponent<Button>();
        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            if (onClick != null)
                button.onClick.AddListener(onClick);
        }
    }

    public void SetSelected(bool value)
    {
        selected = value;
    }

    void Update()
    {
        nameScroll.Tick();
        timeTick += Time.deltaTime;
        if (timeTick < 1f)
            return;
        timeTick = 0f;
        RefreshTime();
    }

    void LateUpdate()
    {
        nameScroll.LateTick();

        var rt = (RectTransform)transform;
        var position = rt.anchoredPosition;
        bool layoutChanged = !hasWritten || (position - written).sqrMagnitude > 0.25f;
        if (layoutChanged)
            appliedOffset = 0f;

        position.x -= appliedOffset;
        float next = selected ? selectedOffset : 0f;
        position.x += next;
        appliedOffset = next;
        rt.anchoredPosition = position;
        written = position;
        hasWritten = true;
    }

    void RefreshTime()
    {
        if (timeText == null)
            return;

        if (!TryGetRemaining(out var remaining))
        {
            timeText.text = shop != null && !string.IsNullOrEmpty(shop.resetPeriod) ? shop.resetPeriod : string.Empty;
            return;
        }

        if (remaining.TotalSeconds < 0)
            remaining = TimeSpan.Zero;

        if (remaining.TotalDays >= 1)
            timeText.text = $"{(int)remaining.TotalDays}:{remaining.Hours}:{remaining.Minutes}";
        else
            timeText.text = $"{remaining.Hours}:{remaining.Minutes}:{remaining.Seconds}";
    }

    bool TryGetRemaining(out TimeSpan remaining)
    {
        remaining = TimeSpan.Zero;
        if (shop == null)
            return false;

        if (!string.IsNullOrEmpty(shop.expiryDate) &&
            DateTimeOffset.TryParse(shop.expiryDate, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var expiry))
        {
            remaining = expiry - DateTimeOffset.UtcNow;
            return true;
        }

        var now = DateTimeOffset.UtcNow;
        if (shop.resetPeriod == "Daily")
        {
            var next = new DateTimeOffset(now.Year, now.Month, now.Day, 0, 0, 0, TimeSpan.Zero).AddDays(1);
            remaining = next - now;
            return true;
        }

        if (shop.resetPeriod == "Weekly")
        {
            int days = ((int)DayOfWeek.Monday - (int)now.DayOfWeek + 7) % 7;
            if (days == 0)
                days = 7;
            var next = new DateTimeOffset(now.Year, now.Month, now.Day, 0, 0, 0, TimeSpan.Zero).AddDays(days);
            remaining = next - now;
            return true;
        }

        return false;
    }

    static void ApplySprite(Image image, Sprite[] sprites, int index)
    {
        if (image == null || sprites == null || index < 0 || index >= sprites.Length || sprites[index] == null)
            return;
        image.sprite = sprites[index];
    }

    TMP_Text FindText(string childName)
    {
        var found = transform.Find(childName);
        return found != null ? found.GetComponent<TMP_Text>() : null;
    }
}
