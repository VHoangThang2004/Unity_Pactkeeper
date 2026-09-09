using UnityEngine;

public interface IInteractionState
{
    void OnEnter(ClientUnit unit = null, Vector3Int? targetTile = null);
    void OnExit();
    void OnTileClick(Vector3Int cell);
    void OnTileHover(Vector3Int cell);
}