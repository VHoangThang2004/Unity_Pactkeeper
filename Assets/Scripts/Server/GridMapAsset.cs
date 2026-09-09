using UnityEngine;

/// <summary>
/// Single source of truth for a map. Walkability is baked by TilemapBakerWindow.
/// Spawn points are filled manually in the Inspector after baking.
/// Shared by both server and client — bundled in build like any other asset.
/// </summary>
[CreateAssetMenu(fileName = "Map_", menuName = "SRPG/Grid Map Asset")]
public class GridMapAsset : ScriptableObject
{
    [Header("Baked Data (do not edit manually)")]
    public int width;
    public int height;
    public int originX;
    public int originY;
    public bool[] walkable;

    [Header("Visuals")]
    [Tooltip("Prefab containing the Grid with all Tilemap layers. Client instantiates this at match start.")]
    public GameObject tilemapPrefab;

    [Header("Spawn Points (fill after baking)")]
    public TeamSpawnPoints[] teamSpawns;

    [System.Serializable]
    public class TeamSpawnPoints
    {
        public Vector2Int[] positions;
    }

    public Vector2Int GetSpawn(int team, int index = 0)
    {
        if (teamSpawns == null || team >= teamSpawns.Length)
        {
            Debug.LogError($"[GridMapAsset] No spawn data for team {team}!");
            return Vector2Int.zero;
        }

        var spawns = teamSpawns[team];
        if (spawns.positions == null || spawns.positions.Length == 0)
        {
            Debug.LogError($"[GridMapAsset] Team {team} has no spawn positions!");
            return Vector2Int.zero;
        }

        if (index >= spawns.positions.Length)
        {
            Debug.LogWarning($"[GridMapAsset] Team {team} spawn index {index} out of range, using 0.");
            index = 0;
        }

        return spawns.positions[index];
    }
}