using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

public class UnitConfigManager : MonoBehaviour
{
    [Header("Config")]
    [SerializeField] public SceneConfig sceneConfig;
    [SerializeField] public BackendConfig clientBackendConfig;
    [SerializeField] public UnitPrefabRegistry unitPrefabRegistry;
    [SerializeField] public ClassDefinitionRegistry classLibrary;

    [SerializeField] public SkillLibrary skillLibrary;
    [SerializeField] public ClientEffectRegistry effectRegistry;
    [SerializeField] public WeaponDefinitionRegistry weaponRegistry;
    [SerializeField] public TrinketDefinitionRegistry trinketRegistry;

    [Header("Loading")]
    [SerializeField] private LoadingScreen loadingScreen;
    [Header("UI")]
    [SerializeField] public UnitContainer currentUnitContainer;
    [SerializeField] public TextMeshProUGUI unitHp;
    [SerializeField] public TextMeshProUGUI unitSP;
    [SerializeField] public TextMeshProUGUI unitSpeed;
    [SerializeField] public TextMeshProUGUI unitDamageMultiplier;
    [SerializeField] public TextMeshProUGUI unitDamageReduction;

    [Header("Unique Passive (PassivePanel, below the stats) — one per unit, display only")]
    [SerializeField] private Image passiveIcon;
    [SerializeField] private TextMeshProUGUI passiveName;
    [SerializeField] private TextMeshProUGUI passiveDescription;

    [Header("Slots")]
    [SerializeField] public SlotConfig movementSkillSlot;
    [SerializeField] public SlotConfig classSkillSlot;
    [SerializeField] public SlotConfig weaponSlot;
    [SerializeField] public SlotConfig trinketSlot;

    // -------------------------------------------------------
    // Central Data
    // -------------------------------------------------------
    public UnitConfigDto CurrentUnit { get; private set; }
    public OwnedUnitDto CurrentUnitDataWithUnlockedClass { get; private set; }
    public UnitGradeStatsDto CurrentStats { get; private set; }
    public List<ClassSkillResultDto> AvailableMovementSkills { get; private set; } = new();
    public List<ClassSkillResultDto> AvailableClassSkills { get; private set; } = new();
    public List<OwnedWeaponResultDto> AvailableWeapons { get; private set; } = new();
    public List<OwnedTrinketResultDto> AvailableTrinkets { get; private set; } = new();
    public List<WeaponDefinitionDto> WeaponDefinitions { get; private set; } = new();
    public List<TrinketDefinitionDto> TrinketDefinitions { get; private set; } = new();

    //triggers

    public void OnClickCloseAll()
    {
        movementSkillSlot.CloseAll();
        classSkillSlot.CloseAll();
        weaponSlot.CloseAll();
        trinketSlot.CloseAll();
    }



    // Overlay mode: 8_ loaded additively on top of the match during formation prep
    public static bool IsOverlay { get; set; }
    [Header("Overlay")]
    [SerializeField] private GameObject mainMenuButton; // hidden in overlay mode

    void Awake()
    {
        if (!IsOverlay) return;
        // The match scene already owns camera, audio listener and event system
        foreach (var root in gameObject.scene.GetRootGameObjects())
        {
            foreach (var c in root.GetComponentsInChildren<Camera>(true)) c.gameObject.SetActive(false);
            foreach (var l in root.GetComponentsInChildren<AudioListener>(true)) l.enabled = false;
            foreach (var e in root.GetComponentsInChildren<UnityEngine.EventSystems.EventSystem>(true)) e.gameObject.SetActive(false);
        }
        if (mainMenuButton != null) mainMenuButton.SetActive(false);
    }

    void Start()
    {
        unitPrefabRegistry.Init();
        skillLibrary.Init();
        weaponRegistry.Init();
        trinketRegistry.Init();
        effectRegistry.Init();
        classLibrary.Init();
        StartCoroutine(FullInitSequence(loadingScreen));
    }

    // -------------------------------------------------------
    // Init
    // -------------------------------------------------------

    IEnumerator FullInitSequence(LoadingScreen screen)
    {
        screen?.Show();

        yield return FetchPlayerProfile();
        yield return FetchUnitConfig();
        yield return FetchSkillOptions();
        yield return FetchEquipmentOptions();
        PopulateAllSlots();

        screen?.Hide();
    }

    public IEnumerator ReinitEquipment()
    {
        yield return FetchUnitConfig();
        yield return FetchEquipmentOptions();
        weaponSlot?.Refresh();
        trinketSlot?.Refresh();
    }

    /// -------------------------------
    /// Recalculate
    /// -------------------------------
    /// 


    private void RecalculateStats()
    {
        if (CurrentUnit == null) return;
        CurrentStats = new UnitGradeStatsDto
        {
            maxHP = CurrentUnit.gradeStats.maxHP + (CurrentUnit.equippedWeapon != null ? CurrentUnit.equippedWeapon.maxHP : 0)
                    + (CurrentUnit.equippedTrinket != null ? CurrentUnit.equippedTrinket.maxHP : 0),
            maxSkillPoint = CurrentUnit.gradeStats.maxSkillPoint + (CurrentUnit.equippedWeapon != null ? CurrentUnit.equippedWeapon.maxSkillPoint : 0)
                            + (CurrentUnit.equippedTrinket != null ? CurrentUnit.equippedTrinket.maxSkillPoint : 0),
            speed = CurrentUnit.gradeStats.speed + (CurrentUnit.equippedWeapon != null ? CurrentUnit.equippedWeapon.speed : 0)
                    + (CurrentUnit.equippedTrinket != null ? CurrentUnit.equippedTrinket.speed : 0),
            damageMultiplier = CurrentUnit.gradeStats.damageMultiplier + (CurrentUnit.equippedWeapon != null ? CurrentUnit.equippedWeapon.damageMultiplier : 0)
                               + (CurrentUnit.equippedTrinket != null ? CurrentUnit.equippedTrinket.damageMultiplier : 0),
            damageReduction = CurrentUnit.gradeStats.damageReduction + (CurrentUnit.equippedWeapon != null ? CurrentUnit.equippedWeapon.damageReduction : 0)
                               + (CurrentUnit.equippedTrinket != null ? CurrentUnit.equippedTrinket.damageReduction : 0)
        };

        unitHp.text = Loc.Format(LocTables.UnitConfig, LocKeys.UnitConfig.HpFormat, CurrentStats.maxHP);
        unitSP.text = Loc.Format(LocTables.UnitConfig, LocKeys.UnitConfig.SpFormat, CurrentStats.maxSkillPoint);
        unitSpeed.text = Loc.Format(LocTables.UnitConfig, LocKeys.UnitConfig.SpeedFormat, CurrentStats.speed);
        unitDamageMultiplier.text = Loc.Format(LocTables.UnitConfig, LocKeys.UnitConfig.DmgPercentFormat, CurrentStats.damageMultiplier);
        unitDamageReduction.text = Loc.Format(LocTables.UnitConfig, LocKeys.UnitConfig.DmgReductionFormat, CurrentStats.damageReduction);

    }


    // -------------------------------------------------------
    // Fetch
    // -------------------------------------------------------
    IEnumerator FetchPlayerProfile()
    {
        using var request = UnityWebRequest.Get($"{clientBackendConfig.backendUrl}/api/PlayerProfile");
        clientBackendConfig.SetHeaders(request, PlayerSession.Token);
        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            loadingScreen?.Show(Loc.Get(LocTables.UnitConfig, LocKeys.UnitConfig.FailedLoadUnits));
            Debug.LogError($"[UnitList] FetchOwnedUnits: {request.error}");
            yield break;
        }

        var profile = JsonUtility.FromJson<PlayerProfileResponse>(request.downloadHandler.text);
        List<OwnedUnitDto> ownedUnits = profile.ownedUnits != null ? new List<OwnedUnitDto>(profile.ownedUnits) : new List<OwnedUnitDto>();
        CurrentUnitDataWithUnlockedClass = ownedUnits.Find(u => u.ownedUnitId == PlayerSession.SelectedOwnedUnitId);
    }

    IEnumerator FetchUnitConfig()
    {
        using var request = UnityWebRequest.Get(
            $"{clientBackendConfig.backendUrl}/api/PlayerProfile/unit/{PlayerSession.SelectedOwnedUnitId}");
        clientBackendConfig.SetHeaders(request, PlayerSession.Token);
        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError($"[UnitConfig] FetchUnitConfig: {request.error} {request}");
            yield break;
        }
        Debug.Log($"[UnitConfig] FetchUnitConfig: {request.downloadHandler.text}");
        CurrentUnit = JsonUtility.FromJson<UnitConfigDto>(request.downloadHandler.text);
        RecalculateStats();

        var prefab = unitPrefabRegistry.Get(CurrentUnit.uId);
        Sprite portrait = prefab != null ? prefab.GetComponent<ClientUnit>()?.unitImg : null;
        currentUnitContainer.Setup(
                        CurrentUnit.ownedUnitId,
                        CurrentUnit.uId,
                        portrait,
                        unitPrefabRegistry.GetName(CurrentUnit.uId),
                        CurrentUnitDataWithUnlockedClass.unlockedClassIds.Length > 0 ? classLibrary.GetFullFrame(CurrentUnitDataWithUnlockedClass.unlockedClassIds[0]) : null,
                        CurrentUnitDataWithUnlockedClass.unlockedClassIds.Length > 1 ? classLibrary.GetHalfFrame(CurrentUnitDataWithUnlockedClass.unlockedClassIds[1]) : null,
                        false);


        Debug.Log($"[UnitConfig] Loaded unit {CurrentUnit.ownedUnitId} uId={CurrentUnit.uId}");
    }

    IEnumerator FetchSkillOptions()
    {
        if (CurrentUnitDataWithUnlockedClass == null || CurrentUnitDataWithUnlockedClass.unlockedClassIds.Length == 0)
            yield break;

        string body = JsonUtility.ToJson(new ClassIdFilterDto { classIds = CurrentUnitDataWithUnlockedClass.unlockedClassIds.ToList() });
        byte[] bodyBytes = System.Text.Encoding.UTF8.GetBytes(body);
        using (var req = new UnityWebRequest(
            $"{clientBackendConfig.backendUrl}/api/ClassDefinition/movement-skills", "POST"))
        {
            req.uploadHandler = new UploadHandlerRaw(bodyBytes);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            clientBackendConfig.SetHeaders(req, PlayerSession.Token);
            yield return req.SendWebRequest();
            Debug.Log($"[UnitConfig] FetchSkillOptions movement-skills response: {req.downloadHandler.text}");

            if (req.result == UnityWebRequest.Result.Success)
            {
                var wrapper = JsonUtility.FromJson<ClassSkillResultWrapper>(
                    "{\"items\":" + req.downloadHandler.text + "}");
                AvailableMovementSkills = wrapper.items != null ?
                    new List<ClassSkillResultDto>(wrapper.items) : new();
                Debug.Log($"[UnitConfig] Loaded {AvailableMovementSkills.Count} movement skills for classes {string.Join(", ", CurrentUnitDataWithUnlockedClass.unlockedClassIds.ToList())}");
            }
        }

        using (var req = new UnityWebRequest(
            $"{clientBackendConfig.backendUrl}/api/ClassDefinition/class-skills", "POST"))
        {
            req.uploadHandler = new UploadHandlerRaw(bodyBytes);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            clientBackendConfig.SetHeaders(req, PlayerSession.Token);
            yield return req.SendWebRequest();
            Debug.Log($"[UnitConfig] FetchSkillOptions class-skills response: {req.downloadHandler.text}");

            if (req.result == UnityWebRequest.Result.Success)
            {
                var wrapper = JsonUtility.FromJson<ClassSkillResultWrapper>(
                    "{\"items\":" + req.downloadHandler.text + "}");
                AvailableClassSkills = wrapper.items != null ?
                    new List<ClassSkillResultDto>(wrapper.items) : new();
                Debug.Log($"[UnitConfig] Loaded {AvailableClassSkills.Count} class skills for classes {string.Join(", ", CurrentUnitDataWithUnlockedClass.unlockedClassIds.ToList())}");
            }
        }
    }

    IEnumerator FetchEquipmentOptions()
    {
        using (var req = UnityWebRequest.Get($"{clientBackendConfig.backendUrl}/api/WeaponDefinition"))
        {
            clientBackendConfig.SetHeaders(req, PlayerSession.Token);
            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success)
            {
                var wrapper = JsonUtility.FromJson<WeaponDefinitionWrapper>(
                    "{\"items\":" + req.downloadHandler.text + "}");
                WeaponDefinitions = wrapper.items != null ?
                    new List<WeaponDefinitionDto>(wrapper.items) : new();
            }
        }

        using (var req = UnityWebRequest.Get($"{clientBackendConfig.backendUrl}/api/TrinketDefinition"))
        {
            clientBackendConfig.SetHeaders(req, PlayerSession.Token);
            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success)
            {
                var wrapper = JsonUtility.FromJson<TrinketDefinitionWrapper>(
                    "{\"items\":" + req.downloadHandler.text + "}");
                TrinketDefinitions = wrapper.items != null ?
                    new List<TrinketDefinitionDto>(wrapper.items) : new();
            }
        }

        using (var req = UnityWebRequest.Get($"{clientBackendConfig.backendUrl}/api/PlayerProfile"))
        {
            clientBackendConfig.SetHeaders(req, PlayerSession.Token);
            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success)
            {
                var profile = JsonUtility.FromJson<PlayerProfileResponse>(req.downloadHandler.text);

                var equippedWeaponIds = new HashSet<string>();
                var equippedTrinketIds = new HashSet<string>();
                foreach (var u in profile.ownedUnits ?? new OwnedUnitDto[0])
                {
                    if (!string.IsNullOrEmpty(u.equippedOwnedWeaponId))
                        equippedWeaponIds.Add(u.equippedOwnedWeaponId);
                    if (!string.IsNullOrEmpty(u.equippedOwnedTrinketId))
                        equippedTrinketIds.Add(u.equippedOwnedTrinketId);
                }

                AvailableWeapons = new List<OwnedWeaponResultDto>();
                foreach (var w in profile.ownedWeapons ?? new OwnedWeaponDto[0])
                {
                    // Only show weapons whose class is unlocked by this unit
                    var weaponDef = WeaponDefinitions.Find(d => d.weaponId == w.weaponDefinitionId);
                    if (weaponDef == null) continue;
                    if (!CurrentUnitDataWithUnlockedClass.unlockedClassIds.Contains(weaponDef.classId)) continue;

                    AvailableWeapons.Add(new OwnedWeaponResultDto
                    {
                        ownedWeaponId = w.ownedWeaponId,
                        weaponDefinitionId = w.weaponDefinitionId,
                        isEquipped = equippedWeaponIds.Contains(w.ownedWeaponId)
                    });
                }

                AvailableTrinkets = new List<OwnedTrinketResultDto>();
                foreach (var t in profile.ownedTrinkets ?? new OwnedTrinketDto[0])
                    AvailableTrinkets.Add(new OwnedTrinketResultDto
                    {
                        ownedTrinketId = t.ownedTrinketId,
                        trinketDefinitionId = t.trinketDefinitionId,
                        isEquipped = equippedTrinketIds.Contains(t.ownedTrinketId)
                    });
            }
        }

    }

    // -------------------------------------------------------
    // Populate
    // -------------------------------------------------------

    void PopulateAllSlots()
    {
        if (CurrentUnit == null) return;
        movementSkillSlot?.Init(this, SlotConfig.SlotType.MovementSkill);
        classSkillSlot?.Init(this, SlotConfig.SlotType.ClassSkill);
        weaponSlot?.Init(this, SlotConfig.SlotType.Weapon);
        trinketSlot?.Init(this, SlotConfig.SlotType.Trinket);
        ShowUniquePassive();

        OnClickCloseAll();
    }

    // Fills PassivePanel: icon, name, skill description + each effect's description
    void ShowUniquePassive()
    {
        int id = CurrentUnit.passiveSkillId;
        SkillDefinition skill = id > 0 ? skillLibrary.Get(id) : null;

        if (passiveIcon != null)
        {
            passiveIcon.sprite = skill?.icon;
            passiveIcon.enabled = skill?.icon != null;
        }
        if (passiveName != null)
            passiveName.text = skill != null ? skill.skillName : Loc.Get(LocTables.UnitConfig, LocKeys.UnitConfig.Empty);
        if (passiveDescription != null)
        {
            var unit = new UnitData
            {
                MaxHP = CurrentStats?.maxHP ?? 0,
                MaxSkillPoint = CurrentStats?.maxSkillPoint ?? 0,
                Speed = CurrentStats?.speed ?? 0,
                DamageMultiplier = CurrentStats?.damageMultiplier ?? 100,
                DamageReduction = CurrentStats?.damageReduction ?? 0,
            };
            // Skill text, then every effect it carries or grants (each once — see ClientEffectRegistry.Describe)
            string effects = skill != null ? effectRegistry.Describe(skill.effectIds, unit) : "";
            passiveDescription.text = ((skill?.description ?? "") + "\n" + effects).Trim();
        }
    }

    // -------------------------------------------------------
    // Patch
    // -------------------------------------------------------

    public IEnumerator PatchMovementSkill(int skillId)
    {
        yield return PatchRequest("movement-skill",
            JsonUtility.ToJson(new UpdateSkillDto { skillId = skillId }));
        CurrentUnit.equippedMovementSkillId = skillId;
        CurrentUnitDataWithUnlockedClass.equippedMovementSkillId = skillId;
        movementSkillSlot?.Refresh();
    }

    public IEnumerator PatchClassSkill(int skillId)
    {
        yield return PatchRequest("class-skill",
            JsonUtility.ToJson(new UpdateSkillDto { skillId = skillId }));
        CurrentUnit.equippedClassSkillId = skillId;
        CurrentUnitDataWithUnlockedClass.equippedClassSkillId = skillId;
        classSkillSlot?.Refresh();
    }

    public IEnumerator PatchWeapon(string ownedWeaponId)
    {
        yield return PatchRequest("weapon",
            JsonUtility.ToJson(new UpdateWeaponDto { ownedWeaponId = ownedWeaponId }));
        yield return FullInitSequence(null);
    }

    public IEnumerator PatchTrinket(string ownedTrinketId)
    {
        yield return PatchRequest("trinket",
            JsonUtility.ToJson(new UpdateTrinketDto { ownedTrinketId = ownedTrinketId }));
        yield return FullInitSequence(null);
    }

    IEnumerator PatchRequest(string endpoint, string body)
    {
        string url = $"{clientBackendConfig.backendUrl}/api/PlayerProfile/unit/" +
                     $"{PlayerSession.SelectedOwnedUnitId}/{endpoint}";
        using var req = new UnityWebRequest(url, "PATCH");
        req.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(body));
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");
        clientBackendConfig.SetHeaders(req, PlayerSession.Token);
        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
            Debug.LogError($"[UnitConfig] PATCH {endpoint} failed: {req.error}");
    }

    // -------------------------------------------------------
    // Navigation
    // -------------------------------------------------------

    public void OnClickReturnMainMenu()
    {
        if (IsOverlay) { CloseOverlay(); return; }
        UnityEngine.SceneManagement.SceneManager.LoadScene(sceneConfig.mainMenuScene);
    }

    public void OnClickReturnUnitList()
    {
        if (IsOverlay) { CloseOverlay(); return; }
        UnityEngine.SceneManagement.SceneManager.LoadScene(sceneConfig.unitListScene);
    }

    void CloseOverlay() => UnityEngine.SceneManagement.SceneManager.UnloadSceneAsync(gameObject.scene);

    // -------------------------------------------------------
    // DTOs
    // -------------------------------------------------------

    [System.Serializable] public class UpdateSkillDto { public int skillId; }
    [System.Serializable] public class UpdateWeaponDto { public string ownedWeaponId; }
    [System.Serializable] public class UpdateTrinketDto { public string ownedTrinketId; }
    [System.Serializable] public class ClassIdFilterDto { public List<int> classIds; }
    [System.Serializable] private class ClassSkillResultWrapper { public ClassSkillResultDto[] items; }
    [System.Serializable] private class WeaponDefinitionWrapper { public WeaponDefinitionDto[] items; }
    [System.Serializable] private class TrinketDefinitionWrapper { public TrinketDefinitionDto[] items; }
}