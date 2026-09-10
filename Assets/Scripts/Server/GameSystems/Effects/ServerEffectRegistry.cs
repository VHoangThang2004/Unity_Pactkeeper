using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "ServerEffectRegistry", menuName = "SRPG/Server Effect Registry")]
public class ServerEffectRegistry : ScriptableObject
{
    [SerializeField] private ServerActiveEffectBase[] effects;
    private Dictionary<int, ServerActiveEffectBase> _lookup;

    public void Init()
    {
        _lookup = new Dictionary<int, ServerActiveEffectBase>();
        foreach (var effect in effects)
        {
            if (effect == null) continue;
            if (_lookup.ContainsKey(effect.effectId))
            {
                Debug.LogWarning($"[ServerEffectRegistry] Duplicate effectId {effect.effectId}!");
                continue;
            }
            _lookup[effect.effectId] = effect;
        }
        Debug.Log($"[ServerEffectRegistry] Loaded {_lookup.Count} effects.");
    }

    public ServerActiveEffectBase Get(int effectId)
    {
        if (_lookup == null)
        {
            Debug.LogError("[ServerEffectRegistry] Not initialized!");
            return null;
        }
        if (!_lookup.TryGetValue(effectId, out var effect))
        {
            Debug.LogError($"[ServerEffectRegistry] No effect for effectId {effectId}");
            return null;
        }
        return effect;
    }

    public bool Exists(int effectId) => _lookup != null && _lookup.ContainsKey(effectId);
}