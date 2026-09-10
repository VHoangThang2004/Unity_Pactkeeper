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
}