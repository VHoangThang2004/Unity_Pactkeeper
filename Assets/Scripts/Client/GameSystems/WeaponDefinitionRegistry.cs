using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "WeaponDefinitionRegistry", menuName = "SRPG/Weapon Definition Registry")]
public class WeaponDefinitionRegistry : ScriptableObject
{
    [System.Serializable]
    public struct Entry
    {
        public int weaponId;
        public Sprite icon;
        public string name;
        public int classId;
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
            if (_lookup.ContainsKey(e.weaponId))
            {
                Debug.LogWarning($"[WeaponDefinitionRegistry] Duplicate weaponId {e.weaponId}!");
                continue;
            }
            _lookup[e.weaponId] = e;
        }
        Debug.Log($"[WeaponDefinitionRegistry] Loaded {_lookup.Count} entries.");
    }

    public Sprite GetIcon(int weaponId)
    {
        if (_lookup == null) { Debug.LogError("[WeaponDefinitionRegistry] Not initialized!"); return null; }
        _lookup.TryGetValue(weaponId, out var entry);
        return entry.icon;
    }

    public WeaponDefinitionDto GetDefinition(int weaponId)
    {
        if (_lookup == null) { Debug.LogError("[WeaponDefinitionRegistry] Not initialized!"); return null; }
        _lookup.TryGetValue(weaponId, out var entry);
        if (entry.weaponId == 0) return null;

        return new WeaponDefinitionDto
        {
            weaponId = entry.weaponId,
            name = entry.name,
            classId = entry.classId,
            skillId = entry.skillId,
            statModifiers = entry.statModifiers
        };
    }
}