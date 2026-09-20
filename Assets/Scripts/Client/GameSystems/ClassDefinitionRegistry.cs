using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "ClassDefinitionRegistry", menuName = "SRPG/Class Definition Registry")]
public class ClassDefinitionRegistry : ScriptableObject
{
    [System.Serializable]
    public struct Entry
    {
        public int classId;
        public Sprite fullFrameIcon;
        public Sprite haflFrameIcon;
    }

    [SerializeField] private Entry[] entries;
    private Dictionary<int, Sprite> _lookupFullFrames;
    private Dictionary<int, Sprite> _lookupHalfFrames;

    public void Init()
    {
        _lookupFullFrames = new Dictionary<int, Sprite>();
        _lookupHalfFrames = new Dictionary<int, Sprite>();

        foreach (var e in entries)
        {
            if (_lookupFullFrames.ContainsKey(e.classId) || _lookupHalfFrames.ContainsKey(e.classId))
            {
                Debug.LogWarning($"[TrinketDefinitionRegistry] Duplicate trinketId {e.classId}!");
                continue;
            }
            _lookupFullFrames[e.classId] = e.fullFrameIcon;
            _lookupHalfFrames[e.classId] = e.haflFrameIcon;
        }
    }

    public Sprite GetFullFrame(int classId)
    {
        if (_lookupFullFrames == null) { Debug.LogError("[TrinketDefinitionRegistry] Not initialized!"); return null; }
        _lookupFullFrames.TryGetValue(classId, out var sprite);
        return sprite;
    }
    public Sprite GetHalfFrame(int classId)
    {
        if (_lookupHalfFrames == null) { Debug.LogError("[TrinketDefinitionRegistry] Not initialized!"); return null; }
        _lookupHalfFrames.TryGetValue(classId, out var sprite);
        return sprite;
    }
}