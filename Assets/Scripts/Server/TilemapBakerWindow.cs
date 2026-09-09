#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEditor;

/// <summary>
/// Editor-only tool. Lives in Assets/Editor/ — never included in builds.
/// Opens via Tools → SRPG → Bake Tilemap.
/// Bakes walkability from a Tilemap into a GridMapAsset ScriptableObject.
/// Spawn points are filled manually in the Inspector after baking.
/// </summary>
public class TilemapBakerWindow : EditorWindow
{
    private Tilemap tilemap;
    private string assetPath = "Assets/Data/Maps/Map_.asset";

    [MenuItem("Tools/SRPG/Bake Tilemap")]
    public static void Open()
    {
        var window = GetWindow<TilemapBakerWindow>("Tilemap Baker");
        window.minSize = new Vector2(380, 200);
    }

    void OnGUI()
    {
        GUILayout.Label("Tilemap Baker", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        tilemap = (Tilemap)EditorGUILayout.ObjectField(
            "Tilemap", tilemap, typeof(Tilemap), allowSceneObjects: true);

        EditorGUILayout.Space();

        assetPath = EditorGUILayout.TextField("Output Asset Path", assetPath);

        EditorGUILayout.HelpBox(
            "Asset will be created or overwritten at the path above.\n" +
            "After baking, open the asset and fill in Team Spawn Points manually.",
            MessageType.Info);

        EditorGUILayout.Space();

        GUI.enabled = tilemap != null;

        if (GUILayout.Button("Bake", GUILayout.Height(36)))
            Bake();

        GUI.enabled = true;

        if (tilemap == null)
            EditorGUILayout.HelpBox("Assign a Tilemap to bake.", MessageType.Warning);
    }

    void Bake()
    {
        // Must compress bounds first — essential
        tilemap.CompressBounds();

        BoundsInt bounds = tilemap.cellBounds;
        int width  = bounds.size.x;
        int height = bounds.size.y;

        bool[] walkable = new bool[width * height];
        int emptyCount = 0;

        foreach (var pos in bounds.allPositionsWithin)
        {
            int x = pos.x - bounds.xMin;
            int y = pos.y - bounds.yMin;
            bool hasTile = tilemap.HasTile(pos);
            walkable[x + y * width] = hasTile;
            if (!hasTile) emptyCount++;
        }

        // Load existing asset or create a new one
        var asset = AssetDatabase.LoadAssetAtPath<GridMapAsset>(assetPath);
        if (asset == null)
        {
            asset = CreateInstance<GridMapAsset>();
            AssetDatabase.CreateAsset(asset, assetPath);
        }

        // Write baked data — preserve spawn points if asset already existed
        asset.width   = width;
        asset.height  = height;
        asset.originX = bounds.xMin;
        asset.originY = bounds.yMin;
        asset.walkable = walkable;

        EditorUtility.SetDirty(asset);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // Ping the asset so it's visible in Project window
        EditorGUIUtility.PingObject(asset);
        Selection.activeObject = asset;

        Debug.Log($"✅ Baked: {assetPath}");
        Debug.Log($"📦 Size: {width} x {height}");
        Debug.Log($"📍 Origin: ({bounds.xMin}, {bounds.yMin})");
        Debug.Log($"⬜ Empty tiles: {emptyCount}/{width * height}");
        Debug.Log($"👉 Now fill in Team Spawn Points in the Inspector.");
    }
}
#endif