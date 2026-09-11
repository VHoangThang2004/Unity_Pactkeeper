using System.Collections;
using UnityEngine;

public class ClientSyncMachine : MonoBehaviour
{
    public enum SyncState { Idle, LoadingMap, SpawningUnits }

    [Header("Refs")]
    [SerializeField] private ClientMatchSession session;
    [SerializeField] private ClientScene scene;
    [SerializeField] private ClientMapLoader mapLoader;
    [SerializeField] private ClientSpawner spawner;

    public SyncState State { get; private set; } = SyncState.Idle;
    public bool IsSyncing => State != SyncState.Idle;

    private bool syncRequested = false;

    public void Init()
    {
        StartCoroutine(SyncLoop());
    }

    public void RequestSync()
    {
        syncRequested = true;
    }

    IEnumerator SyncLoop()
    {
        while (true)
        {
            // Wait for sync request
            while (!syncRequested)
                yield return null;

            syncRequested = false;

            // Load map if needed
            if (session.MapAsset != null && scene.movableTilemap == null)
            {
                TransitionTo(SyncState.LoadingMap);

                bool mapDone = false;
                bool mapSuccess = false;
                StartCoroutine(mapLoader.Load(result =>
                {
                    mapSuccess = result;
                    mapDone = true;
                }));

                while (!mapDone) yield return null;

                if (!mapSuccess)
                {
                    TransitionTo(SyncState.Idle);
                    continue;
                }
            }

            // Sync units
            TransitionTo(SyncState.SpawningUnits);

            bool spawnDone = false;
            bool spawnSuccess = false;
            StartCoroutine(spawner.Sync(result =>
            {
                spawnSuccess = result;
                spawnDone = true;
            }));

            while (!spawnDone) yield return null;

            TransitionTo(SyncState.Idle);
            Debug.Log($"[SyncMachine] Sync complete.");
        }
    }

    void TransitionTo(SyncState next)
    {
        Debug.Log($"[SyncMachine] {State} -> {next}");
        State = next;
    }
}