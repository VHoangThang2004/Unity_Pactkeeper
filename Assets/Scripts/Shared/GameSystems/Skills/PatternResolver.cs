using System.Collections.Generic;
using UnityEngine;

public static class PatternResolver
{
    public static List<Vector3Int> TranslateTargetPattern(
        SkillDefinition skill,
        UnitData caster)
    {
        var result = new List<Vector3Int>();
        if (skill.targetPattern == null) return result;

        foreach (var offset in skill.targetPattern.cells)
            result.Add(new Vector3Int(
                caster.CurrentCell.x + offset.x,
                caster.CurrentCell.y + offset.y,
                0));
        return result;
    }

    public static List<Vector3Int> TranslateAoEPattern(
        SkillPattern aoePattern,
        Vector3Int casterCell,
        Vector3Int targetCell)
    {
        var result = new List<Vector3Int>();
        if (aoePattern == null) return result;

        Vector2Int dir = SnapTo8Dir(
            targetCell.x - casterCell.x,
            targetCell.y - casterCell.y);

        foreach (var offset in aoePattern.cells)
        {
            Vector2Int rotated = RotateOffset(offset, dir);
            result.Add(new Vector3Int(
                targetCell.x + rotated.x,
                targetCell.y + rotated.y,
                0));
        }
        return result;
    }

    public static Vector2Int SnapTo8Dir(int dx, int dy)
    {
        if (dx == 0 && dy == 0) return new Vector2Int(0, 1);

        int ax = Mathf.Abs(dx);
        int ay = Mathf.Abs(dy);
        int sx = dx > 0 ? 1 : dx < 0 ? -1 : 0;
        int sy = dy > 0 ? 1 : dy < 0 ? -1 : 0;

        if (ax > ay) return new Vector2Int(sx, 0);
        if (ay > ax) return new Vector2Int(0, sy);
        return new Vector2Int(sx, sy);
    }

    public static Vector2Int RotateOffset(Vector2Int offset, Vector2Int dir)
    {
        float cos, sin;

        if      (dir.x ==  0 && dir.y ==  1) { cos =  1;      sin =  0;     }
        else if (dir.x ==  1 && dir.y ==  0) { cos =  0;      sin =  1;     }
        else if (dir.x ==  0 && dir.y == -1) { cos = -1;      sin =  0;     }
        else if (dir.x == -1 && dir.y ==  0) { cos =  0;      sin = -1;     }
        else if (dir.x ==  1 && dir.y ==  1) { cos =  0.707f; sin =  0.707f;}
        else if (dir.x ==  1 && dir.y == -1) { cos = -0.707f; sin =  0.707f;}
        else if (dir.x == -1 && dir.y == -1) { cos = -0.707f; sin = -0.707f;}
        else                                  { cos =  0.707f; sin = -0.707f;}

        float rx = offset.x * cos - offset.y * sin;
        float ry = offset.x * sin + offset.y * cos;
        return new Vector2Int(Mathf.RoundToInt(rx), Mathf.RoundToInt(ry));
    }
}