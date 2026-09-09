using UnityEngine;

public class GridMap
{
    public int Width { get; private set; }
    public int Height { get; private set; }
    public int OriginX { get; private set; }
    public int OriginY { get; private set; }

    private bool[] walkable;

    public void Init(GridMapAsset asset)
    {
        Width   = asset.width;
        Height  = asset.height;
        OriginX = asset.originX;
        OriginY = asset.originY;
        walkable = asset.walkable;
    }

    // -------------------------------------------------------
    // Walkability
    // -------------------------------------------------------

    public bool IsWalkable(int x, int y)
    {
        Vector2Int pos = TranslatePos(x, y);
        if (!InBounds(pos.x, pos.y)) return false;
        return walkable[pos.x + pos.y * Width];
    }

    public void SetWalkable(int x, int y, bool value)
    {
        Vector2Int pos = TranslatePos(x, y);
        if (!InBounds(pos.x, pos.y)) return;
        walkable[pos.x + pos.y * Width] = value;
    }

    // -------------------------------------------------------
    // Helpers
    // -------------------------------------------------------

    public bool InBounds(int x, int y)
    {
        return x >= 0 && y >= 0 && x < Width && y < Height;
    }

    public Vector2Int TranslatePos(int worldX, int worldY)
    {
        return new Vector2Int(worldX - OriginX, worldY - OriginY);
    }
}