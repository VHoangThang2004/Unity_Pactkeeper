using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class EyesCanvasUI : MonoBehaviour
{
    [Header("Refs")]
    public GameObject panelRoot;
    public CanvasGroup rootCanvasGroup;
    public Animator leftEyeAnimator;
    public Animator rightEyeAnimator;
    public Image leftEyeImage;
    public Image rightEyeImage;

    [Header("Sprites")]
    public Sprite openSprite;
    public Sprite closeSprite;

    [Header("Timings")]
    public float backgroundFadeDur = 0.25f;
    public float eyeAnimDur = 0.25f;
    public float fadeStartAlpha = 0f;
    public float fadeEndAlpha = 1f;


    public IEnumerator BackgroundFades(bool fadeIn)
    {
        if (rootCanvasGroup == null)
            yield break;

        float startAlpha = fadeIn ? fadeStartAlpha : fadeEndAlpha;
        float endAlpha = fadeIn ? fadeEndAlpha : 0f;

        rootCanvasGroup.alpha = startAlpha;

        float elapsed = 0f;
        while (elapsed < backgroundFadeDur)
        {
            elapsed += Time.deltaTime;
            float t = backgroundFadeDur > 0f ? Mathf.Clamp01(elapsed / backgroundFadeDur) : 1f;
            rootCanvasGroup.alpha = Mathf.Lerp(startAlpha, endAlpha, t);
            yield return null;
        }

        rootCanvasGroup.alpha = endAlpha;
    }

    public IEnumerator PlayEyeSequence(bool leftEye, bool rightEye, bool animateLeft, bool animateRight, InstantStatus myInstantStatus = null, InstantStatus enemyInstantStatus = null)
    {
        Debug.Log($"[EyesCanvasUI] PlayEyeSequence called with leftEye={leftEye}, rightEye={rightEye}, animateLeft={animateLeft}, animateRight={animateRight}");


        if (panelRoot != null)
            panelRoot.SetActive(true);

        if (rootCanvasGroup != null)
        {
            rootCanvasGroup.alpha = fadeStartAlpha;
            rootCanvasGroup.interactable = true;
            rootCanvasGroup.blocksRaycasts = true;
        }
        // Step 1: Set eye sprites based on whether they will animate
        if (animateLeft)
        {
            // Will animate: show starting state (opposite of target)
            if (leftEyeImage != null)
                leftEyeImage.sprite = !leftEye ? openSprite : closeSprite;

            myInstantStatus?.UpdateInstantStatus(!leftEye);
            Debug.Log($"[EyesCanvasUI] Set left eye to starting state: {!leftEye}");
        }
        else
        {
            // Won't animate: show target state
            leftEyeAnimator.Play("Idle", 0, 0f);
            if (leftEyeImage != null)
                leftEyeImage.sprite = leftEye ? openSprite : closeSprite;
            myInstantStatus?.UpdateInstantStatus(leftEye);
            Debug.Log($"[EyesCanvasUI] Set left eye to target state: {leftEye}");
        }

        if (animateRight)
        {
            // Will animate: show starting state (opposite of target)
            if (rightEyeImage != null)
                rightEyeImage.sprite = !rightEye ? openSprite : closeSprite;
            enemyInstantStatus?.UpdateInstantStatus(!rightEye);
            Debug.Log($"[EyesCanvasUI] Set right eye to starting state: {!rightEye}");
        }
        else
        {
            // Won't animate: show target state
            rightEyeAnimator.Play("Idle", 0, 0f);
            if (rightEyeImage != null)
                rightEyeImage.sprite = rightEye ? openSprite : closeSprite;
            enemyInstantStatus?.UpdateInstantStatus(rightEye);
            Debug.Log($"[EyesCanvasUI] Set right eye to target state: {rightEye}");
        }

        // Step 2: Fade in
        yield return StartCoroutine(BackgroundFades(true));

        // Step 3: Play animations only for eyes that need to animate
        if (animateLeft && leftEyeAnimator != null)
        {
            string stateName = leftEye ? "EyeOpen" : "EyeClose";
            leftEyeAnimator.Play(stateName, 0, 0f);
        }

        if (animateRight && rightEyeAnimator != null)
        {
            string stateName = rightEye ? "EyeOpen" : "EyeClose";
            rightEyeAnimator.Play(stateName, 0, 0f);
        }

        myInstantStatus?.UpdateInstantStatus(leftEye);
        enemyInstantStatus?.UpdateInstantStatus(rightEye);
        leftEyeImage.sprite = leftEye ? openSprite : closeSprite;
        rightEyeImage.sprite = rightEye ? openSprite : closeSprite;
        Debug.Log($"[EyesCanvasUI] Updated eye sprites to final state: leftEye={leftEye}, rightEye={rightEye}");

        yield return new WaitForSeconds(eyeAnimDur + 0.5f);

        // Step 4: Fade out
        yield return StartCoroutine(BackgroundFades(false));
        yield return new WaitForSeconds(1f); //buffer



        if (rootCanvasGroup != null)
        {
            rootCanvasGroup.interactable = false;
            rootCanvasGroup.blocksRaycasts = false;
        }

        if (panelRoot != null)
            panelRoot.SetActive(false);

    }

}
