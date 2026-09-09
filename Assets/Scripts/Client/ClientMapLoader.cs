using UnityEngine;

/// <summary>
/// Instantiates the map prefab from GridMapAsset and wires all references
/// into ClientController and its subsystems via the MapPrefab component.
/// Called by ClientController after the snapshot is received.
/// </summary>
public class ClientMapLoader : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private ClientController controller;
    [SerializeField] private ClientVisualController visualController;
    [SerializeField] private ClientInteractionSystem interactionSystem;
    [SerializeField] private ClientSpawner spawner;

    private GameObject spawnedMap;

    public bool LoadMap(GridMapAsset asset)
    {
        if (asset == null)
        {
            Debug.LogError("[ClientMapLoader] GridMapAsset is null!");
            return false;
        }

        if (asset.tilemapPrefab == null)
        {
            Debug.LogError($"[ClientMapLoader] GridMapAsset '{asset.name}' has no tilemapPrefab assigned!");
            return false;
        }

        // Destroy previous map if any (rematch / map change)
        if (spawnedMap != null)
            Destroy(spawnedMap);

        // Instantiate prefab
        spawnedMap = Instantiate(asset.tilemapPrefab, Vector3.zero, Quaternion.identity);
        spawnedMap.name = $"Map_{asset.name}";

        // Get MapPrefab component — all refs are pre-wired on the prefab
        var mapPrefab = spawnedMap.GetComponent<MapPrefab>();
        if (mapPrefab == null)
        {
            Debug.LogError("[ClientMapLoader] Instantiated prefab has no MapPrefab component!");
            Destroy(spawnedMap);
            return false;
        }

        if (mapPrefab.movableTilemap == null || mapPrefab.rangeTilemap == null)
        {
            Debug.LogError("[ClientMapLoader] MapPrefab is missing tilemap references!");
            Destroy(spawnedMap);
            return false;
        }

        // Wire all subsystems
        controller.SetMapRefs(mapPrefab.movableTilemap, mapPrefab.rangeTilemap, mapPrefab.highlighter);
        visualController.SetMapRefs(mapPrefab.movableTilemap, mapPrefab.rangeTilemap);
        interactionSystem.SetTilemap(mapPrefab.movableTilemap);
        spawner.SetTilemap(mapPrefab.movableTilemap);

        Debug.Log($"[ClientMapLoader] Map '{asset.name}' loaded and wired.");
        return true;
    }
}