using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Registry of all StoryEncounterConfig assets. Looked up by (chapterId, sceneId).
/// Same usage pattern as MapRegistry — assign all encounter assets in the Inspector.
/// </summary>
[CreateAssetMenu(fileName = "StoryEncounterRegistry", menuName = "SRPG/Story Encounter Registry")]
public class StoryEncounterRegistry : ScriptableObject
{
    [SerializeField] private StoryEncounterConfig[] encounters;

    private Dictionary<(int, int), StoryEncounterConfig> _lookup;

    public void Init()
    {
        _lookup = new Dictionary<(int, int), StoryEncounterConfig>();
        foreach (var e in encounters)
        {
            if (e == null) continue;
            var key = (e.chapterId, e.sceneId);
            if (_lookup.ContainsKey(key))
            {
                Debug.LogWarning($"[StoryEncounterRegistry] Duplicate entry for " +
                                 $"chapter={e.chapterId} scene={e.sceneId}!");
                continue;
            }
            _lookup[key] = e;
        }
        Debug.Log($"[StoryEncounterRegistry] Loaded {_lookup.Count} encounter configs.");
    }

    public StoryEncounterConfig Get(int chapterId, int sceneId)
    {
        if (_lookup == null)
        {
            Debug.LogError("[StoryEncounterRegistry] Not initialized! Call Init() first.");
            return null;
        }

        if (!_lookup.TryGetValue((chapterId, sceneId), out var config))
        {
            Debug.LogError($"[StoryEncounterRegistry] No encounter found for " +
                           $"chapter={chapterId} scene={sceneId}");
            return null;
        }

        return config;
    }
}