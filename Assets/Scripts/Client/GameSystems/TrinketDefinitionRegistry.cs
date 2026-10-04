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
        public string name;
        public int skillId;
        public EquipmentStatModifiersDto statModifiers;
    }

    [SerializeField] private Entry[] entries;
    private Dictionary<int, Entry> _lookup;

    public void Init()
    {
        _lookup = new Dictionary<int, Entry>();
        foreach (var e in entries)
        {
            if (_lookup.ContainsKey(e.trinketId))
            {
                Debug.LogWarning($"[TrinketDefinitionRegistry] Duplicate trinketId {e.trinketId}!");
                continue;
            }
            _lookup[e.trinketId] = e;
        }
        Debug.Log($"[TrinketDefinitionRegistry] Loaded {_lookup.Count} entries.");
    }

    public Sprite GetIcon(int trinketId)
    {
        if (_lookup == null) { Debug.LogError("[TrinketDefinitionRegistry] Not initialized!"); return null; }
        _lookup.TryGetValue(trinketId, out var entry);
        return entry.icon;
    }

    public TrinketDefinitionDto GetDefinition(int trinketId)
    {
        if (_lookup == null) { Debug.LogError("[TrinketDefinitionRegistry] Not initialized!"); return null; }
        _lookup.TryGetValue(trinketId, out var entry);
        if (entry.trinketId == 0) return null;

        return new TrinketDefinitionDto
        {
            trinketId = entry.trinketId,
            name = entry.name,
            skillId = entry.skillId,
            statModifiers = entry.statModifiers
        };
    }
}