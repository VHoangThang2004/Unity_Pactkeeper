using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class GridPathfinder
{
    private static readonly Vector3Int[] directions =
    {
        Vector3Int.up,
        Vector3Int.down,
        Vector3Int.left,
        Vector3Int.right
    };

    public static List<Vector3Int> FindPath(Tilemap tilemap, Vector3Int start, Vector3Int target)
    {
        Queue<Vector3Int> queue = new Queue<Vector3Int>();
        Dictionary<Vector3Int, Vector3Int> cameFrom = new Dictionary<Vector3Int, Vector3Int>();

        queue.Enqueue(start);
        cameFrom[start] = start;

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();

            if (current == target)
                break;

            foreach (var dir in directions)
            {
                var next = current + dir;

                if (!tilemap.HasTile(next)) continue;
                if (cameFrom.ContainsKey(next)) continue;

                queue.Enqueue(next);
                cameFrom[next] = current;
            }
        }

        // No path
        if (!cameFrom.ContainsKey(target))
            return null;

        // Reconstruct path
        List<Vector3Int> path = new List<Vector3Int>();
        var temp = target;

        while (temp != start)
        {
            path.Add(temp);
            temp = cameFrom[temp];
        }

        path.Reverse();
        return path;
    }
}