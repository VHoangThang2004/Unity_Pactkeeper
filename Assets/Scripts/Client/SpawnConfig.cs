using UnityEngine;

[CreateAssetMenu(fileName = "SpawnConfig", menuName = "SRPG/Spawn Config")]
public class SpawnConfig : ScriptableObject
{
    [Header("Team 0 Spawn Points")]
    public Vector2Int[] team0Spawns;

    [Header("Team 1 Spawn Points")]
    public Vector2Int[] team1Spawns;

    public Vector2Int GetSpawn(int team)
    {
        if (team == 0 && team0Spawns.Length > 0)
            return team0Spawns[0];

        if (team == 1 && team1Spawns.Length > 0)
            return team1Spawns[0];

        Debug.LogError($"[SpawnConfig] No spawn point for team {team}!");
        return Vector2Int.zero;
    }
}