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

    // -------------------------------------------------------
    // GridMap versions (server + client shared logic)
    // -------------------------------------------------------

    /// <summary>
    /// Finds shortest path using GridMap for walkability.
    /// Occupied cells are hard walls — can't pass through or land on.
    /// Returns null if no path exists or path exceeds moveRange.
    /// </summary>
    public static List<Vector3Int> FindPath(
        GridMap map,
        Vector3Int start,
        Vector3Int target,
        ICollection<Vector3Int> occupiedCells = null)
    {
        if (occupiedCells != null && occupiedCells.Contains(target))
            return null;

        var queue = new Queue<Vector3Int>();
        var cameFrom = new Dictionary<Vector3Int, Vector3Int>();

        queue.Enqueue(start);
        cameFrom[start] = start;

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (current == target) break;

            foreach (var dir in directions)
            {
                var next = current + dir;
                if (!map.IsWalkable(next.x, next.y)) continue;
                if (cameFrom.ContainsKey(next)) continue;
                if (occupiedCells != null && occupiedCells.Contains(next)) continue;

                queue.Enqueue(next);
                cameFrom[next] = current;
            }
        }

        if (!cameFrom.ContainsKey(target)) return null;

        var path = new List<Vector3Int>();
        var temp = target;
        while (temp != start)
        {
            path.Add(temp);
            temp = cameFrom[temp];
        }
        path.Reverse();
        return path;
    }

    /// <summary>
    /// Flood-fill BFS using GridMap — returns all cells reachable within maxSteps.
    /// Occupied cells are hard walls.
    /// </summary>
    public static HashSet<Vector3Int> FloodFill(
        GridMap map,
        Vector3Int start,
        int maxSteps,
        ICollection<Vector3Int> occupiedCells = null)
    {
        var reachable = new HashSet<Vector3Int>();
        var queue = new Queue<(Vector3Int cell, int steps)>();
        var visited = new Dictionary<Vector3Int, int>();

        queue.Enqueue((start, 0));
        visited[start] = 0;

        while (queue.Count > 0)
        {
            var (current, steps) = queue.Dequeue();

            if (current != start)
                reachable.Add(current);

            if (steps >= maxSteps) continue;

            foreach (var dir in directions)
            {
                var next = current + dir;
                if (!map.IsWalkable(next.x, next.y)) continue;
                if (occupiedCells != null && occupiedCells.Contains(next)) continue;

                int nextSteps = steps + 1;
                if (visited.TryGetValue(next, out int best) && best <= nextSteps) continue;

                visited[next] = nextSteps;
                queue.Enqueue((next, nextSteps));
            }
        }

        return reachable;
    }

    // -------------------------------------------------------
    // Tilemap versions (client visual path animation only)
    // -------------------------------------------------------

    /// <summary>
    /// Finds path using Tilemap for walkability — used for visual movement animation only.
    /// For game logic validation, use the GridMap version.
    /// </summary>
    public static List<Vector3Int> FindPath(
        Tilemap tilemap,
        Vector3Int start,
        Vector3Int target,
        ICollection<Vector3Int> occupiedCells = null)
    {
        if (occupiedCells != null && occupiedCells.Contains(target))
            return null;

        var queue = new Queue<Vector3Int>();
        var cameFrom = new Dictionary<Vector3Int, Vector3Int>();

        queue.Enqueue(start);
        cameFrom[start] = start;

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (current == target) break;

            foreach (var dir in directions)
            {
                var next = current + dir;
                if (!tilemap.HasTile(next)) continue;
                if (cameFrom.ContainsKey(next)) continue;
                if (occupiedCells != null && occupiedCells.Contains(next)) continue;

                queue.Enqueue(next);
                cameFrom[next] = current;
            }
        }

        if (!cameFrom.ContainsKey(target)) return null;

        var path = new List<Vector3Int>();
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