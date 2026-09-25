using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Static registry of UI buttons/elements that can be targeted by GuidedClick nodes.
/// Elements in additively-loaded scenes register themselves on OnEnable,
/// unregister on OnDisable. The plot sequencer looks up targets by string ID
/// without needing a direct scene reference.
/// </summary>
public static class TutorialTargetRegistry
{
    private static readonly Dictionary<string, TutorialTarget> _targets = new();

    public static void Register(string id, TutorialTarget target)
    {
        if (string.IsNullOrEmpty(id)) return;
        _targets[id] = target;
        Debug.Log($"[TutorialTargetRegistry] Registered: {id}");
    }

    public static void Unregister(string id)
    {
        if (string.IsNullOrEmpty(id)) return;
        _targets.Remove(id);
    }

    public static TutorialTarget Get(string id)
    {
        _targets.TryGetValue(id, out var target);
        return target;
    }

    public static void Clear() => _targets.Clear();
}