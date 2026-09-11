using System.Collections;
using System.Collections.Generic;
using UnityEngine;

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

        // Wait until first sync confirmed
        yield return new WaitUntil(() => session.CurrentToken != -1);
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
            scene.unitLibrary.Init();
            scene.unitPrefabRegistry.Init(scene.unitLibrary);
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
    private SnapshotPackage? latestQueuedPackage = null;

    public void OnSnapshotReceived(SessionSnapshotData snapshot, ResolveData resolve, SecretData secret, DecisionRequestData decision, int token)
    {
        latestQueuedPackage = new SnapshotPackage
        {
            snapshot = snapshot,
            resolve = resolve,
            secret = secret,
            decision = decision,
            token = token
        };
        initPackageReceived = true;
    }

    IEnumerator SyncReceiverLoop()
    {
        while (true)
        {
            if ((session.SyncState == 0 || session.SyncState == -1) && latestQueuedPackage.HasValue)
            {
                var pkg = latestQueuedPackage.Value;
                latestQueuedPackage = null;
                session.ApplySnapshot(pkg.snapshot, pkg.resolve, pkg.secret, pkg.decision, pkg.token);
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