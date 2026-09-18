using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Thin intersection layer. Starts client init, routes bridge RPCs to session,
/// routes input to interaction system, routes decisions to bridge.
/// Does not own any data or scene objects — those live in ClientMatchSession and ClientScene.
/// </summary>
public class ClientController : MonoBehaviour
{
    [Header("Bridge")]
    [SerializeField] private SyncedBridge bridge;

    [Header("Data")]
    [SerializeField] public ClientMatchSession session;

    [Header("Ref")]
    [SerializeField] private ClientScene scene;
    [SerializeField] private SceneConfig sceneConfig;
    // -------------------------------------------------------
    // Error Guard
    // -------------------------------------------------------

    private int trialErrorCount = 0;
    private int totalErrorCount = 0;
    private bool initPackageReceived = false;


    IEnumerator ClientGuard()
    {
        while (true)
        {
            if (trialErrorCount > 10 || totalErrorCount > 50)
            {
                Debug.LogError("[ClientController] Too many errors — critically wrong!");
                // TODO: disconnect, return to main menu
                yield break;
            }
            yield return new WaitForSeconds(60f);
            trialErrorCount = 0;
        }
    }

    void IncrementErrors()
    {
        trialErrorCount++;
        totalErrorCount++;
    }

    // -------------------------------------------------------
    // Unity
    // -------------------------------------------------------

    void Start()
    {
        scene.loadingScreen.Show("Loading...");
        StartCoroutine(ClientGuard());
        StartCoroutine(InitSequence());
    }


    void Update()
    {
        scene.cameraController.Move(scene.input.MoveInput, scene.input.IsFastMove);
        scene.cameraController.Zoom(scene.input.ZoomInput);
    }

    // -------------------------------------------------------
    // Init Sequence
    // -------------------------------------------------------

    IEnumerator InitSequence()
    {
        while (!TryInitRegistries())
        {
            Debug.LogError("[ClientController] Registry init failed, retrying...");
            IncrementErrors();
            yield return new WaitForSeconds(1f);
        }
        // Request initial state — SyncMachine takes over once snapshot arrives
        StartCoroutine(SyncReceiverLoop());
        //Init complete, ready to receive first snapshot of the match session
        scene.syncMachine.Init();
        session.Init();
        yield return TryRequestInitialStateServer();

        // Wait until first sync confirmed (with timeout)
        float initTimeout = 30f;
        float elapsed = 0f;
        while (session.CurrentToken == -1 && elapsed < initTimeout)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (session.CurrentToken == -1)
        {
            Debug.LogError("[ClientController] Init sequence timed out waiting for first snapshot");
            IncrementErrors();
            yield break;
        }

        Debug.Log("[ClientController] Client initialized successfully.");
        //Start the statemachines that requires data from now on
        scene.visualController.StartUILoop();
    }

    IEnumerator TryRequestInitialStateServer()
    {
        while (!initPackageReceived)
        {
            bridge.RequestInitialStateServerRpc();
            yield return new WaitForSeconds(1f);
        }
    }

    bool TryInitRegistries()
    {
        try
        {
            scene.unitPrefabRegistry.Init();
            scene.skillLibrary.Init();
            session.mapRegistry.Init();
            scene.effectRegistry.Init();
            return true;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[ClientController] Registry init exception: {e.Message}");
            return false;
        }
    }
    // -------------------------------------------------------
    // Bridge -> Server
    // -------------------------------------------------------

    public void TryDecision(int unitId, Vector3Int target, DecisionType type, int skillId)
        => bridge.SendDecisionServerRpc(unitId, target, type, skillId, session.CurrentToken);

    // -------------------------------------------------------
    // Bridge -> Client (from SyncedBridge RPCs)
    // -------------------------------------------------------

    //Snapshot queue (to makes sure sync packages doesnt friendly fire)

    private struct SnapshotPackage
    {
        public SessionSnapshotData snapshot;
        public ResolveData resolve;
        public SecretData secret;
        public DecisionRequestData decision;
        public int token;
    }
    private Queue<SnapshotPackage> snapshotQueue = new Queue<SnapshotPackage>();
    private SnapshotPackage? latestNonResolvePackage = null;
    public void OnSnapshotReceived(SessionSnapshotData snapshot, ResolveData resolve, SecretData secret, DecisionRequestData decision, int token)
    {
        var pkg = new SnapshotPackage
        {
            snapshot = snapshot,
            resolve = resolve,
            secret = secret,
            decision = decision,
            token = token
        };

        if (resolve.HasResolve)
        {
            snapshotQueue.Enqueue(pkg);
            latestNonResolvePackage = null;
        }
        else
            latestNonResolvePackage = pkg;

        initPackageReceived = true;
    }

    IEnumerator SyncReceiverLoop()
    {
        while (true)
        {
            if (session.SyncState == SyncStateValue.Idle || session.SyncState == SyncStateValue.Uninitialized)
            {
                SnapshotPackage? pkg = null;

                if (snapshotQueue.Count > 0)
                    pkg = snapshotQueue.Dequeue();
                else if (latestNonResolvePackage.HasValue)
                {
                    pkg = latestNonResolvePackage.Value;
                    latestNonResolvePackage = null;
                }

                if (pkg.HasValue) // only process if token is newer than current session token
                    yield return session.ApplySnapshot(pkg.Value.snapshot, pkg.Value.resolve, pkg.Value.secret, pkg.Value.decision, pkg.Value.token);
            }

            yield return null;
        }
    }
    public void OnTimelineTick(TimelineData data)
    {
        session.ApplyTimelineTick(data);
        Debug.Log($"[Client] Tick {data.currentInstant}/{data.maxInstant}");
    }

    public void OnUnitSpawned(UnitData unitData)
    {
        // Ignored — ClientSpawner handles unit sync via token system
    }

}