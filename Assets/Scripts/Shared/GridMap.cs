using UnityEngine;

public class GridMap
{
    public int Width { get; private set; }
    public int Height { get; private set; }

    public int OriginX { get; private set; }
    public int OriginY { get; private set; }

    private bool[] walkable;

    public void Init(GridMapData data)
    {
        Width = data.width;
        Height = data.height;
        OriginX = data.originX;
        OriginY = data.originY;
        walkable = data.walkable;
    }

    // 🔥 MAIN FUNCTION (server will use this everywhere)
    public bool IsWalkable(int x, int y)
    {
        Vector2Int pos = TranslatePos(x, y);
        if (!InBounds(pos.x, pos.y))
            return false;
        Debug.Log($"Width={Width}, Height={Height}, WalkableLength={walkable.Length}");
        return walkable[pos.x + pos.y * Width];
    }
    public void SetWalkable(int x, int y, bool value)
    {
        Vector2Int pos = TranslatePos(x, y);
        if (!InBounds(pos.x, pos.y))
            return;
        Debug.Log($"Width={Width}, Height={Height}, WalkableLength={walkable.Length}");
        walkable[pos.x + pos.y * Width] = value;
        return;
    }


    public bool InBounds(int x, int y)
    {
        return x >= 0 && y >= 0 && x < Width && y < Height;
    }

    public Vector2Int TranslatePos(int worldX, int worldY)
    {
        return new Vector2Int(
            worldX - OriginX,
            worldY - OriginY
        );
    }

    private bool IsWalkableLocalInternal(int x, int y)
    {
        return walkable[x + y * Width];
    }
}