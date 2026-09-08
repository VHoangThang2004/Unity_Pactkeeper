using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "UnitPrefabRegistry", menuName = "SRPG/Unit Prefab Registry")]
public class UnitPrefabRegistry : ScriptableObject
{
    [System.Serializable]
    public struct Entry
    {
        public int uId;
        public GameObject prefab;
    }

    [SerializeField] private Entry[] entries;

    private Dictionary<int, GameObject> _lookup;

    public void Init(UnitLibrary library)
    {
        _lookup = new Dictionary<int, GameObject>();

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

            // Warn if uId has no matching definition in library
            if (!library.Exists(e.uId))
                Debug.LogWarning($"[UnitPrefabRegistry] uId {e.uId} has no matching UnitDefinition in library!");

            _lookup[e.uId] = e.prefab;
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

        if (!_lookup.TryGetValue(uId, out var prefab))
        {
            Debug.LogError($"[UnitPrefabRegistry] No prefab found for uId {uId}");
            return null;
        }

        return prefab;
    }
}