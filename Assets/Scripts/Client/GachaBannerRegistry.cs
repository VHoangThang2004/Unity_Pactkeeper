using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "GachaBannerRegistry", menuName = "SRPG/Gacha Banner Registry")]
public class GachaBannerRegistry : ScriptableObject
{
    [System.Serializable]
    public struct Entry
    {
        public string bannerId;       // MongoDB banner ID slug (e.g., "60c72b2f9b1d8e2d88a45678")
        public string bannerName;     // Alternate match key (e.g., "Standard Summon")
        public Sprite listButtonArt;  // Sprite for the custom list item button
        public Sprite promoArt;       // Sprite for the large main promotional area
    }

    [SerializeField] private Entry[] entries;

    private Dictionary<string, Entry> _idLookup;
    private Dictionary<string, Entry> _nameLookup;

    public void Init()
    {
        _idLookup = new Dictionary<string, Entry>();
        _nameLookup = new Dictionary<string, Entry>();

        if (entries == null) return;

        foreach (var e in entries)
        {
            if (!string.IsNullOrEmpty(e.bannerId) && !_idLookup.ContainsKey(e.bannerId))
            {
                _idLookup[e.bannerId] = e;
            }
            if (!string.IsNullOrEmpty(e.bannerName) && !_nameLookup.ContainsKey(e.bannerName))
            {
                _nameLookup[e.bannerName] = e;
            }
        }
    }

    public Entry? GetArt(string id, string name)
    {
        if (_idLookup == null || _nameLookup == null)
        {
            Init();
        }

        if (!string.IsNullOrEmpty(id) && _idLookup.TryGetValue(id, out var entryById))
        {
            return entryById;
        }

        if (!string.IsNullOrEmpty(name) && _nameLookup.TryGetValue(name, out var entryByName))
        {
            return entryByName;
        }

        return null;
    }
}
