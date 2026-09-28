using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(RectTransform))]
public class TutorialTarget : MonoBehaviour
{
    [SerializeField] private string targetId;

    public RectTransform RectTransform { get; private set; }
    public Button Button { get; private set; }
    public ExtendedButton ExtendedButton { get; private set; }

    void Awake()
    {
        RectTransform = GetComponent<RectTransform>();
        Button = GetComponent<Button>();
        ExtendedButton = GetComponent<ExtendedButton>();
    }

    void OnEnable() => TutorialTargetRegistry.Register(targetId, this);
    void OnDestroy() => TutorialTargetRegistry.Unregister(targetId, this);
}