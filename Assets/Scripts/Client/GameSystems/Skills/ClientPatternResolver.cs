// Client/GameSystems/Skills/ClientPatternResolver.cs
using System.Collections.Generic;
using UnityEngine;

public static class ClientPatternResolver
{
    /// <summary>
    /// Calculates AoE cells for a skill given a chosen target cell.
    /// Called during decision making when player hovers over target cells.
    /// </summary>
    public static List<Vector3Int> GetAoECells(
        SkillPattern aoePattern,
        Vector3Int casterCell,
        Vector3Int targetCell,
        ClientMatchSession session)
    {
        var translated = PatternResolver.TranslateAoEPattern(aoePattern, casterCell, targetCell);
        var result = new List<Vector3Int>();
        foreach (var cell in translated)
            if (session.Map != null && session.Map.IsWalkable(cell.x, cell.y))
                result.Add(cell);
        return result;
    }
}