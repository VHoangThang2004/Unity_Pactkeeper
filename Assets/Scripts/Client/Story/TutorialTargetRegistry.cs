using System.Collections.Generic;
using UnityEngine;

public static class TutorialTargetRegistry
{
    private static readonly Dictionary<string, List<TutorialTarget>> _targets = new();

    public static void Register(string id, TutorialTarget target)
    {
        if (string.IsNullOrEmpty(id)) return;
        if (!_targets.ContainsKey(id))
            _targets[id] = new List<TutorialTarget>();
        if (!_targets[id].Contains(target))
            _targets[id].Add(target);
        Debug.Log($"[TutorialTargetRegistry] Registered: {id} (total: {_targets[id].Count})");
    }

    public static void Unregister(string id, TutorialTarget target)
    {
        if (string.IsNullOrEmpty(id)) return;
        if (_targets.TryGetValue(id, out var list))
        {
            list.Remove(target);
            if (list.Count == 0) _targets.Remove(id);
            Debug.Log($"[TutorialTargetRegistry] Unregistered: {id}");
        }
    }

    // Returns first available (non-null, active) target for this id
    public static TutorialTarget Get(string id)
    {
        // Debug.Log($"[Registry] All keys: {string.Join(", ", _targets.Keys)}");
        if (!_targets.TryGetValue(id, out var list))
        {
            // Debug.LogWarning($"[Registry] Key '{id}' not found.");
            return null;
        }
        foreach (var t in list)
            if (t != null && t.gameObject.activeInHierarchy) return t;
        // Debug.LogWarning($"[Registry] Key '{id}' found but no active target. List count: {list.Count}");
        return null;
    }

    public static void Clear() => _targets.Clear();
}