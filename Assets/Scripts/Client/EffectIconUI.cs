using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One status icon in UnitEffectStrip. Click → effect details in the inspect panel.
/// </summary>
public class EffectIconUI : MonoBehaviour
{
    [SerializeField] private Image icon;
    [SerializeField] private TextMeshProUGUI remainingText; // bottom-right, empty for permanent effects
    [SerializeField] private TextMeshProUGUI stacksText;    // centre, empty when stacks <= 1
    [SerializeField] private Button button;

    private System.Action onClick;

    void Awake() => button.onClick.AddListener(() => onClick?.Invoke());

    public void Set(Sprite sprite, int remaining, int stacks, System.Action onClick)
    {
        icon.sprite = sprite;
        icon.enabled = sprite != null;        remainingText.text = remaining < 0 ? "" : remaining.ToString();
        if (stacksText != null) stacksText.text = stacks > 1 ? stacks.ToString() : "";
        this.onClick = onClick;
    }
}
