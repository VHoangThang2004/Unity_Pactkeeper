using UnityEngine;

public class UIButtonSound : MonoBehaviour
{
    public AudioClip clickSfx;

    public void PlayClick()
    {
        if (AudioManager.Instance != null && clickSfx != null)
            AudioManager.Instance.PlaySFX(clickSfx);
    }
}
