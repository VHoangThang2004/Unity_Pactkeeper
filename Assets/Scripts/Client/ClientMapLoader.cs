using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Pure map loading worker. Called by ClientSyncMachine.
/// No token awareness — SyncMachine handles that.
/// Loads map prefab, wires ClientScene refs, signals completion.
/// </summary>
public class ClientMapLoader : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private ClientMatchSession session;
    [SerializeField] private ClientScene scene;

    private string loadedMapId = null;
    private GameObject spawnedMap = null;

    /// <summary>
    /// Called by ClientSyncMachine. Loads map if changed, wires refs, calls onComplete(success).
    /// </summary>
    public IEnumerator Load(Action<bool> onComplete)
    {
        string targetMapId = session.MapId;

        // No map change needed
        if (targetMapId == loadedMapId)
        {
            onComplete(true);
            yield break;
        }

        if (string.IsNullOrEmpty(targetMapId) || session.MapAsset == null)
        {
            Debug.LogError("[ClientMapLoader] No map asset to load!");
            onComplete(false);
            yield break;
        }

        // Destroy previous
        if (spawnedMap != null)
        {
            Destroy(spawnedMap);
            scene.ClearMapRefs();
            scene.ClearUnits();
        }

        // Instantiate
        spawnedMap      = Instantiate(session.MapAsset.tilemapPrefab, Vector3.zero, Quaternion.identity);
        spawnedMap.name = $"Map_{session.MapAsset.name}";

        var mapPrefab = spawnedMap.GetComponent<MapPrefab>();
        if (mapPrefab == null || mapPrefab.movableTilemap == null || mapPrefab.rangeTilemap == null)
        {
            Debug.LogError("[ClientMapLoader] MapPrefab missing or incomplete!");
            Destroy(spawnedMap);
            onComplete(false);
            yield break;
        }

        // Wire scene refs
        scene.SetMapRefs(mapPrefab.movableTilemap, mapPrefab.rangeTilemap, mapPrefab.highlighter);
        scene.clientInteractionSystem.Init();

        loadedMapId = targetMapId;
        Debug.Log($"[ClientMapLoader] Map '{loadedMapId}' loaded.");

        onComplete(true);
        yield break;
    }
}