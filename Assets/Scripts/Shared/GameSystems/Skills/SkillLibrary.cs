// Shared/GameSystems/Skills/SkillLibrary.cs
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "SkillLibrary", menuName = "SRPG/Skill Library")]
public class SkillLibrary : ScriptableObject
{
    [SerializeField] private SkillDefinition[] definitions;

    private Dictionary<int, SkillDefinition> _lookup;

    public void Init()
    {
        _lookup = new Dictionary<int, SkillDefinition>();
        foreach (var def in definitions)
        {
            if (def == null) continue;
            if (_lookup.ContainsKey(def.skillId))
            {
                Debug.LogWarning($"[SkillLibrary] Duplicate skillId {def.skillId} for {def.skillName}!");
                continue;
            }
            _lookup[def.skillId] = def;
        }
        Debug.Log($"[SkillLibrary] Loaded {_lookup.Count} skill definitions.");
    }

    public SkillDefinition Get(int skillId)
    {
        if (_lookup == null)
        {
            Debug.LogError("[SkillLibrary] Not initialized! Call Init() first.");
            return null;
        }
        if (!_lookup.TryGetValue(skillId, out var def))
        {
            Debug.LogError($"[SkillLibrary] No definition found for skillId {skillId}");
            return null;
        }
        return def;
    }

    public bool Exists(int skillId) => _lookup != null && _lookup.ContainsKey(skillId);
}