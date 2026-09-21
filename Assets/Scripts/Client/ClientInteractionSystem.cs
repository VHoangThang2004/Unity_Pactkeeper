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
            if (session.SyncState == SyncStateValue.Idle)
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
            if (session.SyncState != SyncStateValue.Idle)
                ForceLockedInputState();
            yield return null;
        }
    }

    // -------------------------------------------------------
    // Interaction
    // -------------------------------------------------------

    public bool isScrolling = false;
    public void OnScrollButtonClicked()
    {
        if (isScrolling) return;
        isScrolling = true;
        StartCoroutine(scene.visualController.ShowCommandLog(!scene.CommandLogs[0].IsActive()));
    }

    public void HandleTileHover()
    {
        if (scene.movableTilemap == null) return;

        Vector3 worldPos = scene.cam.ScreenToWorldPoint(scene.input.MouseScreenPosition);
        worldPos.z = 0;

        session.currentCellMouseOn = scene.movableTilemap.WorldToCell(worldPos);
        if (!EventSystem.current.IsPointerOverGameObject())
        {
            scene.IsCenteringCell = false;
        }

        if (scene.IsCenteringCell)
        {
            scene.hoverHighlight.SetActive(true);
            return;
        }
        if (!scene.movableTilemap.HasTile(session.currentCellMouseOn) || EventSystem.current.IsPointerOverGameObject())
        {
            session.isOnCell = false;
            scene.hoverHighlight.SetActive(false);
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

    public void SelectWeaponSkill()
    {
        if (session.selectedUnitId == -1) return;
        UnitData data = session.GetUnitDataById(session.selectedUnitId);
        int skillId = data.WeaponSkillId;
        scene.clientInteractionSystem.HandleDecisionSelectSkill(skillId);
    }
    public void SelectOwnedReadyUnit(int index)
    {
        if (session.ownedReadyUnitIds == null) return;
        if (session.ownedReadyUnitIds.Count <= index) return;
        HandleDecisionSelectUnit(session.ownedReadyUnitIds[index]);
    }
    public void SelectEnemyReadyUnit(int index)
    {
        if (session.enemyReadyUnitIds == null) return;
        if (session.enemyReadyUnitIds.Count <= index) return;
        HandleDecisionSelectUnit(session.enemyReadyUnitIds[index]);
    }


    public void ForceNoneState()
    {
        stateMachine?.GoToNone();
    }
    public void HandleTileClick(Vector3Int cell)
    {
        stateMachine?.OnTileClick(cell);
    }

    public void HandleDecisionSelectUnit(int unitId)
    {
        stateMachine?.GoToUnitSelected(unitId);
    }
    public void HandleDecisionSelectSkill(int skillId)
    {
        stateMachine?.OnDecisionSelectSkill(skillId);
    }
    public void HandleDecisionInspectSkill(int skillId)
    {
        stateMachine?.OnDecisionInspectSkill(skillId);
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