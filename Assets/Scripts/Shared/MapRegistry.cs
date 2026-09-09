using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Shared by server and client — bundled in build like UnitLibrary.
/// Maps a string mapId to a GridMapAsset.
/// Server picks the mapId (hardcoded now, backend later).
/// Clients use the mapId from the snapshot to load the same asset.
/// Lives in Assets/Data/Maps/
/// </summary>
[CreateAssetMenu(fileName = "MapRegistry", menuName = "SRPG/Map Registry")]
public class MapRegistry : ScriptableObject
{
    [System.Serializable]
    public struct Entry
    {
        public string mapId;
        public GridMapAsset asset;
    }

    [SerializeField] private Entry[] entries;

    private Dictionary<string, GridMapAsset> _lookup;

    public void Init()
    {
        _lookup = new Dictionary<string, GridMapAsset>();

        foreach (var e in entries)
        {
            if (string.IsNullOrEmpty(e.mapId))
            {
                Debug.LogWarning("[MapRegistry] Entry with empty mapId skipped.");
                continue;
            }

            if (e.asset == null)
            {
                Debug.LogWarning($"[MapRegistry] Entry '{e.mapId}' has null asset, skipped.");
                continue;
            }

            if (_lookup.ContainsKey(e.mapId))
            {
                Debug.LogWarning($"[MapRegistry] Duplicate mapId '{e.mapId}' — skipped.");
                continue;
            }

            _lookup[e.mapId] = e.asset;
        }

        Debug.Log($"[MapRegistry] Loaded {_lookup.Count} map entries.");
    }

    public GridMapAsset Get(string mapId)
    {
        if (_lookup == null)
        {
            Debug.LogError("[MapRegistry] Not initialized! Call Init() first.");
            return null;
        }

        if (!_lookup.TryGetValue(mapId, out var asset))
        {
            Debug.LogError($"[MapRegistry] No asset found for mapId '{mapId}'.");
            return null;
        }

        return asset;
    }

    public bool Exists(string mapId)
    {
        return _lookup != null && _lookup.ContainsKey(mapId);
    }
}