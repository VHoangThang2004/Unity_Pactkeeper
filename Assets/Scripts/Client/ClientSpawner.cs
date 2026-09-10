using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Pure unit sync worker. Called by ClientSyncMachine.
/// No token awareness — SyncMachine handles that.
/// Diffs scene units vs session units: spawns missing, removes stale, repositions existing.
/// </summary>
public class ClientSpawner : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private ClientMatchSession session;
    [SerializeField] private ClientScene scene;
    [SerializeField] private UnitLibrary unitLibrary;
    [SerializeField] private UnitPrefabRegistry unitPrefabRegistry;

    /// <summary>
    /// Called by ClientSyncMachine. Syncs scene units to session units, calls onComplete(success).
    /// </summary>
    public IEnumerator Sync(Action<bool> onComplete)
    {
        if (scene.movableTilemap == null)
        {
            Debug.LogError("[ClientSpawner] Tilemap not ready!");
            onComplete(false);
            yield break;
        }

        List<UnitData> sessionUnits = session.units;
        List<ClientUnit> sceneUnits = scene.GetAllSceneUnits();
        List<int> sessionUnitIds = session.GetAllUnitIds();

        //delete all clientUnit that is not supposed to be in scene
        foreach (ClientUnit cu in new List<ClientUnit>(sceneUnits))
        {
            if (!sessionUnitIds.Contains(cu.unitId))
            {
                scene.UnregisterUnit(cu);
                Destroy(cu.gameObject);
            }
        }

        foreach (var unitData in sessionUnits)
        {
            ClientUnit existing = scene.GetSceneUnitById(unitData.Id);
            if (existing != null)
            {
                // //if the spawned unit at wrong position
                // if (existing.unitId.CurrentCell != unitData.CurrentCell)
                //     existing.SetPosition(scene.movableTilemap, unitData.CurrentCell);
                // => above is truncated, because as unit data synced - unit will automatically sync to destination once
            }
            else
            {
                SpawnUnit(unitData);
            }
        }

        Debug.Log($"[ClientSpawner] Units synced.");
        onComplete(true);
        yield break;
    }

    void SpawnUnit(UnitData unitData)
    {
        var prefab = unitPrefabRegistry.Get(unitData.UId);
        if (prefab == null)
        {
            Debug.LogError($"[ClientSpawner] No prefab for uId {unitData.UId}!");
            return;
        }

        var def = unitLibrary.Get(unitData.UId);
        if (def == null)
        {
            Debug.LogError($"[ClientSpawner] No definition for uId {unitData.UId}!");
            return;
        }

        var worldPos = scene.movableTilemap.GetCellCenterWorld(unitData.CurrentCell);
        var go = Instantiate(prefab, worldPos, Quaternion.identity);
        go.name = $"Unit_{unitData.Id}_{def.unitName}";

        var clientUnit = go.GetComponent<ClientUnit>();
        if (clientUnit == null)
        {
            Debug.LogError($"[ClientSpawner] Prefab for uId {unitData.UId} has no ClientUnit!");
            Destroy(go);
            return;
        }

        clientUnit.Init(unitData, session, scene);
        scene.RegisterUnit(clientUnit);

        Debug.Log($"[ClientSpawner] Spawned unit {unitData.Id} ({def.unitName}) at {unitData.CurrentCell}");
    }
}