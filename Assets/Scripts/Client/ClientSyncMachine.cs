using System.Collections;
using UnityEngine;

/// <summary>
/// Owns the client sync flow.
/// States: Idle -> LoadingMap -> SpawningUnits -> Confirmed
/// Each state defines its own validity conditions — checked every frame.
/// Any violation transitions back to Idle immediately.
/// Token is just one of many possible invalidation reasons.
/// </summary>
public class ClientSyncMachine : MonoBehaviour
{
    public enum SyncState
    {
        Idle,
        LoadingMap,
        SpawningUnits
    }

    [Header("Refs")]
    [SerializeField] private ClientMatchSession session;
    [SerializeField] private ClientScene scene;
    [SerializeField] private ClientMapLoader mapLoader;
    [SerializeField] private ClientSpawner spawner;

    public SyncState State { get; private set; } = SyncState.Idle;

    private int syncingToken = -1;
    private Coroutine activeWorker = null;
    private bool workerDone = false;
    private bool workerSuccess = false;

    public void Init()
    {
        StartCoroutine(SyncLoop());
    }

    // -------------------------------------------------------
    // State Validity — each state defines what must be true to remain valid
    // -------------------------------------------------------

    bool IdleIsValid()
    {
        // Idle exits when a new unconfirmed token arrives
        return session.PendingToken == syncingToken;
    }

    bool LoadingMapIsValid()
    {
        // Must be syncing the right token
        if (session.PendingToken != syncingToken) return false;
        // Map asset must exist
        if (session.MapAsset == null) return false;
        return true;
    }

    bool SpawningUnitsIsValid()
    {
        // Must be syncing the right token
        if (session.PendingToken != syncingToken) return false;
        // Map must be loaded in scene before spawning
        if (scene.movableTilemap == null) return false;
        return true;
    }

    // -------------------------------------------------------
    // Main Loop
    // -------------------------------------------------------

    IEnumerator SyncLoop()
    {
        while (true)
        {
            switch (State)
            {
                case SyncState.Idle:
                    yield return RunIdle();
                    break;

                case SyncState.LoadingMap:
                    yield return RunLoadingMap();
                    break;

                case SyncState.SpawningUnits:
                    yield return RunSpawningUnits();
                    break;

            }
            yield return null;
        }
    }

    // -------------------------------------------------------
    // State Runners
    // -------------------------------------------------------

    IEnumerator RunIdle()
    {
        // Wait here until a new token breaks idle validity
        while (IdleIsValid())
            yield return null;
    
        // New token arrived — begin sync
        syncingToken = session.PendingToken;
        Debug.Log($"[SyncMachine] New token {syncingToken}.");
        TransitionTo(SyncState.LoadingMap);
    }

    IEnumerator RunLoadingMap()
    {
        workerDone    = false;
        workerSuccess = false;
        activeWorker  = StartCoroutine(mapLoader.Load(result =>
        {
            workerSuccess = result;
            workerDone    = true;
        }));

        // Guard every frame — state validity is the authority
        while (!workerDone)
        {
            if (!LoadingMapIsValid())
            {
                StopWorker();
                TransitionTo(SyncState.Idle);
                yield break;
            }
            yield return null;
        }

        if (!LoadingMapIsValid() || !workerSuccess)
        {
            TransitionTo(SyncState.Idle);
            yield break;
        }

        TransitionTo(SyncState.SpawningUnits);
    }

    IEnumerator RunSpawningUnits()
    {
        workerDone    = false;
        workerSuccess = false;
        activeWorker  = StartCoroutine(spawner.Sync(result =>
        {
            workerSuccess = result;
            workerDone    = true;
        }));

        // Guard every frame — state validity is the authority
        while (!workerDone)
        {
            if (!SpawningUnitsIsValid())
            {
                StopWorker();
                TransitionTo(SyncState.Idle);
                yield break;
            }
            yield return null;
        }

        if (!SpawningUnitsIsValid() || !workerSuccess)
        {
            TransitionTo(SyncState.Idle);
            yield break;
        }

        TransitionTo(SyncState.Idle);
        Debug.Log($"[SyncMachine] Token {syncingToken} confirmed.");
    }

    // -------------------------------------------------------
    // Helpers
    // -------------------------------------------------------

    void StopWorker()
    {
        if (activeWorker != null)
        {
            StopCoroutine(activeWorker);
            activeWorker = null;
        }
        Debug.Log($"[SyncMachine] State {State} invalidated — worker stopped.");
    }

    void TransitionTo(SyncState next)
    {
        Debug.Log($"[SyncMachine] {State} -> {next}");
        State = next;
    }
}