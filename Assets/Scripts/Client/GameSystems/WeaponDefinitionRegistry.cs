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
    }

    [SerializeField] private Entry[] entries;
    private Dictionary<int, Sprite> _lookup;

    public void Init()
    {
        _lookup = new Dictionary<int, Sprite>();
        foreach (var e in entries)
        {
            if (_lookup.ContainsKey(e.weaponId))
            {
                Debug.LogWarning($"[WeaponDefinitionRegistry] Duplicate weaponId {e.weaponId}!");
                continue;
            }
            _lookup[e.weaponId] = e.icon;
        }
        Debug.Log($"[WeaponDefinitionRegistry] Loaded {_lookup.Count} entries.");
    }

    public Sprite GetIcon(int weaponId)
    {
        if (_lookup == null) { Debug.LogError("[WeaponDefinitionRegistry] Not initialized!"); return null; }
        _lookup.TryGetValue(weaponId, out var sprite);
        return sprite;
    }
}