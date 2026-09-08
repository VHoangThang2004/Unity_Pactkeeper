using UnityEngine;
using UnityEngine.Tilemaps;

public class ClientSpawner : MonoBehaviour
{
    [SerializeField] private ClientController controller;
    [SerializeField] private Tilemap tilemap;

    public void SpawnUnit(UnitData unitData)
    {
        // Get prefab from registry
        var prefab = controller.unitPrefabRegistry.Get(unitData.UId);
        if (prefab == null)
        {
            Debug.LogError($"[ClientSpawner] No prefab for uId {unitData.UId}!");
            return;
        }

        // Get definition from library
        var def = controller.unitLibrary.Get(unitData.UId);
        if (def == null)
        {
            Debug.LogError($"[ClientSpawner] No definition for uId {unitData.UId}!");
            return;
        }

        // Instantiate at cell position
        Vector3 worldPos = tilemap.GetCellCenterWorld(unitData.CurrentCell);
        var go = Instantiate(prefab, worldPos, Quaternion.identity);
        go.name = $"Unit_{unitData.Id}_{def.unitName}";

        // Setup ClientUnit
        var clientUnit = go.GetComponent<ClientUnit>();
        if (clientUnit == null)
        {
            Debug.LogError($"[ClientSpawner] Prefab for uId {unitData.UId} has no ClientUnit component!");
            Destroy(go);
            return;
        }

        clientUnit.Init(unitData);

        // Register in controller so everyone can find it
        controller.RegisterUnit(unitData.Id, clientUnit);

        Debug.Log($"[ClientSpawner] Spawned unit {unitData.Id} ({def.unitName}) at {unitData.CurrentCell}");
    }
}