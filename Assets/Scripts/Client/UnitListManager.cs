using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class UnitListManager : MonoBehaviour
{
    [Header("Config")]
    [SerializeField] public SceneConfig sceneConfig;
    [SerializeField] public BackendConfig clientBackendConfig;
    [SerializeField] public UnitPrefabRegistry unitPrefabRegistry;
    [SerializeField] public ClassDefinitionRegistry classLibrary;

    [Header("Loading")]
    [SerializeField] public LoadingScreen loadingScreen;

    [Header("Formation (fixed 5 slots — pre-placed in inspector)")]
    [SerializeField] public List<UnitContainer> formationSlots;

    [Header("Unit List (spawned at runtime)")]
    [SerializeField] public Transform unitListContainer;
    [SerializeField] public GameObject unitContainerPrefab;

    [Header("State Machine")]
    [SerializeField] public UnitListStateMachine stateMachine;

    public List<OwnedUnitDto> ownedUnits = new();
    public List<int> formationUIds = new();
    public List<UnitContainer> unitListItems = new();

    public bool IsBusy { get; private set; } = true;
    private bool _initialTutorialSetupDone = false;

    void Awake()
    {
        IsBusy = true;
    }

    void Start()
    {
        unitPrefabRegistry.Init();
        classLibrary.Init();
        StartCoroutine(InitSequence());
    }

    IEnumerator InitSequence()
    {
        IsBusy = true;
        loadingScreen?.Show();

        yield return StartCoroutine(FetchOwnedUnits());
        yield return StartCoroutine(FetchFormation());

        if (TutorialContext.IsActive && !_initialTutorialSetupDone)
        {
            _initialTutorialSetupDone = true;
            EnsureTutorialFormationHasBenchAndEmptySlot();
        }

        PopulateFormationSlots();
        PopulateUnitList();
        EnsureBenchDropZone();

        stateMachine.Init(formationSlots, unitListItems);

        loadingScreen?.Hide();
        loadingScreen = null;
        IsBusy = false;
    }

    void EnsureTutorialFormationHasBenchAndEmptySlot()
    {
        if (ownedUnits == null || ownedUnits.Count == 0 || formationUIds == null)
            return;

        if (ownedUnits.Count == 1)
        {
            // With only 1 owned unit, place it on the bench initially so the player can drag it into a slot
            formationUIds.Clear();
            return;
        }

        int maxAllowedInFormation = Mathf.Min(formationSlots != null && formationSlots.Count > 1 ? formationSlots.Count - 1 : 4, ownedUnits.Count - 1);
        if (formationUIds.Count > maxAllowedInFormation)
        {
            formationUIds.RemoveRange(maxAllowedInFormation, formationUIds.Count - maxAllowedInFormation);
        }
    }

    // -------------------------------------------------------
    // Fetch
    // -------------------------------------------------------

    IEnumerator FetchOwnedUnits()
    {
        yield return FormationApi.GetOwnedUnits(clientBackendConfig, units =>
        {
            if (units == null) loadingScreen?.Show("Failed to load units.");
            else ownedUnits = units;
        });
    }

    IEnumerator FetchFormation()
    {
        yield return FormationApi.GetFormation(clientBackendConfig, uIds => formationUIds = uIds);
    }

    // -------------------------------------------------------
    // Populate
    // -------------------------------------------------------

    void PopulateFormationSlots()
    {
        for (int i = 0; i < formationSlots.Count; i++)
        {
            var slot = formationSlots[i];
            slot.Init(stateMachine);
            AttachDrag(slot);

            if (i < formationUIds.Count)
            {
                OwnedUnitDto unit = ownedUnits.Find(u => u.unitDefinitionUId == formationUIds[i]);
                if (unit != null)
                    BindUnit(slot, unit, true);
                else
                    slot.SetEmpty();
            }
            else
            {
                slot.SetEmpty();
            }

            ConfigureFormationSlotTutorialTargets(slot, i);
        }
    }

    void ConfigureFormationSlotTutorialTargets(UnitContainer slot, int slotIndex)
    {
        if (slot == null) return;

        if (slot.EmptySlotStatePanel != null)
        {
            var emptyTarget = slot.EmptySlotStatePanel.GetComponent<TutorialTarget>();
            if (emptyTarget == null)
                emptyTarget = slot.EmptySlotStatePanel.AddComponent<TutorialTarget>();
            TutorialTargetRegistry.Unregister("C0S3_1", emptyTarget);
            emptyTarget.Configure("C0S3_1");
        }

        if (slot.MainFrameObject != null)
        {
            var mainTarget = slot.MainFrameObject.GetComponent<TutorialTarget>();
            if (mainTarget == null)
                mainTarget = slot.MainFrameObject.AddComponent<TutorialTarget>();

            if (slotIndex == 0)
                mainTarget.Configure("C0S3_4", "C0S3_5", "C0S3_0");
            else
                mainTarget.Configure("C0S3_0");
        }
    }

    void PopulateUnitList()
    {
        foreach (Transform child in unitListContainer) Destroy(child.gameObject);
        unitListItems.Clear();

        var formationOwnedIds = new HashSet<string>();
        foreach (var slot in formationSlots)
            if (!slot.isEmpty) formationOwnedIds.Add(slot.ownedUnitId);

        ownedUnits.Sort((a, b) => a.unitDefinitionUId.CompareTo(b.unitDefinitionUId));

        foreach (var unit in ownedUnits)
        {
            if (formationOwnedIds.Contains(unit.ownedUnitId)) continue;

            var go = Instantiate(unitContainerPrefab, unitListContainer);
            var container = go.GetComponent<UnitContainer>();
            container.Init(stateMachine);
            BindUnit(container, unit, false);
            AttachDrag(container);

            if (container.MainFrameObject != null)
            {
                var benchTarget = container.MainFrameObject.GetComponent<TutorialTarget>();
                if (benchTarget == null)
                    benchTarget = container.MainFrameObject.AddComponent<TutorialTarget>();
                TutorialTargetRegistry.Unregister("C0S3_0", benchTarget);
                benchTarget.Configure("C0S3_2");
            }

            unitListItems.Add(container);
        }
    }

    // -------------------------------------------------------
    // Public
    // -------------------------------------------------------

    public void RebuildUnitList()
    {
        PopulateUnitList();
        stateMachine.UpdateUnitListItems(unitListItems);
    }

    public void BindUnit(UnitContainer container, OwnedUnitDto unit, bool asFormationSlot)
    {
        var prefab = unitPrefabRegistry.Get(unit.unitDefinitionUId);
        Sprite portrait = prefab != null ? prefab.GetComponent<ClientUnit>()?.unitImg : null;
        container.Setup(
            unit.ownedUnitId,
            unit.unitDefinitionUId,
            portrait,
            unitPrefabRegistry.GetName(unit.unitDefinitionUId),
            unit.unlockedClassIds != null && unit.unlockedClassIds.Length > 0 ? classLibrary.GetFullFrame(unit.unlockedClassIds[0]) : null,
            unit.unlockedClassIds != null && unit.unlockedClassIds.Length > 1 ? classLibrary.GetHalfFrame(unit.unlockedClassIds[1]) : null,
            asFormationSlot);
    }

    void AttachDrag(UnitContainer container)
    {
        if (container != null && container.GetComponent<UnitListDragHandler>() == null)
            container.gameObject.AddComponent<UnitListDragHandler>();
    }

    void EnsureBenchDropZone()
    {
        if (unitListContainer == null) return;

        MarkBench(unitListContainer.gameObject);
        var scroll = unitListContainer.GetComponentInParent<UnityEngine.UI.ScrollRect>();
        if (scroll == null) return;

        MarkBench(scroll.gameObject);
        if (scroll.viewport != null)
            MarkBench(scroll.viewport.gameObject);
    }

    static void MarkBench(GameObject go)
    {
        if (go != null && go.GetComponent<UnitListDropCatcher>() == null)
            go.AddComponent<UnitListDropCatcher>();
    }

    public void OnClickReturnMainMenu()
    {
        if (TutorialContext.IsActive)
        {
            Debug.Log("[UnitList] Tutorial active - deferring scene transition to StoryScene manager.");
            return;
        }
        UnityEngine.SceneManagement.SceneManager.LoadScene(sceneConfig.mainMenuScene);
    }

    public IEnumerator SaveFormationToBackend(List<int> uIds)
    {
        IsBusy = true;
        yield return FormationApi.SaveFormation(clientBackendConfig, uIds);
        yield return StartCoroutine(InitSequence());
        IsBusy = false;
    }
}