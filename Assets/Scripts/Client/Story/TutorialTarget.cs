using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(RectTransform))]
public class TutorialTarget : MonoBehaviour
{
    [SerializeField] private string targetId;
    private readonly List<string> _aliases = new();

    private RectTransform _rectTransform;
    private Button _button;
    private ExtendedButton _extendedButton;

    public string TargetId => targetId;
    public RectTransform RectTransform => _rectTransform != null ? _rectTransform : (_rectTransform = GetComponent<RectTransform>());
    public Button Button => _button != null ? _button : (_button = GetComponent<Button>());
    public ExtendedButton ExtendedButton => _extendedButton != null ? _extendedButton : (_extendedButton = GetComponent<ExtendedButton>());

    void Awake()
    {
        _rectTransform = GetComponent<RectTransform>();
        _button = GetComponent<Button>();
        _extendedButton = GetComponent<ExtendedButton>();
    }

    void OnEnable() => RegisterAll();
    void OnDestroy() => UnregisterAll();

    /// <summary>
    /// Configures the primary targetId (if not already set) and any additional alias IDs,
    /// then immediately registers all IDs with TutorialTargetRegistry.
    /// </summary>
    public void Configure(string primaryId, params string[] aliases)
    {
        if (!string.IsNullOrEmpty(primaryId))
        {
            if (string.IsNullOrEmpty(targetId))
                targetId = primaryId;
            else if (targetId != primaryId && !_aliases.Contains(primaryId))
                _aliases.Add(primaryId);
        }

        if (aliases != null)
        {
            foreach (var alias in aliases)
            {
                if (!string.IsNullOrEmpty(alias) && alias != targetId && !_aliases.Contains(alias))
                    _aliases.Add(alias);
            }
        }

        RegisterAll();
    }

    private void RegisterAll()
    {
        if (!string.IsNullOrEmpty(targetId))
            TutorialTargetRegistry.Register(targetId, this);
        foreach (var alias in _aliases)
            TutorialTargetRegistry.Register(alias, this);
    }

    private void UnregisterAll()
    {
        if (!string.IsNullOrEmpty(targetId))
            TutorialTargetRegistry.Unregister(targetId, this);
        foreach (var alias in _aliases)
            TutorialTargetRegistry.Unregister(alias, this);
    }
}