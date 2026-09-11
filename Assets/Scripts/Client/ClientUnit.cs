using TMPro;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Visual representation of a unit. Wired to ClientMatchSession via Init().
/// Auto-update loop watches own UnitData entry in session — reacts automatically
/// when data changes (step count, position, hp, etc.). No one needs to call this directly.
/// </summary>
public class ClientUnit : MonoBehaviour
{
    [Header("Visuals")]
    [SerializeField] public GameObject hoverUnit;
    [SerializeField] private Animator UnitAnimator;
    [SerializeField] private Animator VFXAnimator;
    [SerializeField] private TextMeshProUGUI stepText;
    [SerializeField] private GameObject actionMenuUI;

    [Header("InitRefs")] //put here so notice when something is not initiallized
    private ClientMatchSession session;
    private ClientScene scene;

    // -------------------------------------------------------
    // Data
    // -------------------------------------------------------

    public int unitId; // pointer to data in session

    public bool isResolvingAnimation = false; // can be fetched from afar
    public bool isAtBeforeSnapshot = false; // a mark that makes sure only 1 animation process is running for this unit only.

    // -------------------------------------------------------
    // Init (called by ClientSpawner)
    // -------------------------------------------------------

    public void Init(UnitData unitData, ClientMatchSession session, ClientScene scene)
    {
        this.session = session;
        this.scene = scene;
        if (unitData == null)
        {
            Debug.LogError("NULL data at initallizer, FATAL ERROR!!!!");
            return;
        }
        unitId = unitData.Id;
        //TODO in the future: data has list of active skillIds, link that onto buttons? Or let the visual controller link that on the main UI, for now using individual UIs
        SyncPositionToCurrentSession();

        StartCoroutine(AutoUpdate());
    }

    IEnumerator AutoUpdate()
    {
        while (true)
        {
            //an exception, does not affect the other processes
            UpdateStepUI();
            if (session.selectedUnitId == unitId && actionMenuUI!=null)
            {
                //this unit is selected, shows action menu UI
                actionMenuUI.SetActive(true);
            }
            else
            {
                actionMenuUI.SetActive(false);
            }

            yield return new WaitForSeconds(0.1f);
            //emergency sync: stop all animations and starts brute sync
            //sync only once, so that the resolve from other place can take place smoothly and uninterrupted
            if (session.PendingToken != session.CurrentToken)
            {
                if (!isAtBeforeSnapshot)
                {
                    isAtBeforeSnapshot = true;
                    SyncPositionToCurrentSession(); // position is synced here because it only runs once (isBeforeSnapshot)
                }
                //ignores the part after if sync is not done (might be in resolving progress)
                continue;
            }

            isAtBeforeSnapshot = false; //gets here means syncing completed, flip this back so it can be reused in future syncs
            Vector3Int currentCellFromTransform = scene.movableTilemap.WorldToCell(transform.position);
            if (currentCellFromTransform != session.GetUnitDataById(unitId).CurrentCell)
            {
                SyncPositionToCurrentSession();
            }

        }
    }

    // -------------------------------------------------------
    // UI
    // -------------------------------------------------------

    public void UpdateStepUI()
    {
        if (stepText == null) return;
        stepText.text = session.GetUnitDataById(unitId).CurrentStep.ToString();
    }

    // -------------------------------------------------------
    // Position
    // -------------------------------------------------------

    public void SyncPositionToCurrentSession()
    {
        transform.position = scene.movableTilemap.GetCellCenterWorld(session.GetUnitDataById(unitId).CurrentCell);
    }
    public void SyncPositionToTarget(Vector3Int cell)
    {
        transform.position = scene.movableTilemap.GetCellCenterWorld(cell);
    }

    public void SelectWeaponSkill()
    {
        if(session.selectedUnitId!=unitId) return;
        UnitData data = session.GetUnitDataById(unitId);
        int skillId = data.WeaponSkillId;
        scene.clientInteractionSystem.HandleDecisionSelectSkill(skillId);
    }
}