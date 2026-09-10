// Shared/GameSystems/Skills/SkillPattern.cs
using UnityEngine;

[CreateAssetMenu(fileName = "Pattern_", menuName = "SRPG/Pattern")]
public class SkillPattern : ScriptableObject
{
    // Offset cells from (0,0) root
    // Target pattern: (0,0) = caster position
    // AoE pattern: (0,0) = chosen target cell, ref vector (0,1) heads up
    public Vector2Int[] cells;
}