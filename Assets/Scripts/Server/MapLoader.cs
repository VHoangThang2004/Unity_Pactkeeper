using System.IO;
using UnityEngine;

public static class MapLoader
{
    public static GridMap Load(string fileName)
    {
        // Example path:
        string path = Path.Combine(Application.dataPath, "Maps", fileName);

        if (!File.Exists(path))
        {
            Debug.LogError($"❌ Map file not found: {path}");
            return null;
        }

        string json = File.ReadAllText(path);

        GridMapData data = JsonUtility.FromJson<GridMapData>(json);

        GridMap map = new GridMap();
        map.Init(data);

        Debug.Log($"✅ Loaded map: {fileName} ({map.Width}x{map.Height})");

        return map;
    }
}