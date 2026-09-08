using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "UnitLibrary", menuName = "SRPG/Unit Library")]
public class UnitLibrary : ScriptableObject
{
    [SerializeField] private UnitDefinition[] definitions;

    private Dictionary<int, UnitDefinition> _lookup;

    // Call once before match starts
    public void Init()
    {
        _lookup = new Dictionary<int, UnitDefinition>();

        foreach (var def in definitions)
        {
            if (_lookup.ContainsKey(def.uId))
            {
                Debug.LogWarning($"[UnitLibrary] Duplicate uId {def.uId} for {def.unitName}!");
                continue;
            }
            _lookup[def.uId] = def;
        }

        Debug.Log($"[UnitLibrary] Loaded {_lookup.Count} unit definitions.");
    }

    public UnitDefinition Get(int uId)
    {
        if (_lookup == null)
        {
            Debug.LogError("[UnitLibrary] Not initialized! Call Init() first.");
            return null;
        }

        if (!_lookup.TryGetValue(uId, out var def))
        {
            Debug.LogError($"[UnitLibrary] No definition found for uId {uId}");
            return null;
        }

        return def;
    }

    public bool Exists(int uId)
    {
        return _lookup != null && _lookup.ContainsKey(uId);
    }
}