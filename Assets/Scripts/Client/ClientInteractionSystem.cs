using System.Collections;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.EventSystems;

public class ClientInteractionSystem : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private ClientMatchSession session;
    [SerializeField] private ClientScene scene;

    public InteractionStateMachine stateMachine;
    // this state machine will affect most of the visible of UI elements to control the input & flow of UI for inputs. One of the main-most important state machine beside sync-machine

    // -------------------------------------------------------
    // Init (called by ClientMapLoader after map is loaded)
    // -------------------------------------------------------

    public void Init()
    {
        stateMachine = new InteractionStateMachine(session, scene);
        stateMachine.Init();
        scene.input.OnLeftClick += HandleClick;
        StartCoroutine(InteractionManagingLoop());
    }

    void HandleClick()
    {
        if (scene.movableTilemap == null) return;
        // if (!session.isOnCell) return;
        if (EventSystem.current.IsPointerOverGameObject()) return;
        HandleTileClick(session.currentCellMouseOn);
    }

    private Coroutine unlockCoroutine = null;
    public void PromiseUnlock()
    {
        if (unlockCoroutine != null)
            StopCoroutine(unlockCoroutine);
        unlockCoroutine = StartCoroutine(UnlockOnSyncAndResolveCompleted());
    }
    IEnumerator UnlockOnSyncAndResolveCompleted()
    {
        while (true)
        {
            if (session.PendingToken == session.CurrentToken && session.SyncState == 0)
            {
                stateMachine.GoToNone();
                yield break;
            }
            yield return null;
        }
    }
    IEnumerator InteractionManagingLoop()
    {
        while (true)
        {
            HandleTileHover();
            if (session.CurrentToken != session.PendingToken)
                ForceLockedInputState();
            yield return null;
        }
    }

    // -------------------------------------------------------
    // Interaction
    // -------------------------------------------------------

    public void HandleTileHover()
    {
        if (scene.movableTilemap == null) return;

        Vector3 worldPos = scene.cam.ScreenToWorldPoint(scene.input.MouseScreenPosition);
        worldPos.z = 0;

        session.currentCellMouseOn = scene.movableTilemap.WorldToCell(worldPos);

        if (!scene.movableTilemap.HasTile(session.currentCellMouseOn))
        {
            if (scene.hoverHighlight != null && scene.hoverHighlight.activeSelf)
                scene.hoverHighlight.SetActive(false);
            session.isOnCell = false;
            return;
        }

        session.isOnCell = true;
        Vector3 center = scene.movableTilemap.GetCellCenterWorld(session.currentCellMouseOn);

        if (scene.hoverHighlight != null && !scene.hoverHighlight.activeSelf)
            scene.hoverHighlight.SetActive(true);

        if (scene.hoverHighlight != null)
            scene.hoverHighlight.transform.position = center + scene.offset;

        stateMachine.OnTileHover(session.currentCellMouseOn);
    }

    public void ForceLockedInputState()
    {
        stateMachine.GoToLocked();
    }

    public void ForceNoneState()
    {
        stateMachine?.GoToNone();
    }
    public void HandleTileClick(Vector3Int cell)
    {
        stateMachine?.OnTileClick(cell);
    }
    public void HandleDecisionSelectSkill(int skillId)
    {
        stateMachine?.OnDecisionSelectSkill(skillId);
    }
    public void HandleDecisionMoveOrSkill()
    {
        if (stateMachine.currentState is UnitSelectedState)
        {
            stateMachine?.OnDecision();
            return;
        }
    }
    public void HandleWait()
    {
        stateMachine?.OnWait();
    }
    public void HandleCancel()
    {
        stateMachine?.OnCancel();
    }
    void OnDestroy()
    {
        scene.input.OnLeftClick -= HandleClick;
    }

}