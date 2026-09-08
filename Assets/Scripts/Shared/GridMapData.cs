using System;

[System.Serializable]
public class GridMapData
{
    public int width;
    public int height;
    public int originX;
    public int originY;
    public bool[] walkable;
}
