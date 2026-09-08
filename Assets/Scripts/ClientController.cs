using UnityEngine;
using UnityEngine.Tilemaps;

public class ClientController : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private InputReader input;
    [SerializeField] private Tilemap tilemap;
    [SerializeField] private ClientCameraController cameraController;
    [SerializeField] private Camera cam;
    [SerializeField] private GameObject hoverHighlight;
    [SerializeField] private Vector3 offset;

    void Start()
    {
        input.OnLeftClick += HandleClick;
    }

    void OnDestroy()
    {
        input.OnLeftClick -= HandleClick;
    }

    void Update()
    {
        HandleCamera();
        HandleHover();
    }

    void HandleHover()
    {
        Vector3 worldPos = cam.ScreenToWorldPoint(input.MouseScreenPosition);
        worldPos.z = 0;

        Vector3Int cell = tilemap.WorldToCell(worldPos);

        if (!tilemap.HasTile(cell))
        {
            if (hoverHighlight.activeSelf)
                hoverHighlight.SetActive(false);

            return;
        }

        Vector3 center = tilemap.GetCellCenterWorld(cell);

        if (!hoverHighlight.activeSelf)
            hoverHighlight.SetActive(true);
        hoverHighlight.transform.position = center + offset;
    }

    void HandleCamera()
    {
        cameraController.Move(input.MoveInput, input.IsFastMove);
        cameraController.Zoom(input.ZoomInput);
    }

    void HandleClick()
    {
        Vector3 worldPos = cam.ScreenToWorldPoint(input.MouseScreenPosition);
        worldPos.z = 0;

        Vector3Int cell = tilemap.WorldToCell(worldPos);

        if (!tilemap.HasTile(cell))
        {
            Debug.Log($"Clicked EMPTY: {cell}");
            return;
        }

        Debug.Log($"Clicked TILE: {cell}");
    }
}