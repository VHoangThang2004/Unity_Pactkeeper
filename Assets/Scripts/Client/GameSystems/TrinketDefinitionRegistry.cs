using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "TrinketDefinitionRegistry", menuName = "SRPG/Trinket Definition Registry")]
public class TrinketDefinitionRegistry : ScriptableObject
{
    [System.Serializable]
    public struct Entry
    {
        public int trinketId;
        public Sprite icon;
    }

    [SerializeField] private Entry[] entries;
    private Dictionary<int, Sprite> _lookup;

    public void Init()
    {
        _lookup = new Dictionary<int, Sprite>();
        foreach (var e in entries)
        {
            if (_lookup.ContainsKey(e.trinketId))
            {
                Debug.LogWarning($"[TrinketDefinitionRegistry] Duplicate trinketId {e.trinketId}!");
                continue;
            }
            _lookup[e.trinketId] = e.icon;
        }
        Debug.Log($"[TrinketDefinitionRegistry] Loaded {_lookup.Count} entries.");
    }

    public Sprite GetIcon(int trinketId)
    {
        if (_lookup == null) { Debug.LogError("[TrinketDefinitionRegistry] Not initialized!"); return null; }
        _lookup.TryGetValue(trinketId, out var sprite);
        return sprite;
    }
}