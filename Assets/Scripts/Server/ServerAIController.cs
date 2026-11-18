using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ServerAIController : MonoBehaviour
{
    [SerializeField] private ServerTimelineManager timeline;
    [SerializeField] private ServerMatchSession session;
    [SerializeField] private StoryEncounterConfig encounterConfig;

    private const int AI_TEAM_ID = 1;
    private const int MAX_ACTIONS_PER_INSTANT = 12;

    private Coroutine watchCoroutine;

    // Per-Instant action budgets (Anti-Infinite-Loop Safeguard)
    private int _budgetInstant = -1;
    private int _actionsThisInstant = 0;
    private int _lastAttemptedToken = -1;
    private readonly Dictionary<int, int> _movesUsed = new();
    private readonly Dictionary<int, int> _utilityUsed = new();
    private readonly Dictionary<int, int> _nonInstantUsed = new();
    private readonly HashSet<int> _executedScriptedIndices = new();

    public void StartAI()
    {
        RefreshEncounterConfig();
        watchCoroutine = StartCoroutine(WatchForTurn());
        Debug.Log("[ServerAI] Started watching for AI team's turn.");
    }

    public void StopAI()
    {
        if (watchCoroutine != null)
            StopCoroutine(watchCoroutine);
    }

    private void RefreshEncounterConfig()
    {
        if (session == null) return;
        var dynamicEncounter = session.GetStoryEncounter();
        if (dynamicEncounter != null && dynamicEncounter != encounterConfig)
        {
            encounterConfig = dynamicEncounter;
            _executedScriptedIndices.Clear();
            _budgetInstant = -1;
            _lastAttemptedToken = -1;
        }
    }

    IEnumerator WatchForTurn()
    {
        while (true)
        {
            yield return null;

            if (timeline == null || session == null) continue;
            if (!timeline.IsWaitingForDecision) continue;
            if (timeline.CurrentDecisionTeamId != AI_TEAM_ID) continue;

            // Refresh encounter if tutorial phase switched dynamically
            RefreshEncounterConfig();
            MakeDecision();
        }
    }

    private void ResetBudgetIfNeeded()
    {
        if (session.CurrentInstant == _budgetInstant)
            return;

        if (session.CurrentInstant < _budgetInstant)
        {
            _executedScriptedIndices.Clear();
            _lastAttemptedToken = -1;
        }

        _budgetInstant = session.CurrentInstant;
        _actionsThisInstant = 0;
        _movesUsed.Clear();
        _utilityUsed.Clear();
        _nonInstantUsed.Clear();
    }

    void MakeDecision()
    {
        ResetBudgetIfNeeded();

        // Guard against duplicate evaluation on the exact same server sync token
        if (_lastAttemptedToken == timeline.CurrentToken)
        {
            SendWait();
            return;
        }
        _lastAttemptedToken = timeline.CurrentToken;

        StoryAIMode mode = encounterConfig != null
            ? encounterConfig.GetEffectiveAIMode()
            : StoryAIMode.AlwaysWait;

        if (mode == StoryAIMode.AlwaysWait)
        {
            SendWait();
            return;
        }

        if (_actionsThisInstant >= MAX_ACTIONS_PER_INSTANT)
        {
            Debug.LogWarning($"[ServerAI] Hit max actions budget ({MAX_ACTIONS_PER_INSTANT}) at Instant {session.CurrentInstant} — forcing Wait.");
            SendWait();
            return;
        }

        // Phase 1 (Hybrid): Check if a scripted action applies at this Instant
        if (mode == StoryAIMode.ScriptedThenTactical && TryExecuteScriptedAction())
            return;

        // If still inside scripted opening window without a matching action, wait cleanly unless past last scripted instant
        if (mode == StoryAIMode.ScriptedThenTactical && IsStillInScriptedOpeningWindow())
        {
            SendWait();
            return;
        }

        // Phase 2 / Tactical Mode: Score best valid action among ready AI units
        if (TryExecuteTacticalAction())
            return;

        SendWait();
    }

    private bool IsStillInScriptedOpeningWindow()
    {
        if (encounterConfig?.scriptedActions == null || encounterConfig.scriptedActions.Count == 0)
            return false;

        int maxScriptedInstant = -1;
        for (int i = 0; i < encounterConfig.scriptedActions.Count; i++)
        {
            if (encounterConfig.scriptedActions[i].instantIndex > maxScriptedInstant)
                maxScriptedInstant = encounterConfig.scriptedActions[i].instantIndex;
        }

        // Hold scripted control through the end of maxScriptedInstant so Tactical AI never injects extra moves into a scripted Instant
        return session.CurrentInstant <= maxScriptedInstant;
    }

    private bool TryExecuteScriptedAction()
    {
        if (encounterConfig?.scriptedActions == null || encounterConfig.scriptedActions.Count == 0)
            return false;

        var aiTeam = session.GetTeamDataByTeamId(AI_TEAM_ID);
        if (aiTeam == null) return false;

        for (int i = 0; i < encounterConfig.scriptedActions.Count; i++)
        {
            if (_executedScriptedIndices.Contains(i)) continue;
            var step = encounterConfig.scriptedActions[i];
            if (step.instantIndex != session.CurrentInstant) continue;

            _executedScriptedIndices.Add(i);

            if (step.skillSlot == SkillSlot.None)
            {
                SendWait();
                return true;
            }

            UnitData targetUnit = ResolveReadyAIUnitForSlot(aiTeam, step.unitSlotIndex);
            if (targetUnit == null) continue;

            int skillId = GetSkillIdForSlot(targetUnit, step.skillSlot);
            if (skillId <= 0) continue;

            SkillDefinition skillDef = session.skillLibrary?.Get(skillId);
            if (skillDef == null || !session.CanUseSkill(targetUnit.Id, skillId, skillDef))
                continue;

            Vector3Int chosenCell;
            if (step.hasCustomTarget)
            {
                chosenCell = new Vector3Int(step.customTargetCell.x, step.customTargetCell.y, 0);
                if (!session.IsValidSkillTarget(targetUnit, skillId, chosenCell))
                    continue;
            }
            else
            {
                if (!TryFindBestTargetForSkill(targetUnit, skillDef, out chosenCell, out float score) || score <= float.MinValue / 2f)
                    continue;
            }

            SubmitAction(targetUnit.Id, chosenCell, skillDef);
            return true;
        }

        return false;
    }

    private UnitData ResolveReadyAIUnitForSlot(TeamData aiTeam, int unitSlotIndex)
    {
        if (unitSlotIndex >= 0 && unitSlotIndex < aiTeam.unitIds.Count)
        {
            int unitId = aiTeam.unitIds[unitSlotIndex];
            if (session.ReadyUnitIds.Contains(unitId))
                return session.GetUnitByUnitId(unitId);
        }

        foreach (int id in aiTeam.unitIds)
        {
            if (session.ReadyUnitIds.Contains(id))
                return session.GetUnitByUnitId(id);
        }

        return null;
    }

    private static int GetSkillIdForSlot(UnitData unit, SkillSlot slot) => slot switch
    {
        SkillSlot.Movement => unit.MovementSkillId,
        SkillSlot.Weapon => unit.WeaponSkillId,
        SkillSlot.Class => unit.ClassSkillId,
        SkillSlot.Trinket => unit.TrinketSkillId,
        _ => -1
    };

    private bool TryExecuteTacticalAction()
    {
        var aiTeam = session.GetTeamDataByTeamId(AI_TEAM_ID);
        if (aiTeam == null || session.skillLibrary == null || session.effectRegistry == null)
            return false;

        int bestUnitId = -1;
        SkillDefinition bestSkill = null;
        Vector3Int bestTarget = default;
        float bestScore = 0f;

        foreach (int unitId in aiTeam.unitIds)
        {
            if (!session.ReadyUnitIds.Contains(unitId)) continue;
            var unit = session.GetUnitByUnitId(unitId);
            if (unit == null) continue;

            // Evaluate Weapon, Class, Trinket, and Movement safely (ignoring -1 slots)
            EvaluateSkillSlot(unit, unit.WeaponSkillId, SkillSlot.Weapon, ref bestScore, ref bestUnitId, ref bestSkill, ref bestTarget);
            EvaluateSkillSlot(unit, unit.ClassSkillId, SkillSlot.Class, ref bestScore, ref bestUnitId, ref bestSkill, ref bestTarget);
            EvaluateSkillSlot(unit, unit.TrinketSkillId, SkillSlot.Trinket, ref bestScore, ref bestUnitId, ref bestSkill, ref bestTarget);
            EvaluateSkillSlot(unit, unit.MovementSkillId, SkillSlot.Movement, ref bestScore, ref bestUnitId, ref bestSkill, ref bestTarget);
        }

        if (bestUnitId != -1 && bestSkill != null && bestScore > 0f)
        {
            SubmitAction(bestUnitId, bestTarget, bestSkill);
            return true;
        }

        return false;
    }

    private void EvaluateSkillSlot(
        UnitData unit,
        int skillId,
        SkillSlot slot,
        ref float bestScore,
        ref int bestUnitId,
        ref SkillDefinition bestSkill,
        ref Vector3Int bestTarget)
    {
        if (skillId <= 0) return; // Safe skip for -1 / unequipped slots

        if (!IsWithinUnitBudget(unit.Id, slot))
            return;

        SkillDefinition skill = session.skillLibrary.Get(skillId);
        if (skill == null || !session.CanUseSkill(unit.Id, skillId, skill))
            return;

        if (TryFindBestTargetForSkill(unit, skill, out Vector3Int candidateCell, out float score) && score > bestScore)
        {
            bestScore = score;
            bestUnitId = unit.Id;
            bestSkill = skill;
            bestTarget = candidateCell;
        }
    }

    private bool IsWithinUnitBudget(int unitId, SkillSlot slot)
    {
        switch (slot)
        {
            case SkillSlot.Movement:
                return !_movesUsed.TryGetValue(unitId, out int moves) || moves < 1;
            case SkillSlot.Weapon:
                return !_nonInstantUsed.TryGetValue(unitId, out int attacks) || attacks < 1;
            case SkillSlot.Class:
            case SkillSlot.Trinket:
                return !_utilityUsed.TryGetValue(unitId, out int utils) || utils < 1;
            default:
                return false;
        }
    }

    private bool TryFindBestTargetForSkill(UnitData unit, SkillDefinition skill, out Vector3Int bestCell, out float bestScore)
    {
        bestCell = default;
        bestScore = float.MinValue;

        if (unit.SkillPatterns == null)
            return false;

        Vector3Int[] validCells = null;
        for (int i = 0; i < unit.SkillPatterns.Length; i++)
        {
            if (unit.SkillPatterns[i].SkillId == skill.skillId)
            {
                validCells = unit.SkillPatterns[i].TargetCells;
                break;
            }
        }

        if (validCells == null || validCells.Length == 0)
            return false;

        bool foundAny = false;
        for (int i = 0; i < validCells.Length; i++)
        {
            Vector3Int cell = validCells[i];
            if (cell.z != 1) continue; // Only selectable cells

            Vector3Int cleanCell = new Vector3Int(cell.x, cell.y, 0);
            if (!session.IsValidSkillTarget(unit, skill.skillId, cleanCell))
                continue;

            float score = ScoreCandidateCell(unit, skill, cleanCell);
            if (!foundAny || score > bestScore)
            {
                foundAny = true;
                bestScore = score;
                bestCell = cleanCell;
            }
        }

        return foundAny;
    }

    private float ScoreCandidateCell(UnitData unit, SkillDefinition skill, Vector3Int targetCell)
    {
        if (skill.effectIds == null || skill.effectIds.Length == 0)
            return -1f;

        var effect = session.effectRegistry.Get(skill.effectIds[0]);
        if (effect == null)
            return -1f;

        AIBehaviorProfile profile = encounterConfig != null
            ? encounterConfig.behaviorProfile
            : AIBehaviorProfile.Balanced;

        // 1. Movement scoring
        if (effect is MoveEffectServer)
            return ScoreMoveCell(unit, targetCell, profile);

        // 2. Damage / Attack scoring
        if (effect is DamageEffectServerBase dmgEffect)
            return ScoreAttackCell(unit, skill, dmgEffect, targetCell, profile);

        // 3. Healing scoring
        if (effect is NormalHealEffectServer || effect is TriageEffectServer)
            return ScoreHealCell(unit, effect, targetCell, profile);

        // 4. Guard / Swap / Pull / Push / Buffs
        if (effect is GuardEffectServer)
        {
            var ally = session.GetUnitAt(targetCell);
            if (ally == null || ally.Id == unit.Id) return -1f;
            return ally.CurrentHP < unit.CurrentHP && ally.CurrentHP <= 2 ? 45f : -1f;
        }

        if (effect is PullEffectServer || effect is PushEffectServer)
        {
            var hit = session.GetUnitAt(targetCell);
            if (hit != null && session.GetTeamIdByUnitId(hit.Id) != AI_TEAM_ID)
                return 35f;
            return -1f;
        }

        if (effect is ApplyBuffEffectServer)
        {
            // Only self-buff if an enemy is reasonably close and we have SP to spare
            int minDist = GetNearestEnemyDistance(unit.CurrentCell);
            return minDist <= 3 ? 25f : -1f;
        }

        return -1f;
    }

    private float ScoreAttackCell(UnitData unit, SkillDefinition skill, DamageEffectServerBase dmgEffect, Vector3Int targetCell, AIBehaviorProfile profile)
    {
        List<Vector3Int> affected = dmgEffect.aoePattern != null
            ? PatternResolver.TranslateAoEPattern(dmgEffect.aoePattern, unit.CurrentCell, targetCell)
            : new List<Vector3Int> { targetCell };
        if (affected == null || affected.Count == 0)
            affected = new List<Vector3Int> { targetCell };

        float score = 0f;
        int enemiesHit = 0;

        for (int i = 0; i < affected.Count; i++)
        {
            var targetUnit = session.GetUnitAt(affected[i]);
            if (targetUnit == null) continue;

            int teamId = session.GetTeamIdByUnitId(targetUnit.Id);
            if (teamId == AI_TEAM_ID)
            {
                score -= 60f; // Avoid friendly fire
                continue;
            }

            enemiesHit++;
            int estDamage = Mathf.Max(1, Mathf.RoundToInt(dmgEffect.baseValue * (unit.DamageMultiplier / 100f)));
            // Assassin Ambush bonus when attacking downward
            if (unit.PassiveSkillId == 5001 && affected[i].y < unit.CurrentCell.y)
                estDamage *= 2;

            score += 60f + estDamage * 15f;
            if (targetUnit.CurrentHP <= estDamage)
                score += 120f; // Lethal bonus

            // Prefer weakening already damaged enemies
            score += (targetUnit.MaxHP - targetUnit.CurrentHP) * 10f;
        }

        if (enemiesHit == 0)
            return -10f;

        if (profile == AIBehaviorProfile.Aggressive)
            score *= 1.15f;

        return score - skill.skillPointCost * 5f;
    }

    private float ScoreHealCell(UnitData unit, EffectBase effect, Vector3Int targetCell, AIBehaviorProfile profile)
    {
        List<Vector3Int> affected = effect.aoePattern != null
            ? PatternResolver.TranslateAoEPattern(effect.aoePattern, unit.CurrentCell, targetCell)
            : new List<Vector3Int> { targetCell };
        if (affected == null || affected.Count == 0)
            affected = new List<Vector3Int> { targetCell };

        float score = 0f;
        for (int i = 0; i < affected.Count; i++)
        {
            var ally = session.GetUnitAt(affected[i]);
            if (ally == null || session.GetTeamIdByUnitId(ally.Id) != AI_TEAM_ID) continue;
            int missing = ally.MaxHP - ally.CurrentHP;
            if (missing > 0)
                score += missing * 35f + (ally.CurrentHP == 1 ? 50f : 0f);
        }

        if (score <= 0f) return -5f;
        return profile == AIBehaviorProfile.Support ? score * 1.3f : score;
    }

    private float ScoreMoveCell(UnitData unit, Vector3Int destCell, AIBehaviorProfile profile)
    {
        int currentDist = GetNearestEnemyDistance(unit.CurrentCell);
        int nextDist = GetNearestEnemyDistance(destCell);
        if (currentDist == int.MaxValue || nextDist == int.MaxValue)
            return -1f;

        // If unit can already attack an enemy from its current position, don't waste an action moving needlessly
        if (HasValidAttackTargetRightNow(unit))
            return -5f;

        float score = 0f;

        if (profile == AIBehaviorProfile.Defensive)
        {
            // Keep safe range (~2-3 cells) rather than rushing into melee
            int idealDist = unit.UId == 4 ? 3 : 2;
            score = (Mathf.Abs(currentDist - idealDist) - Mathf.Abs(nextDist - idealDist)) * 18f;
        }
        else
        {
            // Advance toward nearest player unit
            score = (currentDist - nextDist) * 20f;
        }

        // Assassin prefers landing above an enemy (higher Y) to trigger Ambush next
        if (unit.PassiveSkillId == 5001)
        {
            var nearest = GetNearestEnemyUnit(destCell);
            if (nearest != null && destCell.y > nearest.CurrentCell.y)
                score += 12f;
        }

        // Slight tie-breaker so moving closer beats standing idle when out of range
        return score > 0f ? score + 5f : -1f;
    }

    private bool HasValidAttackTargetRightNow(UnitData unit)
    {
        if (unit.WeaponSkillId <= 0 || unit.SkillPatterns == null) return false;
        for (int i = 0; i < unit.SkillPatterns.Length; i++)
        {
            if (unit.SkillPatterns[i].SkillId != unit.WeaponSkillId) continue;
            var cells = unit.SkillPatterns[i].TargetCells;
            if (cells == null) break;
            for (int c = 0; c < cells.Length; c++)
            {
                if (cells[c].z != 1) continue;
                var occ = session.GetUnitAt(new Vector3Int(cells[c].x, cells[c].y, 0));
                if (occ != null && session.GetTeamIdByUnitId(occ.Id) != AI_TEAM_ID)
                    return true;
            }
        }
        return false;
    }

    private int GetNearestEnemyDistance(Vector3Int fromCell)
    {
        var nearest = GetNearestEnemyUnit(fromCell);
        if (nearest == null) return int.MaxValue;
        return Mathf.Abs(nearest.CurrentCell.x - fromCell.x) + Mathf.Abs(nearest.CurrentCell.y - fromCell.y);
    }

    private UnitData GetNearestEnemyUnit(Vector3Int fromCell)
    {
        var playerTeam = session.GetTeamDataByTeamId(0);
        if (playerTeam == null) return null;

        UnitData best = null;
        int bestDist = int.MaxValue;
        for (int i = 0; i < playerTeam.unitIds.Count; i++)
        {
            var u = session.GetUnitByUnitId(playerTeam.unitIds[i]);
            if (u == null) continue;
            int d = Mathf.Abs(u.CurrentCell.x - fromCell.x) + Mathf.Abs(u.CurrentCell.y - fromCell.y);
            if (d < bestDist)
            {
                bestDist = d;
                best = u;
            }
        }
        return best;
    }

    private void SubmitAction(int unitId, Vector3Int targetCell, SkillDefinition skillDef)
    {
        _actionsThisInstant++;

        var unit = session.GetUnitByUnitId(unitId);
        SkillSlot slot = unit != null ? unit.GetSlot(skillDef.skillId) : SkillSlot.None;

        if (slot == SkillSlot.Movement)
            _movesUsed[unitId] = (_movesUsed.TryGetValue(unitId, out int m) ? m : 0) + 1;
        else if (slot == SkillSlot.Weapon)
            _nonInstantUsed[unitId] = (_nonInstantUsed.TryGetValue(unitId, out int w) ? w : 0) + 1;
        else if (slot == SkillSlot.Class || slot == SkillSlot.Trinket)
            _utilityUsed[unitId] = (_utilityUsed.TryGetValue(unitId, out int u) ? u : 0) + 1;

        Debug.Log($"[ServerAI] Action #{_actionsThisInstant}: unit={unitId} skill={skillDef.skillId} ({skillDef.skillName}) target={targetCell}");
        bool accepted = timeline.HandleDecision(
            ServerMatchSession.AI_CLIENT_ID,
            unitId,
            targetCell,
            DecisionType.ActivateAction,
            skillDef.skillId,
            timeline.CurrentToken);

        // Only fall back to Wait if HandleDecision synchronously rejected the command
        if (!accepted)
        {
            Debug.LogWarning($"[ServerAI] Decision rejected by HandleDecision (unit={unitId}, skill={skillDef.skillId}) — falling back to Wait.");
            SendWait();
        }
    }

    void SendWait()
    {
        Debug.Log("[ServerAI] Deciding: Wait.");
        timeline.HandleDecision(ServerMatchSession.AI_CLIENT_ID, unitId: -1, default, DecisionType.Wait, skillcardId: -1, timeline.CurrentToken);
    }
}
