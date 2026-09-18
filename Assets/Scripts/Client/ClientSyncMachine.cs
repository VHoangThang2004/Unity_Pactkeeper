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
        // syncLoopCoroutine = StartCoroutine(SyncProcess());
    }

    public IEnumerator RequestSync()
    {
        if (syncRequested)
        {
            Debug.LogWarning($"[SyncMachine] Sync in progress");
            yield break;
        }
        syncRequested = true;
        yield return SyncProcess();
    }

    public void ForceReset()
    {
        Debug.LogWarning("[SyncMachine] Force reset triggered");
        syncRequested = false;
        State = SyncState.Idle;
    }

    private IEnumerator SyncProcess()
    {

        // Load map if needed
        if (session.MapAsset != null && scene.movableTilemap == null)
        {
            TransitionTo(SyncState.LoadingMap);

            bool mapDone = false;
            bool mapSuccess = false;
            yield return mapLoader.Load(result =>
            {
                try
                {
                    mapSuccess = result;
                    mapDone = true;
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[SyncMachine] Map callback error: {e}");
                    mapSuccess = false;
                    mapDone = true;
                }
            });


            if (!mapSuccess || !mapDone)
            {
                ForceReset();
                yield break;
            }
        }

        // Sync units
        TransitionTo(SyncState.SpawningUnits);

        bool spawnDone = false;
        bool spawnSuccess = false;
        yield return spawner.Sync(result =>
        {
            try
            {
                spawnSuccess = result;
                spawnDone = true;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[SyncMachine] Spawn callback error: {e}");
                spawnSuccess = false;
                spawnDone = true;
            }
        });

        TransitionTo(SyncState.Idle);
        Debug.Log($"[SyncMachine] Sync complete.{spawnDone} - {spawnSuccess}");

    }

    void TransitionTo(SyncState next)
    {
        Debug.Log($"[SyncMachine] {State} -> {next}");
        State = next;
        if(next == SyncState.Idle)
        {
            syncRequested = false;
        }
    }
}