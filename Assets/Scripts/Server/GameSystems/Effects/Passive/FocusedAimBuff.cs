using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "FocusedAimBuff", menuName = "SRPG/Effects/Server/FocusedAimBuff")]
public class FocusedAimBuff : ServerPassiveEffectBase
{
    public override EffectType EffectType => EffectType.Passive;

    public override void ApplyPatternModifier(UnitData unit, ServerMatchSession session)
    {
        for (int i = 0; i < unit.SkillPatterns.Length; i++)
        {
            SkillDefinition skillDef = session.skillLibrary.Get(unit.SkillPatterns[i].SkillId);
            if (skillDef.skillId / 1000 == 1) continue; //movement skill Ids start with 1
            if (skillDef.isTargetPatternFixed) continue;

            int maxAbsY = 0;
            int maxAbsX = 0;

            foreach (Vector3Int cell in unit.SkillPatterns[i].TargetCells)
            {
                if (Mathf.Abs(cell.y) > maxAbsY)
                    maxAbsY = Mathf.Abs(cell.y);
                if (Mathf.Abs(cell.x) > maxAbsX)
                    maxAbsX = Mathf.Abs(cell.x);
            }

            var newCells = new List<Vector3Int>(unit.SkillPatterns[i].TargetCells);

            foreach (var cell in unit.SkillPatterns[i].TargetCells)
            {
                if (Mathf.Abs(cell.y) == maxAbsY)
                    newCells.Add(new Vector3Int(cell.x, cell.y + (int)Mathf.Sign(cell.y), 0));
                if (Mathf.Abs(cell.x) == maxAbsX)
                    newCells.Add(new Vector3Int(cell.x + (int)Mathf.Sign(cell.x), cell.y, 0));
            }

            unit.SkillPatterns[i].TargetCells = newCells.ToArray();
        }
    }

    public override ResolveResult Apply(
        ServerMatchSession session, int sourceUnitId, Vector3Int target)
        => new ResolveResult { EffectId = -1 }; // passive — never directly applied
}