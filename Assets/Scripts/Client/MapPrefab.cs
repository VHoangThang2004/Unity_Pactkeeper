using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Sits on the root of each map prefab.
/// Holds direct references to all tilemap layers and runtime objects.
/// ClientMapLoader reads these after instantiating the prefab.
/// Wire these in the Inspector on the prefab itself — never changes at runtime.
/// </summary>
public class MapPrefab : MonoBehaviour
{
    [Header("Tilemap Layers")]
    public Tilemap movableTilemap;   // walkable tiles — used by interaction, pathfinding, spawning
    public Tilemap rangeTilemap;     // runtime range highlight overlay (empty at start)

    [Header("Runtime Objects")]
    public GameObject highlighter;   // hover highlight sprite
}