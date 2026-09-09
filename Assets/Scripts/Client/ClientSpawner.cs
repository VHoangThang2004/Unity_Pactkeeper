using UnityEngine;
using UnityEngine.Tilemaps;

public class ClientSpawner : MonoBehaviour
{
    [SerializeField] private ClientController controller;

    // Wired at runtime by ClientMapLoader
    private Tilemap tilemap;

    public void SetTilemap(Tilemap t)
    {
        tilemap = t;
    }

    public void SpawnUnit(UnitData unitData)
    {
        if (tilemap == null)
        {
            Debug.LogError("[ClientSpawner] Tilemap not set — map not loaded yet!");
            return;
        }

        var prefab = controller.unitPrefabRegistry.Get(unitData.UId);
        if (prefab == null)
        {
            Debug.LogError($"[ClientSpawner] No prefab for uId {unitData.UId}!");
            return;
        }

        var def = controller.unitLibrary.Get(unitData.UId);
        if (def == null)
        {
            Debug.LogError($"[ClientSpawner] No definition for uId {unitData.UId}!");
            return;
        }

        Vector3 worldPos = tilemap.GetCellCenterWorld(unitData.CurrentCell);
        var go = Instantiate(prefab, worldPos, Quaternion.identity);
        go.name = $"Unit_{unitData.Id}_{def.unitName}";

        var clientUnit = go.GetComponent<ClientUnit>();
        if (clientUnit == null)
        {
            Debug.LogError($"[ClientSpawner] Prefab for uId {unitData.UId} has no ClientUnit component!");
            Destroy(go);
            return;
        }

        clientUnit.Init(unitData);
        controller.RegisterUnit(unitData.Id, clientUnit);

        Debug.Log($"[ClientSpawner] Spawned unit {unitData.Id} ({def.unitName}) at {unitData.CurrentCell}");
    }
}