using System.Collections.Generic;
using UnityEngine;

public static class OffsetRecalculator
{
    public static Vector3Int RecalculateNewTargetCell(Vector3Int oldPosition, Vector3Int oldTarget, Vector3Int newPosition)
    {
        Vector3Int offset = oldTarget - oldPosition;
        return newPosition + offset;
    }
}