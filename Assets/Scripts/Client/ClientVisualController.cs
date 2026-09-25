using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.UI;

public class ClientVisualController : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private ClientMatchSession session;
    [SerializeField] private ClientScene scene;

    [Header("Camera")]
    public ClientCameraController cameraController;


    [Header("UI Elements")]
    [SerializeField] private TextMeshProUGUI instantStatusText;
    [SerializeField] private TextMeshProUGUI instantCounterText;
    [SerializeField] private Image myTimerBar;
    [SerializeField] private Image enemyTimerBar;

    [SerializeField] public GameObject waitButtonLayer;
    [SerializeField] public GameObject cancelButtonLayer;
    [SerializeField] private CanvasGroup confirmButtonCanvasGroup;

    [SerializeField] private Animator leftCommandLogAnimator;

    [SerializeField] public GameObject ActionMenuUI;
    [SerializeField] public TextMeshProUGUI CurrentUnitInfo;
    [SerializeField] public TextMeshProUGUI CurrentUnitSpInfo;
    [SerializeField] public GameObject SpCooldown;

    [SerializeField] public InstantStatus myInstantStatus;
    [SerializeField] public InstantStatus enemyInstantStatus;


    [SerializeField] private CanvasGroup[] OwnedReadyUnitCanvasGroup;
    [SerializeField] private Image[] OwnedReadyUnitIcon;
    [SerializeField] private CanvasGroup[] EnemyReadyUnitCanvasGroup;
    [SerializeField] private Image[] EnemyReadyUnitIcon;




    // -------------------------------------------------------
    // State Change — controls visibility of UI elements
    // -------------------------------------------------------

    public void UpdateReadyUnitBar()
    {
        // foreach (TeamData team in session.teams)
        // {
        //     bool hasUnitReady = false;
        //     foreach (int unitId in team.unitIds)
        //     {
        //         ClientUnit unit = scene.GetSceneUnitById(unitId);
        //         if (unit != null)
        //         {
        //             hasUnitReady = session.isUnitReady(unitId) || hasUnitReady;
        //         }
        //     }
        //     // team.isInstantEnded = false;
        //     team.isInstantEnded = team.isInstantEnded || !hasUnitReady;
        // }

        for (int i = 0; i < OwnedReadyUnitCanvasGroup.Length; i++)
        {
            if (i < session.ownedReadyUnitIds.Count)
            {
                OwnedReadyUnitCanvasGroup[i].alpha = 1;
                OwnedReadyUnitCanvasGroup[i].interactable = true;
                OwnedReadyUnitCanvasGroup[i].blocksRaycasts = true;
                OwnedReadyUnitIcon[i].sprite = scene.GetSceneUnitById(session.ownedReadyUnitIds[i])?.unitImg;
            }
            else
            {
                OwnedReadyUnitCanvasGroup[i].alpha = 0;
                OwnedReadyUnitCanvasGroup[i].interactable = false;
                OwnedReadyUnitCanvasGroup[i].blocksRaycasts = false;
            }
        }
        for (int i = 0; i < EnemyReadyUnitCanvasGroup.Length; i++)
        {
            if (i < session.enemyReadyUnitIds.Count)
            {
                EnemyReadyUnitCanvasGroup[i].alpha = 1;
                EnemyReadyUnitCanvasGroup[i].interactable = true;
                EnemyReadyUnitCanvasGroup[i].blocksRaycasts = true;
                EnemyReadyUnitIcon[i].sprite = scene.GetSceneUnitById(session.enemyReadyUnitIds[i])?.unitImg;
            }
            else
            {
                EnemyReadyUnitCanvasGroup[i].alpha = 0;
                EnemyReadyUnitCanvasGroup[i].interactable = false;
                EnemyReadyUnitCanvasGroup[i].blocksRaycasts = false;
            }
        }


    }

    public void UpdateVisualOnStateChange()
    {
        var state = scene?.clientInteractionSystem?.stateMachine?.currentState;
        ClearRange();
        ClearShadowBrute();
        RedrawRange();
        ShowInspectPanel(false);
        // scene.IsCenteringCell = false;
        scene.visualController.UpdateReadyUnitBar();

        if (state is UnitSelectedState)
        {
            ShowWaitButton(false);
            ShowCancelButton(true);
            ShowActionMenu(true);

            if (session.isTargetLocked)
            {
                Vector3Int x = session.CurrentTargetableCells.Find(cell => cell.x == session.currentPreviewCell.x && cell.y == session.currentPreviewCell.y);
                ShowConfirmButton(x.z == 1 && session.isUnitReady(session.selectedUnitId));
            }
        }
        else if (state is NoneState)
        {
            ShowWaitButton(true);
            ShowCancelButton(false);
            ShowConfirmButton(false);
            ShowActionMenu(false);
            ActionMenuUI.SetActive(false);
        }
        else if (state is LockedInputState)
        {
            ShowWaitButton(false);
            ShowCancelButton(false);
            ShowConfirmButton(false);
            ShowActionMenu(false);
            ActionMenuUI.SetActive(false);
        }
    }

    // -------------------------------------------------------
    // Range Display — dumb redraw from session data
    // -------------------------------------------------------

    public void RedrawRange()
    {
        bool isOwned = session.IsMyUnit(session.selectedUnitId);
        ClearRange();
        if (scene.rangeTilemap == null) return;

        var newTarget = new HashSet<Vector3Int>(session.CurrentTargetableCells);

        scene.rangeTilemap.ClearAllTiles();

        foreach (var cell in newTarget)
        {
            TileBase tile = (isOwned && cell.z == 1) ? scene.mapPrefab.rangeTileBase : scene.mapPrefab.enemyRangeTileBase;
            scene.rangeTilemap.SetTile(cell, tile);
            Debug.Log($"cell[{cell}] is set {(isOwned && cell.z == 1)}");
        }
    }
    public void ClearRange()
    {
        if (scene.rangeTilemap != null)
            scene.rangeTilemap.ClearAllTiles();
    }

    // -------------------------------------------------------
    // Hover Shadow
    // -------------------------------------------------------

    private List<GameObject> activeShadows = new List<GameObject>();

    public void HoverShadow()
    {
        UnitData unit = session.GetUnitDataById(session.selectedUnitId);
        if (unit == null || scene.movableTilemap == null) return;

        var sceneUnit = scene.GetSceneUnitById(session.selectedUnitId);
        if (sceneUnit == null || sceneUnit.hoverUnit == null) return;

        Vector3Int cell = session.isTargetLocked
            ? session.currentPreviewCell
            : session.currentCellMouseOn;

        cell.z = 1;

        // Debug.Log($"Current previewing cell: {cell}");

        if (!session.CurrentTargetableCells.Contains(cell))
        {
            ClearShadowBrute();
            return;
        }
        // Debug.Log($"Cell is in CurrentTagetable cells");

        GameObject shadow = sceneUnit.hoverUnit;
        shadow.SetActive(true);
        if (!activeShadows.Contains(shadow))
            activeShadows.Add(shadow);
        shadow.transform.position = scene.movableTilemap.GetCellCenterWorld(cell);
    }
    public void ClearShadowBrute()
    {
        foreach (var shadow in activeShadows)
            if (shadow != null) shadow.SetActive(false);
        activeShadows.Clear();
    }

    // -------------------------------------------------------
    //Center cell highlight
    // -------------------------------------------------------

    public void HighlightCell(Vector3Int cell)
    {
        scene.IsCenteringCell = true;
        scene.hoverHighlight.SetActive(true);
        scene.hoverHighlight.transform.position = scene.movableTilemap.GetCellCenterWorld(cell);
    }

    // Inspect panel
    public void ShowInspectPanel(bool isShown, SkillDefinition skill = null)
    {
        if (!isShown || skill == null)
        {
            scene.InspectPanel.SetActive(false);
            return;
        }

        UnitData unit = session.GetUnitDataById(session.selectedUnitId);
        if (unit == null) return;
        SkillUsageData[] usageCopy = unit.SkillUsages.Clone() as SkillUsageData[];

        int index = -1;
        for (int i = 0; i < usageCopy.Length; i++)
        {
            if (usageCopy[i].SkillId == skill.skillId)
            {
                index = i;
                break;
            }
        }

        scene.InspectPanel.SetActive(true);
        scene.SkillName.text = skill.skillName;
        scene.EffectType.text = skill.effectIds.Length > 0 ? scene.effectRegistry.Get(skill.effectIds[0]).InstantType.ToString() : "";
        scene.SkillDescription.text = skill.description + " => ";
        foreach (var effectId in skill.effectIds)
        {
            ClientActiveEffectBase effect = scene.effectRegistry.Get(effectId);
            scene.SkillDescription.text += " " + effect.GetDescription(session.GetUnitDataById(session.selectedUnitId), scene.effectRegistry);
        }
        scene.SkillCost.text = Loc.Format(LocTables.ClientMatch, LocKeys.ClientMatch.SpCostFormat, skill.skillPointCost);
        scene.StepMultiplier.text = Loc.Format(
            LocTables.ClientMatch,
            LocKeys.ClientMatch.StepCostFormat,
            skill.stepCostMultiplier * unit.CurrentStepBase,
            skill.stepCostMultiplier);

        string unlimited = Loc.Get(LocTables.ClientMatch, LocKeys.ClientMatch.Unlimited);
        string totalLimit = skill.useLimitTotal > 0 ? usageCopy[index].UsageTotal + "/" + skill.useLimitTotal.ToString() : unlimited;
        string instantLimit = skill.useLimitPerInstant < 0 ? unlimited : (index < 0 ? "0/" + skill.useLimitPerInstant : usageCopy[index].UsageThisInstant + "/" + skill.useLimitPerInstant);
        scene.SkillUsageText.text = Loc.Format(LocTables.ClientMatch, LocKeys.ClientMatch.SkillUsageFormat, instantLimit, totalLimit);
    }

    // -------------------------------------------------------
    // Button Visibility
    // -------------------------------------------------------

    public void ShowWaitButton(bool isShown)
    {
        waitButtonLayer.SetActive(session.IsMyTurn() && isShown);
    }

    public void ShowCancelButton(bool isShown)
    {
        cancelButtonLayer.SetActive(session.IsMyTurn() && isShown);
    }

    public Sprite noSkillIcon;
    public void ShowActionMenu(bool isShown)
    {
        if (isShown)
        {
            ActionMenuUI.SetActive(true);
            UnitData unit = session.GetUnitDataById(session.selectedUnitId);

            scene.mainWeaponSkillSlot?.RefreshIcon();
            scene.classSkillSlot?.RefreshIcon();
            scene.trinketSkillSlot?.RefreshIcon();
            scene.uniquePassiveSlot?.RefreshIcon();

            CurrentUnitInfo.text = "Name: " + scene.unitPrefabRegistry.GetName(unit.UId)
            + "\n HP: " + unit.CurrentHP.ToString() + " / " + unit.MaxHP.ToString()
            + "\n SP: " + SkillPointToPlus(unit.CurrentSkillPoint, unit.MaxSkillPoint)
            + "\n BaseStep: " + unit.CurrentStepBase.ToString() + " => NextStep: " + unit.NextStep.ToString();

            if (unit.CurrentSkillPoint == unit.MaxSkillPoint)
            {
                CurrentUnitSpInfo.text = "";
                SpCooldown.SetActive(false);
            }
            else
            {
                int regen = session.matchConfig.conseRegenCap - unit.ConsecutiveRegenInstants;
                Debug.Log($"regen: {regen} conseRegenCap: {session.matchConfig.conseRegenCap} ConsecutiveRegenInstants: {unit.ConsecutiveRegenInstants}");
                CurrentUnitSpInfo.text = regen.ToString();
                SpCooldown.SetActive(true);
            }
        }
        else
        {
            ActionMenuUI.SetActive(false);
        }
    }

    public string SkillPointToPlus(int sp, int maxSp)
    {
        string result = "";
        for (int i = 0; i < maxSp; i++)
        {
            if (i < sp)
                result += "+";
            else
                result += "_";
        }
        return result;
    }

    public void ShowConfirmButton(bool isShown)
    {
        bool show = session.IsMyTurn() && isShown;
        confirmButtonCanvasGroup.alpha = show ? 1f : 0f;
        confirmButtonCanvasGroup.interactable = show;
        confirmButtonCanvasGroup.blocksRaycasts = show;
    }

    //-------------------------------------------------------
    //CommandLogs display
    //_______________________________________________________
    private string[] prevLogs = new string[0];

    private void ProcessAura(TimelineData timeline)
    {
        if (timeline.timelinelog == null || timeline.timelinelog.Length == 0)
        {
            Debug.LogWarning("[ProcessAura] timelinelog is null or empty.");
            return;
        }

        foreach (ClientUnit clientUnit in scene.spawnedUnits)
            clientUnit.UnitAura.SetActive(false);

        for (int i = timeline.timelinelog.Length - 1; i >= 0; i--)
        {
            string log = timeline.timelinelog[i];
            Debug.Log($"[ProcessAura] log[{i}]: {log}");

            if (log.Contains("paused"))
            {
                Debug.Log($"[ProcessAura] Hit paused at [{i}], stopping.");
                return;
            }

            if (!log.Contains("SID")) continue;

            int searchFrom = 0;
            int unitId = -1;
            int skillId = -1;

            while (searchFrom < log.Length)
            {
                int linkStart = log.IndexOf("\"ID ", searchFrom);
                if (linkStart < 0) break;

                int linkEnd = log.IndexOf('"', linkStart + 1);
                if (linkEnd < 0) break;

                string linkContent = log.Substring(linkStart + 1, linkEnd - linkStart - 1);
                Debug.Log($"[ProcessAura] linkContent: '{linkContent}'");

                if (linkContent.Contains("SID"))
                {
                    string[] parts = linkContent.Split(' ');
                    if (parts.Length >= 4 &&
                        int.TryParse(parts[1], out unitId) &&
                        int.TryParse(parts[3], out skillId))
                    {
                        Debug.Log($"[ProcessAura] Parsed unitId={unitId} skillId={skillId}");
                        break;
                    }
                    else
                        Debug.LogWarning($"[ProcessAura] Failed to parse from '{linkContent}' parts={parts.Length}");
                }

                searchFrom = linkEnd + 1;
            }

            if (unitId < 0 || skillId < 0)
            {
                Debug.LogWarning($"[ProcessAura] Could not parse unitId/skillId from log: {log}");
                continue;
            }

            var skill = scene.skillLibrary.Get(skillId);
            if (skill == null)
            {
                Debug.LogWarning($"[ProcessAura] Skill {skillId} not found.");
                continue;
            }

            bool hasNonInstant = false;
            foreach (var eId in skill.effectIds)
            {
                var effect = scene.effectRegistry.Get(eId);
                if (effect != null && effect.InstantType == InstantType.NonInstant)
                {
                    hasNonInstant = true;
                    break;
                }
            }

            Debug.Log($"[ProcessAura] skillId={skillId} hasNonInstant={hasNonInstant}");

            if (!hasNonInstant) continue;

            var clientUnit = scene.GetSceneUnitById(unitId);
            if (clientUnit == null)
            {
                Debug.LogWarning($"[ProcessAura] ClientUnit {unitId} not found in scene.");
                continue;
            }

            Debug.Log($"[ProcessAura] Activating aura for unit {unitId}.");
            clientUnit.UnitAura?.SetActive(true);
        }
    }

    public IEnumerator ApplyLogs(TimelineData timeline)
    {
        int myTeam = session.GetMyTeam();

        for (int i = 0; i < scene.CommandLogs.Length; i++)
        {
            if (i < timeline.timelinelog.Length)
            {
                string log = timeline.timelinelog[i];
                if (myTeam != -1)
                {
                    log = log.Replace($"Team {myTeam} —", "[Your team]");
                    log = log.Replace($"Team {1 - myTeam} —", "[Enemy team]");
                }
                foreach (var unit in session.units)
                {
                    string name = scene.unitPrefabRegistry.GetName(unit.UId);
                    log = log.Replace($"Unit {unit.UId}", name);
                }
                scene.CommandLogs[i].text = log;
            }
            else
            {
                scene.CommandLogs[i].text = "";
            }
        }

        ProcessAura(timeline);
        bool logsChanged = prevLogs.Length != timeline.timelinelog.Length;
        if (!logsChanged)
        {
            for (int i = 0; i < Mathf.Min(prevLogs.Length, timeline.timelinelog.Length); i++)
            {
                if (!prevLogs[i].Equals(timeline.timelinelog[i]))
                {
                    logsChanged = true;
                }
            }
        }
        string lastLog = timeline.timelinelog.Length > 0 ? timeline.timelinelog[timeline.timelinelog.Length - 1] : "";
        Debug.Log($"[ClientVisualController] last log: {lastLog} Losgs changed: {logsChanged}");
        if (!logsChanged)
        {
            yield break; // no change in logs, skip rest of the sequence
        }

        prevLogs = timeline.timelinelog.Clone() as string[];

        Debug.Log($"Logs changed, checking for state changes...You ended? {session.GetOwnedTeamData().isInstantEnded} Enemy ended? {session.GetEnemyTeamData().isInstantEnded}");

        if (lastLog.Contains("paused"))
        {
            Debug.Log("Timeline paused, playing eye sequence with animation");
            yield return scene.eyesCanvasUI.PlayEyeSequence(!session.GetOwnedTeamData().isInstantEnded, !session.GetEnemyTeamData().isInstantEnded, !session.GetOwnedTeamData().isInstantEnded, !session.GetEnemyTeamData().isInstantEnded, myInstantStatus, enemyInstantStatus);
        }
        else
        {
            int commandedTeam = ExtractTeamFromLog(lastLog);
            if (session.GetTeamDataById(commandedTeam)?.isInstantEnded == true)
            {
                Debug.Log($"Instant ended for team {commandedTeam}, playing eye sequence with animation. Commanded team: {commandedTeam}, My team: {session.GetMyTeam()}");
                if (commandedTeam == session.GetMyTeam())
                    yield return scene.eyesCanvasUI.PlayEyeSequence(!session.GetOwnedTeamData().isInstantEnded, !session.GetEnemyTeamData().isInstantEnded, true, false, myInstantStatus, enemyInstantStatus);
                else
                    yield return scene.eyesCanvasUI.PlayEyeSequence(!session.GetOwnedTeamData().isInstantEnded, !session.GetEnemyTeamData().isInstantEnded, false, true, myInstantStatus, enemyInstantStatus);
            }

        }
    }

    public IEnumerator ShowCommandLog(bool isShow)
    {
        if (isShow)
            leftCommandLogAnimator.Play("ScrollDown", 0, 0f);
        else
            leftCommandLogAnimator.Play("ScrollUp", 0, 0f);
        if (isShow)
            yield return new WaitForSeconds(0.35f);
        foreach (var log in scene.CommandLogs)
        {
            log.gameObject?.SetActive(isShow);
        }
        scene.clientInteractionSystem.isScrolling = false;
    }

    private int ExtractTeamFromLog(string log)
    {
        if (log.StartsWith("Team 0"))
            return 0;
        if (log.StartsWith("Team 1"))
            return 1;
        return -1;
    }

    // -------------------------------------------------------
    // UI Loop — data only, dumb updates every tick
    // -------------------------------------------------------

    public void StartUILoop() => StartCoroutine(UILoop());

    private bool turnFlip = false;

    IEnumerator UILoop()
    {
        while (true)
        {
            if (instantCounterText != null)
                instantCounterText.text = $"{session.Timeline.currentInstant}/{session.Timeline.maxInstant}";

            // Status text
            if (instantStatusText != null)
            {
                if (!session.Timeline.isPaused)
                    instantStatusText.text = Loc.Get(LocTables.ClientMatch, LocKeys.ClientMatch.TimeFlowing);
                else if (session.LastResolve.HasResolve && session.SyncState != SyncStateValue.Idle)
                    instantStatusText.text = Loc.Get(LocTables.ClientMatch, LocKeys.ClientMatch.Resolving);
                else
                    instantStatusText.text = session.IsMyTurn()
                        ? Loc.Get(LocTables.ClientMatch, LocKeys.ClientMatch.YourTurn)
                        : Loc.Get(LocTables.ClientMatch, LocKeys.ClientMatch.EnemyTurn);
            }

            // Turn flip — update button visibility on turn change
            bool isMyTurn = session.IsMyTurn();

            var timerBar = isMyTurn ? myTimerBar : enemyTimerBar;
            var otherBar = isMyTurn ? enemyTimerBar : myTimerBar;

            if (timerBar != null)
            {
                float waitDur = session.waitDur > 0 ? session.waitDur : session.overtimeDur;
                float maxDur = session.waitDur > 0 ? session.maxWaitDur : session.maxOvertimeDur;
                timerBar.color = session.waitDur > 0 ? Color.green : Color.red;
                timerBar.fillAmount = maxDur > 0 ? waitDur / maxDur : 0f;
            }
            if (otherBar != null)
            {
                otherBar.fillAmount = 0;
            }

            if (turnFlip != isMyTurn)
            {
                UpdateVisualOnStateChange();
                turnFlip = isMyTurn;
            }

            // Range + shadow — redraw every tick if unit selected
            if (scene?.clientInteractionSystem?.stateMachine?.currentState is UnitSelectedState)
            {
                // Update confirm button based on target lock
                // RedrawRange();
                if (session.isTargetLocked)
                {
                    Vector3Int x = session.CurrentTargetableCells.Find(cell => cell.x == session.currentPreviewCell.x && cell.y == session.currentPreviewCell.y);
                    ShowConfirmButton(x.z == 1 && session.isUnitReady(session.selectedUnitId));
                }
                else
                {
                    ShowConfirmButton(false);
                }

            }

            yield return new WaitForSeconds(0.1f);
        }
    }
}