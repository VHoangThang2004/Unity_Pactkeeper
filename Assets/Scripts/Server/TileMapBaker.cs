using UnityEngine;
using UnityEngine.Tilemaps;
using System.IO;

public class TilemapBaker : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Tilemap tilemap;

    [Header("Output")]
    [SerializeField] private string fileName = "map.json";

    [ContextMenu("Bake Tilemap To JSON")]
    public void BakeToJson()
    {
        if (tilemap == null)
        {
            Debug.LogError("❌ Tilemap is not assigned!");
            return;
        }

        // 🔥 VERY IMPORTANT
        tilemap.CompressBounds();

        BoundsInt bounds = tilemap.cellBounds;

        int width = bounds.size.x;
        int height = bounds.size.y;

        bool[] walkable = new bool[width * height];

        int emptyCount = 0;

        foreach (var pos in bounds.allPositionsWithin)
        {
            int x = pos.x - bounds.xMin;
            int y = pos.y - bounds.yMin;

            int index = x + y * width;

            bool hasTile = tilemap.HasTile(pos);

            walkable[index] = hasTile;

            if (!hasTile)
            {
                emptyCount++;
                Debug.Log($"EMPTY CELL at {pos}");
            }
        }

        GridMapData data = new GridMapData
        {
            width = width,
            height = height,
            originX = bounds.xMin,
            originY = bounds.yMin,
            walkable = walkable
        };

        string json = JsonUtility.ToJson(data, true);

        // Save to project folder (safe for testing)
        string path = Path.Combine(Application.dataPath, fileName);
        File.WriteAllText(path, json);

        Debug.Log($"✅ Map baked successfully!");
        Debug.Log($"📦 Size: {width} x {height}");
        Debug.Log($"📍 Origin: ({bounds.xMin}, {bounds.yMin})");
        Debug.Log($"❌ Empty tiles: {emptyCount}/{width * height}");
        Debug.Log($"💾 Saved to: {path}");
    }
}