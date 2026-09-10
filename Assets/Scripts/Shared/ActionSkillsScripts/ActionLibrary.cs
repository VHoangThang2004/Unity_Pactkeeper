using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Shared by server and client. Maps actionId/skillCardId to ActionDefinition.
/// Server executes effects, client replays animations using the same definition.
/// Lives in Assets/Data/Actions/ — pattern mirrors UnitLibrary.
/// </summary>
[CreateAssetMenu(fileName = "ActionLibrary", menuName = "SRPG/Action Library")]
public class ActionLibrary : ScriptableObject
{
    [SerializeField] private ActionDefinition[] definitions;

    private Dictionary<int, ActionDefinition> _lookup;

    public void Init()
    {
        _lookup = new Dictionary<int, ActionDefinition>();
        foreach (var def in definitions)
        {
            if (def == null) continue;
            if (_lookup.ContainsKey(def.actionId))
            {
                Debug.LogWarning($"[ActionLibrary] Duplicate actionId {def.actionId} for {def.actionName}!");
                continue;
            }
            _lookup[def.actionId] = def;
        }
        Debug.Log($"[ActionLibrary] Loaded {_lookup.Count} action definitions.");
    }

    public ActionDefinition Get(int actionId)
    {
        if (_lookup == null)
        {
            Debug.LogError("[ActionLibrary] Not initialized! Call Init() first.");
            return null;
        }
        if (!_lookup.TryGetValue(actionId, out var def))
        {
            Debug.LogError($"[ActionLibrary] No definition found for actionId {actionId}");
            return null;
        }
        return def;
    }

    public bool Exists(int actionId) => _lookup != null && _lookup.ContainsKey(actionId);
}