using UnityEngine;
using UnityEngine.Tilemaps;

public class ClientInteractionSystem : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private ClientController controller;

    public InteractionStateMachine stateMachine;

    // Wired at runtime by ClientMapLoader
    private Tilemap tilemap;

    public void SetTilemap(Tilemap t)
    {
        tilemap = t;
    }

    // -------------------------------------------------------
    // Init (called by ClientController after map is loaded)
    // -------------------------------------------------------

    public void Init()
    {
        stateMachine = new InteractionStateMachine(controller);
        stateMachine.Start();
    }

    // -------------------------------------------------------
    // Interaction
    // -------------------------------------------------------

    public void HandleTileHover()
    {
        if (tilemap == null) return;

        Vector3 worldPos = controller.cam.ScreenToWorldPoint(controller.input.MouseScreenPosition);
        worldPos.z = 0;

        controller.currentCell = tilemap.WorldToCell(worldPos);

        if (!tilemap.HasTile(controller.currentCell))
        {
            if (controller.hoverHighlight != null && controller.hoverHighlight.activeSelf)
                controller.hoverHighlight.SetActive(false);
            controller.isOnCell = false;
            return;
        }

        controller.isOnCell = true;
        Vector3 center = tilemap.GetCellCenterWorld(controller.currentCell);

        if (controller.hoverHighlight != null && !controller.hoverHighlight.activeSelf)
            controller.hoverHighlight.SetActive(true);

        if (controller.hoverHighlight != null)
            controller.hoverHighlight.transform.position = center + controller.offset;

        stateMachine.OnTileHover(controller.currentCell);
    }

    public void ForceNoneState()
    {
        stateMachine?.GoToNone();
    }

    public void HandleTileClick(Vector3Int cell)
    {
        stateMachine?.OnTileClick(cell);
    }
}