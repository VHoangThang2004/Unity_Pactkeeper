using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class ServerMatchSession : MonoBehaviour
{
    public int CurrentTeamTurnId = -1; // -1 : not a turn, time flowing, if a team's turn, this value equals to TeamData.teamId
    public GridMap Map { get; private set; }
    public string MapId { get; private set; }
    public GridMapAsset MapAsset { get; private set; }

    [Header("Config")]
    [SerializeField] private string mapId = "defaultPvpMap";

    [Header("Data")]
    [SerializeField] private MapRegistry mapRegistry;
    [SerializeField] public UnitLibrary unitLibrary;
    [SerializeField] public SkillLibrary skillLibrary;
    [SerializeField] public ServerEffectRegistry effectRegistry;
    public List<EffectBase> GlobalActiveEffects { get; set; } = new List<EffectBase>(); public int[] GlobalActiveEffectIds = new int[0];
    private Dictionary<int, List<EffectBase>> unitActiveEffects = new Dictionary<int, List<EffectBase>>();

    public List<EffectBase> GetUnitActiveEffects(int unitId)
    {
        if (!unitActiveEffects.TryGetValue(unitId, out var list))
        {
            list = new List<EffectBase>();
            unitActiveEffects[unitId] = list;
        }
        return list;
    }

    private List<TeamData> teams;
    public List<UnitData> units;
    // Testing phase — hardcoded loadouts
    // Backend phase: replace this with data received from backend
    private List<TeamLoadout> loadouts = new List<TeamLoadout>
    {
        new TeamLoadout { teamId = 0, clientId = 0, unitUIds = new List<int> { 1, 2 } },
        new TeamLoadout { teamId = 1, clientId = 0, unitUIds = new List<int> { 1, 2 } }
    };

    // Last sent state — always up to date, used for targeted sends and resync
    public SessionSnapshotData LastSnapshot;
    public ResolveData LastResolve;
    public DecisionRequestData LastDecision;

    // Secret data per team — never inside TeamData, never accidentally broadcast
    private SecretData[] secretData = new SecretData[]
{
    new SecretData { Data = string.Empty },
    new SecretData { Data = string.Empty }
};

    public SecretData GetSecretData(int team)
    {
        if (team < 0 || team >= secretData.Length) return default;
        return secretData[team];
    }

    public void SetSecretData(int team, SecretData data)
    {
        if (team < 0 || team >= secretData.Length) return;
        secretData[team] = data;
    }
    // -------------------------------------------------------
    // Timeline State (written by TimelineManager, readable by anyone)
    // -------------------------------------------------------
    public ServerSessionState TimelineState { get; set; } = ServerSessionState.None;
    public int CurrentInstant { get; set; } = 0;
    public int FlaggedTeamId { get; set; } = 0;
    public List<int> ReadyUnitIds { get; set; } = new List<int>();


    // -------------------------------------------------------
    // Init
    // -------------------------------------------------------

    public bool Init()
    {
        mapRegistry.Init();

        MapAsset = mapRegistry.Get(mapId);
        if (MapAsset == null)
        {
            Debug.LogError($"[MatchSession] MapRegistry has no entry for mapId '{mapId}'!");
            return false;
        }

        MapId = mapId;
        Map = new GridMap();
        Map.Init(MapAsset);

        unitLibrary.Init();
        effectRegistry.Init();
        SetTeamExcludeServer();
        return true;
    }

    private void SetTeamExcludeServer()
    {
        teams = new List<TeamData>();
        units = new List<UnitData>();

        // Build TeamData from loadouts — clientId assigned from connected clients for now
        var connectedIds = new List<ulong>(NetworkManager.Singleton.ConnectedClientsIds);
        connectedIds.Remove(NetworkManager.Singleton.LocalClientId); // exclude server

        for (int i = 0; i < loadouts.Count && i < connectedIds.Count; i++)
        {
            loadouts[i].clientId = connectedIds[i]; // testing: assign real clientId here
            teams.Add(new TeamData
            {
                teamId = loadouts[i].teamId,
                clientId = loadouts[i].clientId,
                unitIds = new List<int>()
            });
        }
    }

    // -------------------------------------------------------
    // Team & Player Management
    // -------------------------------------------------------

    public int GetTeamNumberByClientId(ulong clientId)
    {
        return teams[0].clientId == clientId ? 0 :
               teams[1].clientId == clientId ? 1 : -1;
    }

    public TeamData GetCurrentTurnTeamData()
    {
        return teams[0].teamId == CurrentTeamTurnId ? teams[0] :
                teams[1].teamId == CurrentTeamTurnId ? teams[1] : null;
    }

    public TeamData GetTeamDataByTeamId(int teamId)
    {
        return teams[0].teamId == teamId ? teams[0] :
               teams[1].teamId == teamId ? teams[1] : null;
    }
    public TeamLoadout GetTeamLoadoutDataByTeamId(int teamId)
    {
        return loadouts[0].teamId == teamId ? loadouts[0] :
               loadouts[1].teamId == teamId ? loadouts[1] : null;
    }
    public TeamData GetTeamDataByClientId(ulong clientId)
    {
        return teams[0].clientId == clientId ? teams[0] :
               teams[1].clientId == clientId ? teams[1] : null;
    }
    public TeamLoadout GetTeamLoadoutDataByClientId(ulong clientId)
    {
        return loadouts[0].clientId == clientId ? loadouts[0] :
               loadouts[1].clientId == clientId ? loadouts[1] : null;
    }
    public List<TeamData> GetAllTeamData()
    {
        return teams;
    }
    public int GetOtherTeamId(int currentTeamId)
    {
        return teams[0].teamId == currentTeamId ? teams[1].teamId : teams[0].teamId;
    }
    // -------------------------------------------------------
    // Lookup
    // -------------------------------------------------------
    public void AddGlobalEffect(EffectBase effect)
    {
        GlobalActiveEffects.Add(effect);
        SyncGlobalEffectIds();
    }

    public void RemoveGlobalEffect(EffectBase effect)
    {
        GlobalActiveEffects.Remove(effect);
        SyncGlobalEffectIds();
    }

    void SyncGlobalEffectIds()
    {
        var ids = new List<int>();
        foreach (var effect in GlobalActiveEffects)
            ids.Add(effect.effectId);
        GlobalActiveEffectIds = ids.ToArray();
    }
    public int GetTeamIdByUnitId(int unitId)
    {
        foreach (var team in teams)
            if (team.unitIds.Contains(unitId)) return team.teamId;
        return -1;
    }

    public TeamData GetTeamByUnitId(int unitId)
    {
        return teams.Find(t => t.unitIds.Contains(unitId));
    }

    public UnitData GetUnit(int instanceId)
    {
        return units.Find(u => u.Id == instanceId);
    }
    public UnitData GetUnitAt(Vector3Int cell)
    {
        return units.Find(u => u.CurrentCell == cell);
    }
    public List<UnitData> GetUnitsAt(List<Vector3Int> cells)
    {
        var result = new List<UnitData>();
        foreach (var cell in cells)
        {
            var unit = GetUnitAt(cell);
            if (unit != null) result.Add(unit);
        }
        return result;
    }

    public List<UnitData> GetAllUnits()
    {
        return new List<UnitData>(units);
    }

    // -------------------------------------------------------
    // Occupied Cells
    // -------------------------------------------------------

    public HashSet<Vector3Int> GetOccupiedCells(int excludeUnitId)
    {
        var occupied = new HashSet<Vector3Int>();
        foreach (var u in units)
        {
            if (u.Id != excludeUnitId)
                occupied.Add(u.CurrentCell);
        }
        return occupied;
    }

    public void ApplyMove(int unitId, Vector3Int target)
    {
        var unit = GetUnit(unitId);
        if (unit == null) return;
        unit.CurrentCell = target;
    }


    // -------------------------------------------------------
    // Skill usage
    // -------------------------------------------------------

    public bool CanUseSkill(int unitId, int skillId, SkillDefinition skill)
    {
        var unit = GetUnit(unitId);
        if (unit == null) return false;

        foreach (var usage in unit.SkillUsages)
        {
            if (usage.SkillId != skillId) continue;
            if (skill.useLimitPerInstant != -1 && usage.UsageThisInstant >= skill.useLimitPerInstant) return false;
            if (skill.useLimitTotal != -1 && usage.UsageTotal >= skill.useLimitTotal) return false;
            return true;
        }
        return true; // no usage record = never used = allowed
    }

    public void RecordSkillUsage(int unitId, int skillId)
    {
        var unit = GetUnit(unitId);
        if (unit == null) return;

        for (int i = 0; i < unit.SkillUsages.Length; i++)
        {
            if (unit.SkillUsages[i].SkillId == skillId)
            {
                unit.SkillUsages[i].UsageThisInstant++;
                unit.SkillUsages[i].UsageTotal++;
                return;
            }
        }

        // First time using this skill — add new entry
        var list = new List<SkillUsageData>(unit.SkillUsages)
    {
        new SkillUsageData { SkillId = skillId, UsageThisInstant = 1, UsageTotal = 1 }
    };
        unit.SkillUsages = list.ToArray();
    }

    public void ResetInstantSkillUsage()
    {
        foreach (var unit in units)
            for (int i = 0; i < unit.SkillUsages.Length; i++)
                unit.SkillUsages[i].UsageThisInstant = 0;
    }

}