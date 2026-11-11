using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
public class ActiveEffectInstance
{
    public ServerPassiveEffectBase effect; // shared SO — read-only config, never mutated
    public bool isPermanent;
    public int remainingInstants;

    // Per-unit runtime state (passives/buffs keep their state here, never on the SO)
    public int stacks = 1;
    public int counter;
    public bool primed;
}
public class ServerMatchSession : MonoBehaviour
{
    public int CurrentTeamTurnId = -1; // -1 : not a turn, time flowing, if a team's turn, this value equals to TeamData.teamId
    public GridMap Map { get; private set; }
    public string MapId { get; private set; }
    public GridMapAsset MapAsset { get; private set; }

    [Header("Config")]
    [SerializeField] private string mapId = "defaultPvpMap";
    public ServerBackendClient backendClient;
    [SerializeField] public MatchConfig matchConfig;

    [Header("Data")]
    [SerializeField] private MapRegistry mapRegistry;
    [SerializeField] private StoryEncounterRegistry storyEncounterRegistry; // story mode only — null in PvP scenes
    [SerializeField] public SkillLibrary skillLibrary;
    [SerializeField] public ServerEffectRegistry effectRegistry;
    public List<EffectBase> GlobalActiveEffects { get; set; } = new List<EffectBase>(); public int[] GlobalActiveEffectIds = new int[0];
    // private Dictionary<int, List<EffectBase>> unitActiveEffects = new Dictionary<int, List<EffectBase>>();

    private Dictionary<int, List<ActiveEffectInstance>> unitActiveEffects = new();

    public List<ActiveEffectInstance> GetUnitActiveEffects(int unitId)
    {
        if (!unitActiveEffects.TryGetValue(unitId, out var list))
        {
            list = new List<ActiveEffectInstance>();
            unitActiveEffects[unitId] = list;
        }
        return list;
    }

    // The one way to put an effect on a unit. Re-adding refreshes duration and adds a stack (up to maxStacks).
    public ActiveEffectInstance AddEffect(int unitId, ServerPassiveEffectBase effect, bool permanent = false)
    {
        var list = GetUnitActiveEffects(unitId);
        var existing = list.Find(e => e.effect.effectId == effect.effectId);
        if (existing != null)
        {
            if (!existing.isPermanent)
                existing.remainingInstants = System.Math.Max(existing.remainingInstants, effect.durationInstants);
            existing.stacks = System.Math.Min(existing.stacks + 1, effect.maxStacks);
            return existing;
        }

        var instance = new ActiveEffectInstance
        {
            effect = effect,
            isPermanent = permanent || effect.isPermanent,
            remainingInstants = effect.durationInstants,
        };
        list.Add(instance);
        return instance;
    }

    // Removes one effect instance (e.g. a consumed status). Safe inside hooks — PassiveHooks iterates a copy.
    public void RemoveEffect(int unitId, ActiveEffectInstance inst) => GetUnitActiveEffects(unitId).Remove(inst);

    // A unit's unique passive: every passive effect of its PassiveSkillId, attached permanently
    public void ApplyUniquePassive(UnitData unit)
    {
        if (unit.PassiveSkillId <= 0) return; // -1 = no passive (all current loadouts)

        var skill = skillLibrary.Get(unit.PassiveSkillId);
        if (skill?.effectIds == null) return;

        foreach (int id in skill.effectIds)
        {
            if (effectRegistry.Get(id) is ServerPassiveEffectBase passive)
                AddEffect(unit.Id, passive, permanent: true);
            else
                Debug.LogWarning($"[MatchSession] Passive skill {skill.skillId} effect {id} is not a passive effect — skipped.");
        }
    }

    // Client-facing copy of the unit's effects — ids + expiry only, no formulas.
    // TickAllEffects decrements at each instant start, so remaining R now ⇒ removed at start of CurrentInstant + R.
    public void SyncEffectViews(UnitData unit)
    {
        var list = GetUnitActiveEffects(unit.Id);
        var views = new ActiveEffectView[list.Count];
        for (int i = 0; i < list.Count; i++)
            views[i] = new ActiveEffectView
            {
                EffectId = list[i].effect.effectId,
                ExpiresAtInstant = list[i].isPermanent ? -1 : CurrentInstant + list[i].remainingInstants,
                Stacks = list[i].stacks,
            };
        unit.ActiveEffects = views;
    }

    private List<TeamData> teams;
    public List<UnitData> units;
    // Testing phase — hardcoded loadouts
    // Backend phase: replace this with data received from backend
    private List<TeamLoadout> loadouts = new();


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
    public int ConsecutivePassInstants { get; set; } = 0;
    public int FlaggedTeamId { get; set; } = 0;
    public List<int> ReadyUnitIds { get; set; } = new List<int>();

    public int OwnedReadyUnitCount(int teamId)
    {
        int count = 0;
        foreach (var unitId in ReadyUnitIds)
            if (GetTeamIdByUnitId(unitId) == teamId)
                count++;
        return count;
    }
    public void ReloadInstant()
    {
        foreach (var team in teams)
        {
            bool hasUnitReady = false;
            foreach (int unitId in team.unitIds)
            {
                hasUnitReady = ReadyUnitIds.Contains(unitId) || hasUnitReady;
            }
            // team.isInstantEnded = false;
            team.isInstantEnded = !hasUnitReady;
        }
    }


    // -------------------------------------------------------
    // Init
    // -------------------------------------------------------

    public bool Init()
    {
        backendClient = FindAnyObjectByType<ServerBackendClient>();

        if (backendClient.Mode == "story")
        {
            if (!InitStoryLoadouts())
                return false;
        }
        else if (backendClient.LoadoutResponse != null)
        {
            SetMatchData(backendClient.MatchId, backendClient.LoadoutResponse);
        }
        else
        {
            Debug.LogError("[MatchSession] No loadout data from backend — match cannot start correctly!");
        }

        mapRegistry.Init();

        string resolvedMapId = ResolveMapId();
        MapAsset = mapRegistry.Get(resolvedMapId);
        if (MapAsset == null)
        {
            Debug.LogError($"[MatchSession] MapRegistry has no entry for mapId '{resolvedMapId}'!");
            return false;
        }

        MapId = resolvedMapId;
        Map = new GridMap();
        Map.Init(MapAsset);

        skillLibrary.Init();
        effectRegistry.Init();
        SetTeamFromLoadout();
        return true;
    }
    public string ResolveMapId()
    {
        if (backendClient == null) backendClient = FindAnyObjectByType<ServerBackendClient>();

        if (backendClient.Mode == "story")
        {
            var encounter = GetStoryEncounter();
            return encounter != null && !string.IsNullOrEmpty(encounter.mapId) ? encounter.mapId : mapId;
        }
        return !string.IsNullOrEmpty(backendClient.MapId) ? backendClient.MapId : mapId; // inspector value = devmode fallback
    }

    public StoryEncounterConfig GetStoryEncounter()
    {
        if (storyEncounterRegistry == null) return null;
        storyEncounterRegistry.Init();
        return storyEncounterRegistry.Get(backendClient.ChapterId, backendClient.SceneId);
    }

    // -------------------------------------------------------
    // Formation prep (before Init) — only for matches using backend loadouts
    // -------------------------------------------------------

    // teamId -> (uId -> spawn index)
    public readonly Dictionary<int, Dictionary<int, int>> PrepPlacements = new();

    public bool IsLoadoutConfigurable()
    {
        if (backendClient == null) backendClient = FindAnyObjectByType<ServerBackendClient>();
        if (backendClient.LoadoutResponse == null) return false;
        if (backendClient.Mode != "story") return true; // pvp, ranked, friendly
        var encounter = GetStoryEncounter();
        return encounter != null && !encounter.HasPresetPlayerLoadout;
    }

    public int TeamIdForPlayer(string playerId)
    {
        var data = backendClient.LoadoutResponse;
        if (data?.player1 != null && data.player1.playerId == playerId) return 0;
        if (data?.player2 != null && data.player2.playerId == playerId) return 1;
        return -1;
    }

    public void SetPlacement(int teamId, int[] uIds, int[] spawnIdx)
    {
        mapRegistry.Init();
        var map = mapRegistry.Get(ResolveMapId());
        int spawnCount = map?.teamSpawns != null && teamId < map.teamSpawns.Length
            ? map.teamSpawns[teamId].positions?.Length ?? 0 : 0;

        var placement = new Dictionary<int, int>();
        var used = new HashSet<int>();
        for (int i = 0; i < Mathf.Min(uIds.Length, spawnIdx.Length); i++)
        {
            int idx = spawnIdx[i];
            if (idx < 0 || idx >= spawnCount || !used.Add(idx)) continue; // invalid or duplicate cell — falls back at spawn
            placement[uIds[i]] = idx;
        }
        PrepPlacements[teamId] = placement;
    }

    public void SetMatchData(string matchId, MatchLoadoutsResponse data)
    {
        Debug.Log($"[MatchSession] Real match — matchId={matchId}");

        loadouts.Clear();

        if (data.player1 != null)
            loadouts.Add(ConvertToTeamLoadout(0, data.player1));

        if (data.player2 != null)
            loadouts.Add(ConvertToTeamLoadout(1, data.player2));

        Debug.Log($"[MatchSession] Loaded {loadouts.Count} team loadouts from backend.");
    }

    // -------------------------------------------------------
    // Story mode only — team 1 (AI) always from local config,
    // team 0 (player) from local config if preset, else from backend.
    // -------------------------------------------------------
    private bool InitStoryLoadouts()
    {
        if (storyEncounterRegistry == null)
        {
            Debug.LogError("[MatchSession] storyEncounterRegistry not assigned!");
            return false;
        }

        storyEncounterRegistry.Init();
        var encounter = storyEncounterRegistry.Get(backendClient.ChapterId, backendClient.SceneId);
        if (encounter == null)
        {
            Debug.LogError($"[MatchSession] No StoryEncounterConfig for " +
                           $"chapter={backendClient.ChapterId} scene={backendClient.SceneId}!");
            return false;
        }

        loadouts.Clear();

        if (encounter.HasPresetPlayerLoadout)
        {
            string fallbackPlayerId = backendClient.LoadoutResponse?.player1?.playerId ?? "UNKNOWN";
            loadouts.Add(encounter.ToPlayerTeamLoadout(fallbackPlayerId));
            Debug.Log($"[MatchSession] Story player team — preset loadout " +
                      $"({encounter.playerUnits.Count} units) playerId={fallbackPlayerId}");
        }
        else if (backendClient.LoadoutResponse?.player1 != null)
        {
            loadouts.Add(ConvertToTeamLoadout(0, backendClient.LoadoutResponse.player1));
            Debug.Log($"[MatchSession] Story player team — backend loadout " +
                      $"playerId={backendClient.LoadoutResponse.player1.playerId}");
        }
        else
        {
            Debug.LogError("[MatchSession] Story mode — no player loadout available " +
                           "(neither preset config nor backend data found)!");
            return false;
        }

        loadouts.Add(encounter.ToAITeamLoadout());
        Debug.Log($"[MatchSession] Story AI team — {encounter.aiUnits.Count} units, " +
                  $"alwaysWait={encounter.alwaysWait}");

        return true;
    }

    private TeamLoadout ConvertToTeamLoadout(int teamId, PlayerLoadoutData data)
    {
        var units = new List<PlayerUnitLoadout>();
        foreach (var u in data.units)
        {
            bool hasWeapon = u.equippedWeapon != null && u.equippedWeapon.definitionId > 0;
            bool hasTrinket = u.equippedTrinket != null && u.equippedTrinket.definitionId > 0;
            units.Add(new PlayerUnitLoadout
            {
                UId = u.uId,
                PassiveSkillId = u.passiveSkillId,
                MovementSkillId = u.equippedMovementSkillId,
                WeaponSkillId = hasWeapon ? u.equippedWeapon.skillId : -1,
                ClassSkillId = u.equippedClassSkillId,
                TrinketSkillId = hasTrinket ? u.equippedTrinket.skillId : -1,
                MaxHP = u.gradeStats.maxHP
                    + (hasWeapon ? u.equippedWeapon.maxHP : 0)
                    + (hasTrinket ? u.equippedTrinket.maxHP : 0),
                MaxSkillPoint = u.gradeStats.maxSkillPoint
                    + (hasWeapon ? u.equippedWeapon.maxSkillPoint : 0)
                    + (hasTrinket ? u.equippedTrinket.maxSkillPoint : 0),
                Speed = u.gradeStats.speed
                    + (hasWeapon ? u.equippedWeapon.speed : 0)
                    + (hasTrinket ? u.equippedTrinket.speed : 0),
                DamageMultiplier = u.gradeStats.damageMultiplier
                    + (hasWeapon ? u.equippedWeapon.damageMultiplier : 0)
                    + (hasTrinket ? u.equippedTrinket.damageMultiplier : 0),
                DamageReduction = u.gradeStats.damageReduction
                    + (hasWeapon ? u.equippedWeapon.damageReduction : 0)
                    + (hasTrinket ? u.equippedTrinket.damageReduction : 0),
            });
        }

        return new TeamLoadout
        {
            TeamId = teamId,
            ClientId = 0,
            PlayerId = data.playerId,
            Units = units
        };
    }

    public void RebindPlayerClientIfNeeded(ulong clientId)
    {
        if (clientId == NetworkManager.Singleton.LocalClientId) return;
        if (teams == null || teams.Count == 0) return;

        // In story mode (or single-player dev mode), ensure team 0 is bound to the active connected client
        if (backendClient != null && backendClient.Mode == "story")
        {
            if (teams[0].clientId != clientId)
            {
                Debug.Log($"[MatchSession] Rebinding story Team 0 clientId from {teams[0].clientId} to {clientId}");
                teams[0].clientId = clientId;
                if (loadouts != null && loadouts.Count > 0)
                    loadouts[0].ClientId = clientId;
            }
        }
    }

    private bool IsEncounterAtCleanStart(int targetSceneId, StoryEncounterConfig encounter, ServerSpawnManager spawnManager)
    {
        if (backendClient == null || backendClient.SceneId != targetSceneId) return false;
        if (CurrentInstant != 0) return false;
        if (!string.IsNullOrEmpty(encounter.mapId) && MapId != encounter.mapId) return false;
        if (MapAsset == null || teams == null || teams.Count < 2 || units == null) return false;

        if (teams[0].unitIds.Count != encounter.playerUnits.Count) return false;
        if (teams[1].unitIds.Count != encounter.aiUnits.Count) return false;
        if (units.Count != encounter.playerUnits.Count + encounter.aiUnits.Count) return false;

        for (int i = 0; i < encounter.playerUnits.Count; i++)
        {
            var expected = encounter.playerUnits[i];
            int targetId = teams[0].unitIds[i];
            var unit = units.Find(u => u.Id == targetId);
            if (unit == null || unit.UId != expected.uId) return false;
            if (unit.CurrentHP != expected.GetStartingHP() || unit.CurrentSkillPoint != expected.GetStartingSP()) return false;
            Vector2Int spawnPt = MapAsset.GetSpawn(0, i);
            if (unit.CurrentCell.x != spawnPt.x || unit.CurrentCell.y != spawnPt.y) return false;
        }

        for (int i = 0; i < encounter.aiUnits.Count; i++)
        {
            var expected = encounter.aiUnits[i];
            int targetId = teams[1].unitIds[i];
            var unit = units.Find(u => u.Id == targetId);
            if (unit == null || unit.UId != expected.uId) return false;
            if (unit.CurrentHP != expected.GetStartingHP() || unit.CurrentSkillPoint != expected.GetStartingSP()) return false;
            Vector2Int spawnPt = MapAsset.GetSpawn(1, i);
            if (unit.CurrentCell.x != spawnPt.x || unit.CurrentCell.y != spawnPt.y) return false;
        }

        return true;
    }

    public bool AdvanceTutorialPhase(int newSceneId, ServerSpawnManager spawnManager, bool forceCleanReset = false)
    {
        if (backendClient == null)
            backendClient = FindAnyObjectByType<ServerBackendClient>();

        if (storyEncounterRegistry == null) return false;
        storyEncounterRegistry.Init();

        int chapterId = backendClient != null ? backendClient.ChapterId : 0;
        var newEncounter = storyEncounterRegistry.Get(chapterId, newSceneId);
        if (newEncounter == null)
        {
            Debug.LogWarning($"[MatchSession] No StoryEncounterConfig found for Chapter {chapterId} Scene {newSceneId}.");
            return false;
        }

        if (!forceCleanReset && IsEncounterAtCleanStart(newSceneId, newEncounter, spawnManager))
        {
            Debug.Log($"[MatchSession] SceneId {newSceneId} is already at a clean initial state — skipping reset.");
            return false;
        }

        Debug.Log($"[MatchSession] Resetting story battle board to Chapter {chapterId} SceneId {newSceneId} (clean state).");

        if (backendClient != null)
            backendClient.SceneId = newSceneId;

        // 1. Rebuild loadouts from the target scene's StoryEncounterConfig (removes any units from other scenes like Paladin in C0_S1)
        if (!InitStoryLoadouts())
            return false;

        // 2. Reload map if the scene uses a different mapId
        mapRegistry.Init();
        string resolvedMapId = ResolveMapId();
        var targetMapAsset = mapRegistry.Get(resolvedMapId);
        if (targetMapAsset != null)
        {
            MapId = resolvedMapId;
            MapAsset = targetMapAsset;
            Map = new GridMap();
            Map.Init(MapAsset);
        }

        // 3. Clear old units & teams and rebind to currently connected client
        SetTeamFromLoadout();

        // 4. Spawn fresh units at initial spawn positions with 100% MaxHP, 0 SP, and initial step
        spawnManager.SpawnAllForTutorialReset();

        // 5. Populate ReadyUnitIds for all units starting with CurrentStep == 0
        ReadyUnitIds.Clear();
        foreach (var unit in units)
        {
            if (unit.CurrentStep == 0 && !ReadyUnitIds.Contains(unit.Id))
            {
                ReadyUnitIds.Add(unit.Id);
            }
        }

        return true;
    }

    // Sentinel — never matches a real connected NGO client.
    // AI decisions are made in-process (ServerAIController), never via RPC,
    // so this clientId is never used for an actual network lookup.
    public const ulong AI_CLIENT_ID = ulong.MaxValue;

    // Sentinel for a team whose player isn't connected at Init. Bound later by
    // ServerConnectionManager.OnClientConnected when that player (re)connects; if they never do,
    // ServerController's client-init timeout cancels the match.
    public const ulong NO_CLIENT_ID = ulong.MaxValue - 1;

    private void SetTeamFromLoadout()
    {
        teams = new List<TeamData>();
        units = new List<UnitData>();

        var unassigned = new List<ulong>(NetworkManager.Singleton.ConnectedClientsIds);
        unassigned.Remove(NetworkManager.Singleton.LocalClientId);

        foreach (var loadout in loadouts)
        {
            ulong assignedClientId = loadout.PlayerId == "AI"
                ? AI_CLIENT_ID
                : FindClientIdForPlayerId(loadout.PlayerId, unassigned);

            unassigned.Remove(assignedClientId);

            loadout.ClientId = assignedClientId;
            teams.Add(new TeamData
            {
                teamId = loadout.TeamId,
                clientId = assignedClientId,
                playerId = loadout.PlayerId,
                unitIds = new List<int>()
            });

            Debug.Log($"[MatchSession] Team {loadout.TeamId} → clientId={assignedClientId} playerId={loadout.PlayerId}");
        }
    }

    private ulong FindClientIdForPlayerId(string playerId, List<ulong> candidates)
    {
        if (!string.IsNullOrEmpty(playerId))
        {
            foreach (var clientId in candidates)
            {
                if (MatchIdentityRegistry.GetPlayerId(clientId) == playerId)
                    return clientId;
            }
            // Never hand the team to someone else's connection (or to clientId 0 = the server itself)
            Debug.LogWarning($"[MatchSession] No client connected for playerId={playerId} — team left unbound until they connect.");
            return NO_CLIENT_ID;
        }
        // No playerId (local preset / dev): take whoever is connected
        return candidates.Count > 0 ? candidates[0] : NO_CLIENT_ID;
    }

    // -------------------------------------------------------
    // Team & Player Management
    // -------------------------------------------------------

    public int GetOppositeTeamId(int currentTeamId)
    {
        foreach (TeamData team in teams)
        {
            if (team.teamId != currentTeamId)
            {
                return team.teamId;
            }
        }
        return -1;
    }
    public TeamData GetTeamByPlayerId(string playerId)
    {
        return teams.Find(t => t.playerId == playerId);
    }

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
        return loadouts[0].TeamId == teamId ? loadouts[0] :
               loadouts[1].TeamId == teamId ? loadouts[1] : null;
    }
    public TeamData GetTeamDataByClientId(ulong clientId)
    {
        return teams[0].clientId == clientId ? teams[0] :
               teams[1].clientId == clientId ? teams[1] : null;
    }
    public TeamLoadout GetTeamLoadoutDataByClientId(ulong clientId)
    {
        return loadouts[0].ClientId == clientId ? loadouts[0] :
               loadouts[1].ClientId == clientId ? loadouts[1] : null;
    }
    public List<TeamData> GetAllTeamData()
    {
        return teams;
    }
    public List<TeamData> GetAllCopyTeamData()
    {
        var result = new List<TeamData>();
        foreach (var team in teams)
            result.Add(new TeamData
            {
                teamId = team.teamId,
                clientId = team.clientId,
                unitIds = new List<int>(team.unitIds),
                WaitDuration = team.WaitDuration,
                Overtime = team.Overtime,
                isInstantEnded = team.isInstantEnded
            });
        return result;
    }
    public int GetOtherTeamId(int currentTeamId)
    {
        return teams[0].teamId == currentTeamId ? teams[1].teamId : teams[0].teamId;
    }
    // -------------------------------------------------------
    // Server logs
    // -------------------------------------------------------
    private List<string> timelineLog = new List<string>();

    public void AddLog(string entry)
    {
        timelineLog.Add(entry);
        if (timelineLog.Count > matchConfig.maxLogCount)
            timelineLog.RemoveAt(0);
    }

    public string[] GetTimelineLog() => timelineLog.ToArray();
    public void ClearLogs() => timelineLog.Clear();
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

    public UnitData GetUnitByUnitId(int unitId)
    {
        return units.Find(u => u.Id == unitId);
    }
    public UnitData GetUnitAt(Vector3Int cell)
    {
        return units.Find(u => u.CurrentCell.x == cell.x && u.CurrentCell.y == cell.y);
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
    public List<UnitData> GetAllCopyUnits()
    {
        var result = new List<UnitData>();
        foreach (var unit in units)
            result.Add(unit.Clone());
        return result;
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

    // -------------------------------------------------------
    // Skill usage
    // -------------------------------------------------------

    public bool CanUseSkill(int unitId, int skillId, SkillDefinition skill)
    {
        var unit = GetUnitByUnitId(unitId);
        if (unit == null) return false;
        // Check skill point cost
        if (unit.CurrentSkillPoint < skill.skillPointCost) return false;

        foreach (var usage in unit.SkillUsages)
        {
            if (usage.SkillId != skillId) continue;
            if (skill.useLimitPerInstant != -1 && usage.UsageThisInstant >= skill.useLimitPerInstant) return false;
            if (skill.useLimitTotal != -1 && usage.UsageTotal >= skill.useLimitTotal) return false;
            return true;
        }
        return true; // no usage record = never used = allowed
    }
    public bool IsValidSkillTarget(UnitData unit, int skillId, Vector3Int target)
    {
        foreach (var p in unit.SkillPatterns)
        {
            if (p.SkillId != skillId) continue;
            if (p.TargetCells == null) return false;
            foreach (var c in p.TargetCells)
                if (c.x == target.x && c.y == target.y) return c.z == 1;
            return false; // cell not in pattern
        }
        return false; // skill not in unit's active slots
    }

    public void RecordSkillUsage(int unitId, int skillId)
    {
        var unit = GetUnitByUnitId(unitId);
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

    // 
    public void RemoveUnit(int unitId)
    {
        units.RemoveAll(u => u.Id == unitId);
        ReadyUnitIds.Remove(unitId);
        unitActiveEffects.Remove(unitId); // unit ids are never reused, so this is the only cleanup point

        foreach (var team in teams)
            team.unitIds.Remove(unitId);

        Debug.Log($"[Session] Unit {unitId} removed — dead.");
    }

    public void Kill(int unitId)
    {
        RemoveUnit(unitId);
        UnitRecalculator.RecalculateAll(this);
        Debug.Log($"[Timeline] Unit {unitId} killed.");

    }

    // The one full-state builder: broadcasts (ServerTimelineManager.GetSnapshot) and kill results both use it
    public SessionSnapshotData FullSnapshot()
    {
        return SessionSnapshotData.Full(
            MapId ?? string.Empty,
            CurrentTeamTurnId,
            GetAllCopyTeamData(),
            GetAllCopyUnits(),
            new TimelineData
            {
                currentInstant = CurrentInstant,
                maxInstant = matchConfig.maxInstant,
                flag = FlaggedTeamId,
                isPaused = TimelineState != ServerSessionState.Flowing,
                consecutivePassInstants = ConsecutivePassInstants,
                timelinelog = GetTimelineLog()
            });
    }
}