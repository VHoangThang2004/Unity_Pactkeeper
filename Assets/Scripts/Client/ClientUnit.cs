using TMPro;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.UI;

/// <summary>
/// Visual representation of a unit. Wired to ClientMatchSession via Init().
/// Auto-update loop watches own UnitData entry in session — reacts automatically
/// when data changes (step count, position, hp, etc.). No one needs to call this directly.
/// </summary>
public class ClientUnit : MonoBehaviour
{
    [Header("Visuals")]
    [SerializeField] public Sprite unitImg;
    [SerializeField] public GameObject hoverUnit;
    [SerializeField] public Animator UnitAnimator;
    [SerializeField] public Animator VFXAnimator;
    [SerializeField] public GameObject VFXSprite;
    [SerializeField] public GameObject UnitAura;

    [SerializeField] private TextMeshProUGUI stepText;
    [SerializeField] private UnitBar hpBar;
    [SerializeField] private UnitBar spBar;

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
        if (session.IsMyUnit(unitId))
        {
            hpBar.Init(unitData.MaxHP, BarType.AllyHP);
        }
        else
        {
            hpBar.Init(unitData.MaxHP, BarType.EnemyHP);
        }
        spBar.Init(unitData.MaxSkillPoint, BarType.SkillPoint);
        UpdateStepUI();
        UpdateBars();

        SyncPositionToCurrentSession();

        StartCoroutine(AutoUpdate());
        ApplyAnimationSpeed();
    }

    void OnEnable()
    {
        UnitAnimationSpeed.Changed += ApplyAnimationSpeed;
        ApplyAnimationSpeed();
    }

    void OnDisable()
    {
        UnitAnimationSpeed.Changed -= ApplyAnimationSpeed;
    }

    public void ApplyAnimationSpeed()
    {
        if (UnitAnimator != null)
            UnitAnimator.speed = UnitAnimationSpeed.Multiplier;
        if (VFXAnimator != null)
            VFXAnimator.speed = UnitAnimationSpeed.Multiplier;
    }

    IEnumerator AutoUpdate()
    {
        while (true)
        {
            //an exception, does not affect the other processes
            UpdateStepUI();
            UpdateBars();

            yield return new WaitForSeconds(0.1f);
            //emergency sync: stop all animations and starts brute sync
            //sync only once, so that the resolve from other place can take place smoothly and uninterrupted
            if (session == null || scene == null || scene.movableTilemap == null)
                continue;

            UnitData currentData = session.GetUnitDataById(unitId);
            if (currentData == null)
                continue; // Unit was killed/removed from session snapshot; wait cleanly until ClientSpawner destroys this GameObject

            if (session.SyncState != SyncStateValue.Idle)
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
            if (currentCellFromTransform != currentData.CurrentCell)
            {
                SyncPositionToCurrentSession();
            }

        }
    }

    // -------------------------------------------------------
    // UI
    // -------------------------------------------------------
    void UpdateBars()
    {
        UnitData data = session.GetUnitDataById(unitId);
        if (data == null || hpBar == null) return;
        hpBar.UpdateValue(data.CurrentHP, data.MaxHP);
        spBar.UpdateValue(data.CurrentSkillPoint, data.MaxSkillPoint);
    }

    public void UpdateStepUI()
    {
        if (stepText == null) return;
        stepText.text = session.GetUnitDataById(unitId)?.CurrentStep.ToString();
        if (stepText.text == "0")
        {
            stepText.text = "";
        }
    }

    // -------------------------------------------------------
    // Position
    // -------------------------------------------------------

    public void SyncPositionToCurrentSession()
    {
        if (session == null || scene == null || scene.movableTilemap == null) return;
        UnitData data = session.GetUnitDataById(unitId);
        if (data == null) return;
        transform.position = scene.movableTilemap.GetCellCenterWorld(data.CurrentCell);
    }
    public void SyncPositionToTarget(Vector3Int cell)
    {
        transform.position = scene.movableTilemap.GetCellCenterWorld(cell);
    }

}