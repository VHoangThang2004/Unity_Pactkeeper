using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

/// <summary>
/// Thin intersection layer. Starts client init, routes bridge RPCs to session,
/// routes input to interaction system, routes decisions to bridge.
/// Does not own any data or scene objects — those live in ClientMatchSession and ClientScene.
/// </summary>
public class ClientController : MonoBehaviour, ISyncedBridgeClient
{
    public event System.Action OnMatchReady;
    public bool IsMatchReady { get; private set; }

    public SyncedBridge bridge;

    [Header("Data")]
    [SerializeField] public ClientMatchSession session;

    [Header("Ref")]
    [SerializeField] private ClientScene scene;
    [SerializeField] private SceneConfig sceneConfig;
    [SerializeField] private FormationPrepManager prep; // null in scenes without formation prep

    public ClientScene Scene => scene;

    [Header("Audio")]
    public AudioClip battleBGM;

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
    private void Start()
    {
        scene.loadingScreen.Show("Connecting to server...");
        StartCoroutine(ConnectWithRetry());
    }

    [Header("Connection Retry")]
    [Tooltip("Total StartClient attempts before giving up (the dedicated server needs 1-3s to boot and open its UDP port).")]
    [SerializeField] private int connectMaxAttempts = 4;
    [Tooltip("Seconds to wait for the SyncedBridge to spawn after each StartClient attempt.")]
    [SerializeField] private float connectAttemptTimeout = 6f;
    [SerializeField] private float connectRetryDelay = 1.5f;

    /// <summary>
    /// Previously a single StartClient() call: if the freshly spawned server was not listening yet
    /// (or crashed), ClientMenuReturner swallowed the disconnect during tutorials and the screen stayed on
    /// "Connecting to server..." forever. Now we retry a few times, then leave cleanly.
    /// </summary>
    IEnumerator ConnectWithRetry()
    {
        for (int attempt = 1; attempt <= connectMaxAttempts; attempt++)
        {
            if (attempt > 1)
            {
                scene.loadingScreen.Show($"Connecting to server... (retry {attempt - 1}/{connectMaxAttempts - 1})");

                var nm = NetworkManager.Singleton;
                if (nm != null && nm.IsListening)
                {
                    nm.Shutdown();
                    float shutdownWait = 0f;
                    while (nm != null && nm.IsListening && shutdownWait < 2f)
                    {
                        shutdownWait += Time.unscaledDeltaTime;
                        yield return null;
                    }
                }
                yield return new WaitForSecondsRealtime(connectRetryDelay);
            }

            ConnectToServer();

            float waited = 0f;
            while (bridge == null && waited < connectAttemptTimeout)
            {
                // StartClient failed or the server refused/closed the connection -> retry right away
                if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening) break;
                waited += Time.unscaledDeltaTime;
                yield return null;
            }

            if (bridge != null) yield break; // OnBridgeReady took over
            Debug.LogWarning($"[ClientController] Connect attempt {attempt}/{connectMaxAttempts} failed.");
        }

        Debug.LogError("[ClientController] Could not reach the battle server - leaving the match.");
        scene.loadingScreen.Show("Cannot reach the battle server. Returning...");
        yield return new WaitForSecondsRealtime(2f);

        bool wasTutorial = TutorialContext.IsActive;
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            NetworkManager.Singleton.Shutdown();
        TutorialContext.Reset();

        string dest = wasTutorial && sceneConfig != null && Application.CanStreamedLevelBeLoaded(sceneConfig.storyProgressScene)
            ? sceneConfig.storyProgressScene
            : (sceneConfig != null ? sceneConfig.mainMenuScene : "3_MainMenu");
        UnityEngine.SceneManagement.SceneManager.LoadScene(dest);
    }

    bool ConnectToServer()
    {
        var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();

        transport.SetConnectionData(
            PlayerSession.ServerIp,
            (ushort)PlayerSession.ServerPort);

        NetworkManager.Singleton.NetworkConfig.ConnectionData =
            Encoding.UTF8.GetBytes(PlayerSession.Token);

        return NetworkManager.Singleton.StartClient();
    }

    
    public void OnBridgeReady(SyncedBridge bridge)
    {
        this.bridge = bridge;
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
            if (prep == null || !prep.IsActive) // formation prep can legitimately take minutes
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

        if (AudioManager.Instance != null && battleBGM != null)
            AudioManager.Instance.PlayBGM(battleBGM);

        //Start the statemachines that requires data from now on
        scene.visualController.StartUILoop();
        IsMatchReady = true;
        OnMatchReady?.Invoke();
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

    public void RequestForfeit()
    {
        if (bridge != null)
        {
            Debug.Log("[ClientController] Sending forfeit request to server.");
            bridge.RequestForfeitServerRpc();
        }
        else
        {
            Debug.LogWarning("[ClientController] Cannot forfeit — SyncedBridge is null.");
        }
    }

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

    // Server answers RequestInitialState with this while formation prep runs (repeats every ~1s)
    public void OnPrepInfo(string mapId, int teamId, float secondsLeft)
    {
        if (prep == null || teamId < 0) return;
        if (prep.IsActive) prep.SyncTimer(secondsLeft);
        else prep.Begin(mapId, teamId, secondsLeft);
    }

    public void OnPrepEnded()
    {
        if (prep == null || !prep.IsActive) return;
        prep.End();
        scene.loadingScreen.Show("Loading...");
    }

    public void OnUnitSpawned(UnitData unitData)
    {
        // Ignored — ClientSpawner handles unit sync via token system
    }

}