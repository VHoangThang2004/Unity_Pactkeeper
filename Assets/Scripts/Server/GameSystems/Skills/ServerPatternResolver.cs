using System.Collections.Generic;
using UnityEngine;

public static class ServerPatternResolver
{
    public static CurrentPatterns BuildSkillPattern(UnitData unit, SkillDefinition skill, ServerMatchSession session)
    {
        var targetCells = new List<Vector3Int>();

        if (skill.targetPattern != null)
        {
            var translated = PatternResolver.TranslateTargetPattern(skill, unit);
            targetCells = FilterTargetPattern(translated, unit, skill, session);
        }

        Vector2Int[] aoePattern = null;
        if (skill.effectIds != null)
            foreach (var effectId in skill.effectIds)
            {
                var effect = session.effectRegistry.Get(effectId);
                if (effect?.aoePattern?.cells != null)
                {
                    aoePattern = effect.aoePattern.cells;
                    break;
                }
            }

        return new CurrentPatterns
        {
            SkillId = skill.skillId,
            TargetCells = targetCells.ToArray(),
            AoePattern = aoePattern
        };
    }

    public static List<Vector3Int> FilterTargetPattern(
        List<Vector3Int> cells,
        UnitData caster,
        SkillDefinition skill,
        ServerMatchSession session)
    {
        var result = new List<Vector3Int>();
        bool skillUsable = session.CanUseSkill(caster.Id, skill.skillId, skill);

        foreach (var cell in cells)
        {
            if (!session.Map.IsWalkable(cell.x, cell.y)) continue;
            var unitAtCell = session.GetUnitAt(cell);

            bool selectable;
            if (!skillUsable)
            {
                selectable = false;
            }
            else
            {
                switch (skill.targeting)
                {
                    case TargetFilter.EmptyCell:
                        selectable = unitAtCell == null;
                        break;
                    case TargetFilter.UnitCell:
                        selectable = unitAtCell != null;
                        break;
                    case TargetFilter.EnemyUnit:
                        selectable = unitAtCell != null &&
                            session.GetTeamIdByUnitId(unitAtCell.Id) != session.GetTeamIdByUnitId(caster.Id);
                        break;
                    case TargetFilter.AllyUnit:
                        selectable = unitAtCell != null &&
                            session.GetTeamIdByUnitId(unitAtCell.Id) == session.GetTeamIdByUnitId(caster.Id);
                        break;
                    case TargetFilter.AnyCell:
                        selectable = true;
                        break;
                    default:
                        selectable = false;
                        break;
                }
            }

            result.Add(new Vector3Int(cell.x, cell.y, selectable ? 1 : 0));
        }

        return result;
    }

    public static List<Vector3Int> FilterAoEPattern(
        List<Vector3Int> cells,
        ServerMatchSession session)
    {
        var result = new List<Vector3Int>();
        foreach (var cell in cells)
            if (session.Map.IsWalkable(cell.x, cell.y))
                result.Add(cell);
        return result;
    }
}