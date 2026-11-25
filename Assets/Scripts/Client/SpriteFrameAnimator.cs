using UnityEngine;
using UnityEngine.UI;

public class SpriteFrameAnimator : MonoBehaviour
{
    [SerializeField] private Image target;
    [SerializeField] private Sprite[] frames;
    [SerializeField] private float framesPerSecond = 12f;

    int index;
    float timer;

    void Awake()
    {
        if (target == null)
            target = GetComponent<Image>();
    }

    void OnEnable()
    {
        index = 0;
        timer = 0f;
        ApplyFrame();
    }

    void Update()
    {
        if (target == null || frames == null || frames.Length == 0)
            return;

        timer += Time.unscaledDeltaTime;
        float step = 1f / Mathf.Max(1f, framesPerSecond);
        while (timer >= step)
        {
            timer -= step;
            index = (index + 1) % frames.Length;
            ApplyFrame();
        }
    }

    void ApplyFrame()
    {
        if (target == null || frames == null || frames.Length == 0)
            return;
        Sprite frame = frames[index];
        if (frame != null)
            target.sprite = frame;
    }
}
