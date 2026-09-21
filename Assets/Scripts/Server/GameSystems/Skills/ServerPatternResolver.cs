using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class ServerPatternResolver
{
    public static void TranslateSkillPattern(UnitData unit, SkillDefinition skill, ServerMatchSession session, int index)
    {
        var worldCells = new List<Vector3Int>();
        foreach (var raw in unit.SkillPatterns[index].TargetCells ?? new Vector3Int[0])
            worldCells.Add(new Vector3Int(
                unit.CurrentCell.x + raw.x,
                unit.CurrentCell.y + raw.y,
                0));

        unit.SkillPatterns[index].TargetCells = FilterTargetPattern(worldCells, unit, skill, session).ToArray();
    }

    public static List<Vector3Int> FilterTargetPattern(
        List<Vector3Int> cells,
        UnitData caster,
        SkillDefinition skill,
        ServerMatchSession session)
    {
        var result = new List<Vector3Int>();

        // Debug.Log("F1");

        bool skillUsable = session.CanUseSkill(caster.Id, skill.skillId, skill);

        // Debug.Log("F2");

        foreach (var cell in cells)
        {
            // Debug.Log("F3");

            if (!session.Map.IsWalkable(cell.x, cell.y))
                continue;

            // Debug.Log("F4");

            var unitAtCell = session.GetUnitAt(cell);

            // Debug.Log("F5");

            bool selectable;
            if (!skillUsable || !session.ReadyUnitIds.Contains(caster.Id))
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

            // Debug.Log("F6");
            foreach (int eId in skill.effectIds)
            {
                var effect = session.effectRegistry.Get(eId);

                if (effect == null)
                {
                    Debug.LogWarning(
                        $"Skill {skill.skillId} contains missing effect {eId}"
                    );
                    continue;
                }

                if (effect.InstantType == InstantType.NonInstant)
                {
                    selectable = session.ReadyUnitIds.Contains(caster.Id);
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