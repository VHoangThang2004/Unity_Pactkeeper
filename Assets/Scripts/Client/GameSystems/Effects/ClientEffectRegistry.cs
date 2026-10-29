using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "ClientEffectRegistry", menuName = "SRPG/Client Effect Registry")]
public class ClientEffectRegistry : ScriptableObject
{
    [SerializeField] private ClientActiveEffectBase[] effects;
    private Dictionary<int, ClientActiveEffectBase> _lookup;

    public void Init()
    {
        _lookup = new Dictionary<int, ClientActiveEffectBase>();
        foreach (var effect in effects)
        {
            if (effect == null) continue;
            if (_lookup.ContainsKey(effect.effectId))
            {
                Debug.LogWarning($"[ClientEffectRegistry] Duplicate effectId {effect.effectId}!");
                continue;
            }
            _lookup[effect.effectId] = effect;
        }
        Debug.Log($"[ClientEffectRegistry] Loaded {_lookup.Count} effects.");
    }

    public ClientActiveEffectBase Get(int effectId)
    {
        if (_lookup == null)
        {
            Debug.LogError("[ClientEffectRegistry] Not initialized!");
            return null;
        }
        if (!_lookup.TryGetValue(effectId, out var effect))
        {
            Debug.LogError($"[ClientEffectRegistry] No effect for effectId {effectId}");
            return null;
        }
        return effect;
    }

    public bool Exists(int effectId) => _lookup != null && _lookup.ContainsKey(effectId);

    /// <summary>
    /// Describes the given effects plus everything reachable through relatedEffectIds, depth-first,
    /// each effect at most once (cycles and shared links can't repeat or loop). One line per effect:
    /// "Name: description" when the effect has a statusName, otherwise just the description.
    /// </summary>
    public string Describe(IEnumerable<int> effectIds, UnitData unit)
    {
        var lines = new List<string>();
        var visited = new HashSet<int>();

        void Visit(int id)
        {
            if (!visited.Add(id) || !Exists(id)) return;
            var effect = _lookup[id];

            string desc = effect.GetDescription(unit, this);
            if (!string.IsNullOrWhiteSpace(desc))
                lines.Add(string.IsNullOrEmpty(effect.statusName) ? desc : $"<b>{effect.statusName}</b>: {desc}");

            if (effect.relatedEffectIds != null)
                foreach (int related in effect.relatedEffectIds) Visit(related);
        }

        if (effectIds != null)
            foreach (int id in effectIds) Visit(id);
        return string.Join("\n", lines);
    }
}