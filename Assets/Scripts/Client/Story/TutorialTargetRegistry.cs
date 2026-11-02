using System.Collections.Generic;
using UnityEngine;

public static class TutorialTargetRegistry
{
    private static readonly Dictionary<string, List<TutorialTarget>> _targets = new();

    public static void Register(string id, TutorialTarget target)
    {
        if (string.IsNullOrEmpty(id) || target == null) return;
        RegisterInternal(id, target);

        string canonical = StripScenePrefix(id);
        if (canonical != id && !string.IsNullOrEmpty(canonical))
            RegisterInternal(canonical, target);
    }

    private static void RegisterInternal(string id, TutorialTarget target)
    {
        if (!_targets.TryGetValue(id, out var list))
        {
            list = new List<TutorialTarget>();
            _targets[id] = list;
        }
        list.RemoveAll(t => t == null);
        if (!list.Contains(target))
            list.Add(target);
        Debug.Log($"[TutorialTargetRegistry] Registered: {id} (total: {list.Count})");
    }

    public static void Unregister(string id, TutorialTarget target)
    {
        if (string.IsNullOrEmpty(id)) return;
        UnregisterInternal(id, target);

        string canonical = StripScenePrefix(id);
        if (canonical != id && !string.IsNullOrEmpty(canonical))
            UnregisterInternal(canonical, target);
    }

    private static void UnregisterInternal(string id, TutorialTarget target)
    {
        if (_targets.TryGetValue(id, out var list))
        {
            list.RemoveAll(t => t == null || t == target);
            if (list.Count == 0) _targets.Remove(id);
            Debug.Log($"[TutorialTargetRegistry] Unregistered: {id}");
        }
    }

    // Returns first available (non-null, active) target for this id, falling back to canonical (unprefixed) id
    public static TutorialTarget Get(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;

        var exact = GetActiveFromList(id);
        if (exact != null) return exact;

        string canonical = StripScenePrefix(id);
        if (canonical != id && !string.IsNullOrEmpty(canonical))
            return GetActiveFromList(canonical);

        return null;
    }

    private static TutorialTarget GetActiveFromList(string key)
    {
        if (!_targets.TryGetValue(key, out var list))
            return null;

        list.RemoveAll(t => t == null);
        foreach (var t in list)
        {
            if (t != null && t.gameObject.activeInHierarchy)
                return t;
        }
        return null;
    }

    /// <summary>
    /// Strips leading scene prefix of form "C{digits}S{digits}_" (e.g. "C0S1_ConfirmButton" -> "ConfirmButton").
    /// </summary>
    public static string StripScenePrefix(string id)
    {
        if (string.IsNullOrEmpty(id) || id[0] != 'C') return id;
        int idx = 1;
        while (idx < id.Length && char.IsDigit(id[idx])) idx++;
        if (idx == 1 || idx >= id.Length || id[idx] != 'S') return id;
        int sIdx = idx + 1;
        while (sIdx < id.Length && char.IsDigit(id[sIdx])) sIdx++;
        if (sIdx == idx + 1 || sIdx >= id.Length || id[sIdx] != '_') return id;
        return id.Substring(sIdx + 1);
    }

    public static void Clear() => _targets.Clear();
}