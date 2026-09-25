using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Add this component to any UI button/element that can be targeted
/// by a GuidedClick plot node. The element self-registers into
/// TutorialTargetRegistry on enable and unregisters on disable.
///
/// The targetId string must match the PlotNode.targetButtonId exactly.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class TutorialTarget : MonoBehaviour
{
    [SerializeField] private string targetId;

    public RectTransform RectTransform { get; private set; }
    public Button Button { get; private set; }

    void Awake()
    {
        RectTransform = GetComponent<RectTransform>();
        Button = GetComponent<Button>();
    }

    void OnEnable() => TutorialTargetRegistry.Register(targetId, this);
    void OnDisable() => TutorialTargetRegistry.Unregister(targetId);
}