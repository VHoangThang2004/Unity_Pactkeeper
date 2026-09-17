using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "UnitPrefabRegistry", menuName = "SRPG/Unit Prefab Registry")]
public class UnitPrefabRegistry : ScriptableObject
{
    [System.Serializable]
    public struct Entry
    {
        public int uId;
        public string unitName;
        public GameObject prefab;
    }

    [SerializeField] private Entry[] entries;

    private Dictionary<int, Entry> _lookup;

    public void Init()
    {
        _lookup = new Dictionary<int, Entry>();

        foreach (var e in entries)
        {
            if (e.prefab == null)
            {
                Debug.LogWarning($"[UnitPrefabRegistry] uId {e.uId} has null prefab!");
                continue;
            }

            if (_lookup.ContainsKey(e.uId))
            {
                Debug.LogWarning($"[UnitPrefabRegistry] Duplicate uId {e.uId}!");
                continue;
            }

            _lookup[e.uId] = e;
        }

        Debug.Log($"[UnitPrefabRegistry] Loaded {_lookup.Count} prefab entries.");
    }

    public GameObject Get(int uId)
    {
        if (_lookup == null)
        {
            Debug.LogError("[UnitPrefabRegistry] Not initialized! Call Init() first.");
            return null;
        }

        if (!_lookup.TryGetValue(uId, out var entry))
        {
            Debug.LogError($"[UnitPrefabRegistry] No prefab found for uId {uId}");
            return null;
        }

        return entry.prefab;
    }

    public string GetName(int uId)
    {
        if (_lookup != null && _lookup.TryGetValue(uId, out var entry))
            return string.IsNullOrEmpty(entry.unitName) ? $"Unit {uId}" : entry.unitName;
        return $"Unit {uId}";
    }
}
