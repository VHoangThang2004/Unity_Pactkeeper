using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using TMPro;

public class UnitListManager : MonoBehaviour
{
    [Header("Config")]
    [SerializeField] public SceneConfig sceneConfig;
    [SerializeField] public BackendConfig clientBackendConfig;
    [SerializeField] public UnitPrefabRegistry unitPrefabRegistry;

    [Header("Loading")]
    [SerializeField] public LoadingScreen loadingScreen;

    [Header("Formation (fixed 5 slots — pre-placed in inspector)")]
    [SerializeField] public List<UnitContainer> formationSlots;

    [Header("Unit List (spawned at runtime)")]
    [SerializeField] public Transform unitListContainer;
    [SerializeField] public GameObject unitContainerPrefab;

    [Header("State Machine")]
    [SerializeField] public UnitListStateMachine stateMachine;

    private List<OwnedUnitDto> ownedUnits = new();
    private List<int> formationUIds = new();
    public List<UnitContainer> unitListItems = new();

    void Start()
    {
        unitPrefabRegistry.Init();
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
        using var request = UnityWebRequest.Get($"{clientBackendConfig.backendUrl}/api/PlayerProfile");
        clientBackendConfig.SetHeaders(request, PlayerSession.Token);
        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            loadingScreen?.Show("Failed to load units.");
            Debug.LogError($"[UnitList] FetchOwnedUnits: {request.error}");
            yield break;
        }

        var profile = JsonUtility.FromJson<PlayerProfileResponse>(request.downloadHandler.text);
        ownedUnits = profile.ownedUnits != null ? new List<OwnedUnitDto>(profile.ownedUnits) : new List<OwnedUnitDto>();
    }

    IEnumerator FetchFormation()
    {
        using var request = UnityWebRequest.Get($"{clientBackendConfig.backendUrl}/api/TeamLoadout");
        clientBackendConfig.SetHeaders(request, PlayerSession.Token);
        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError($"[UnitList] FetchFormation: {request.error}");
            yield break;
        }

        var wrapper = JsonUtility.FromJson<IntArrayWrapper>("{\"items\":" + request.downloadHandler.text + "}");
        formationUIds = wrapper.items != null ? new List<int>(wrapper.items) : new List<int>();
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
                var unit = ownedUnits.Find(u => u.unitDefinitionUId == formationUIds[i]);
                if (unit != null)
                {
                    var prefab = unitPrefabRegistry.Get(unit.unitDefinitionUId);
                    Sprite portrait = prefab != null ? prefab.GetComponent<ClientUnit>()?.unitImg : null;
                    slot.Setup(unit.ownedUnitId, unit.unitDefinitionUId, portrait, unitPrefabRegistry.GetName(unit.unitDefinitionUId), true);
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
            container.Setup(unit.ownedUnitId, unit.unitDefinitionUId, portrait, unitPrefabRegistry.GetName(unit.unitDefinitionUId),false);
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
        string body = JsonUtility.ToJson(new SaveLoadoutDto { UIds = uIds });
        using var request = UnityWebRequest.Put($"{clientBackendConfig.backendUrl}/api/TeamLoadout", body);
        request.SetRequestHeader("Content-Type", "application/json");
        clientBackendConfig.SetHeaders(request, PlayerSession.Token);
        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError($"[UnitList] SaveFormation failed: {request.error}");
            StartCoroutine(InitSequence()); // reload formation from backend to ensure UI consistency
        }
        else
        {
            Debug.Log("[UnitList] Formation saved.");
            StartCoroutine(InitSequence()); // reload formation from backend to ensure UI consistency
        }
    }

    [System.Serializable]
    private class SaveLoadoutDto { public List<int> UIds; }

    // -------------------------------------------------------
    // Response Models
    // -------------------------------------------------------

    [System.Serializable] private class IntArrayWrapper { public int[] items; }
}