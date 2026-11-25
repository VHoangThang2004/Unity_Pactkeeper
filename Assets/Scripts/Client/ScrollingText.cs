using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ScrollingText
{
    TMP_Text text;
    RectTransform view;
    bool ready;
    bool measured;
    bool scrolling;
    float x;
    float textWidth;
    float viewWidth;
    float speed = 40f;

    public void Play(TMP_Text target, string value, float marqueeSpeed)
    {
        text = target;
        speed = marqueeSpeed;
        measured = false;
        Prepare(value);
    }

    public void Tick()
    {
        if (!scrolling || text == null)
            return;

        x -= speed * Time.deltaTime;
        if (x < -textWidth)
            x = viewWidth;

        var position = text.rectTransform.anchoredPosition;
        position.x = x;
        text.rectTransform.anchoredPosition = position;
    }

    public void LateTick()
    {
        if (!measured && text != null)
            Prepare(text.text);
    }

    void Prepare(string value)
    {
        if (text == null)
            return;

        text.text = value ?? string.Empty;
        text.enableWordWrapping = false;
        text.overflowMode = TextOverflowModes.Overflow;

        if (!ready)
        {
            var source = text.rectTransform;
            var viewportObject = new GameObject("TextViewport", typeof(RectTransform));
            viewportObject.transform.SetParent(source.parent, false);
            viewportObject.transform.SetSiblingIndex(source.GetSiblingIndex());
            view = viewportObject.GetComponent<RectTransform>();
            view.anchorMin = source.anchorMin;
            view.anchorMax = source.anchorMax;
            view.pivot = source.pivot;
            view.anchoredPosition = source.anchoredPosition;
            view.sizeDelta = source.sizeDelta;
            view.localScale = Vector3.one;
            viewportObject.AddComponent<RectMask2D>();
            source.SetParent(view, false);
            ready = true;
        }

        viewWidth = view.rect.width;
        measured = viewWidth > 1f;
        if (!measured)
            return;

        text.ForceMeshUpdate();
        textWidth = text.preferredWidth;
        float height = view.rect.height > 1f ? view.rect.height : text.rectTransform.sizeDelta.y;
        var rect = text.rectTransform;

        if (textWidth <= viewWidth + 1f)
        {
            scrolling = false;
            text.alignment = TextAlignmentOptions.Center;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(viewWidth, height);
            rect.anchoredPosition = Vector2.zero;
            return;
        }

        scrolling = true;
        text.alignment = TextAlignmentOptions.MidlineLeft;
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 0.5f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.sizeDelta = new Vector2(textWidth + 24f, height);
        x = 0f;
        rect.anchoredPosition = new Vector2(x, 0f);
    }
}
