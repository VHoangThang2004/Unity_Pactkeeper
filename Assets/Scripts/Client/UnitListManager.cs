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

    void Start()
    {
        unitPrefabRegistry.Init();
        classLibrary.Init();
        StartCoroutine(InitSequence());
    }

    IEnumerator InitSequence()
    {
        loadingScreen?.Show();

        yield return StartCoroutine(FetchOwnedUnits());
        yield return StartCoroutine(FetchFormation());

        PopulateFormationSlots();
        PopulateUnitList();

        stateMachine.Init(formationSlots, unitListItems);

        loadingScreen?.Hide();
        loadingScreen = null;
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

            if (i < formationUIds.Count)
            {
                OwnedUnitDto unit = ownedUnits.Find(u => u.unitDefinitionUId == formationUIds[i]);
                if (unit != null)
                {
                    var prefab = unitPrefabRegistry.Get(unit.unitDefinitionUId);
                    Sprite portrait = prefab != null ? prefab.GetComponent<ClientUnit>()?.unitImg : null;
                    slot.Setup(
                        unit.ownedUnitId,
                        unit.unitDefinitionUId,
                        portrait,
                        unitPrefabRegistry.GetName(unit.unitDefinitionUId),
                        unit.unlockedClassIds.Length > 0 ? classLibrary.GetFullFrame(unit.unlockedClassIds[0]) : null,
                        unit.unlockedClassIds.Length > 1 ? classLibrary.GetHalfFrame(unit.unlockedClassIds[1]) : null,
                        true);
                }
                else
                    slot.SetEmpty();
            }
            else
            {
                slot.SetEmpty();
            }
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
            var prefab = unitPrefabRegistry.Get(unit.unitDefinitionUId);
            Sprite portrait = prefab != null ? prefab.GetComponent<ClientUnit>()?.unitImg : null;
            container.Setup(
                        unit.ownedUnitId,
                        unit.unitDefinitionUId,
                        portrait,
                        unitPrefabRegistry.GetName(unit.unitDefinitionUId),
                        unit.unlockedClassIds.Length > 0 ? classLibrary.GetFullFrame(unit.unlockedClassIds[0]) : null,
                        unit.unlockedClassIds.Length > 1 ? classLibrary.GetHalfFrame(unit.unlockedClassIds[1]) : null,
                        false);
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

    public void OnClickReturnMainMenu()
    {
        UnityEngine.SceneManagement.SceneManager.LoadScene(sceneConfig.mainMenuScene);
    }
    public IEnumerator SaveFormationToBackend(List<int> uIds)
    {
        yield return FormationApi.SaveFormation(clientBackendConfig, uIds);
        StartCoroutine(InitSequence()); // reload formation from backend to ensure UI consistency
    }
}