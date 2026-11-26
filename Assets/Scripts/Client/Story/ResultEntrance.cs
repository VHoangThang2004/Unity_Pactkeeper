using System.Collections;
using UnityEngine;

public static class ResultEntrance
{
    public static IEnumerator Play(RectTransform panel, RectTransform banner, float slideDistance, float slideDuration)
    {
        Vector2 panelRest = panel != null ? panel.anchoredPosition : Vector2.zero;
        Vector2 imageRest = banner != null ? banner.anchoredPosition : Vector2.zero;
        float elapsed = 0f;

        while (elapsed < slideDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float remain = 1f - Mathf.SmoothStep(0f, 1f, elapsed / slideDuration);
            if (panel != null)
                panel.anchoredPosition = panelRest + new Vector2(0f, -slideDistance * remain);
            if (banner != null)
            {
                float panelShift = panel != null ? -slideDistance * remain : 0f;
                float imageShift = slideDistance * remain;
                banner.anchoredPosition = imageRest + new Vector2(0f, imageShift - panelShift);
            }
            yield return null;
        }

        if (panel != null)
            panel.anchoredPosition = panelRest;
        if (banner != null)
            banner.anchoredPosition = imageRest;
    }
}
