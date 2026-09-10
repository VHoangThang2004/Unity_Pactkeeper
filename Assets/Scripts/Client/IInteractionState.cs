using UnityEngine;

public interface IInteractionState
{
    void OnEnter(int? unitId = null, Vector3Int? targetTile = null, int? skillId = null);
    void OnExit();
    void OnTileClick(Vector3Int cell);
    void OnTileHover(Vector3Int cell);
    void OnDecision();
    void Cancel();
}