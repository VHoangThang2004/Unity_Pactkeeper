using System.Collections.Generic;
using UnityEngine;

public static class ServerPatternResolver
{
    public static List<Vector3Int> FilterTargetPattern(
        List<Vector3Int> cells,
        UnitData caster,
        SkillDefinition skill,
        ServerMatchSession session)
    {
        var result = new List<Vector3Int>();

        foreach (var cell in cells)
        {
            if (!session.Map.IsWalkable(cell.x, cell.y)) continue;
            var unitAtCell = session.GetUnitAt(cell);

            switch (skill.targeting)
            {
                case TargetFilter.EmptyCell:
                    if (unitAtCell == null) result.Add(cell);
                    break;
                case TargetFilter.UnitCell:
                    if (unitAtCell != null) result.Add(cell);
                    break;
                case TargetFilter.EnemyUnit:
                    if (unitAtCell != null &&
                        session.GetTeamIdByUnitId(unitAtCell.Id) != session.GetTeamIdByUnitId(caster.Id))
                        result.Add(cell);
                    break;
                case TargetFilter.AllyUnit:
                    if (unitAtCell != null &&
                        session.GetTeamIdByUnitId(unitAtCell.Id) == session.GetTeamIdByUnitId(caster.Id))
                        result.Add(cell);
                    break;
                case TargetFilter.AnyCell:
                    result.Add(cell);
                    break;
            }
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