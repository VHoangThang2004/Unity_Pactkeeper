using UnityEngine;
using UnityEngine.InputSystem;

public class ClientCameraController : MonoBehaviour
{
    [Header("Move")]
    public float moveSpeed = 10f;
    public float fastMultiplier = 2f;

    [Header("Zoom (Orthographic)")]
    public float zoomSpeed = 5f;
    public float minZoom = 5f;
    public float maxZoom = 8f;

    [Header("Bounds")]
    [SerializeField] private BoxCollider2D boundsCollider;

    [Header("Camera")]
    [SerializeField] public Camera cam;

    void Start()
    {
        if (cam == null)
            cam = Camera.main;

        cam.orthographic = true;
        // cam.orthographicSize = 6f;
    }
    void Update()
    {
        ClampPosition();
    }
    public void Move(Vector2 input, bool isFast)
    {
        if (input == Vector2.zero) return;

        float speed = moveSpeed;

        if (isFast)
            speed *= fastMultiplier;

        Vector3 dir = new Vector3(input.x, input.y, 0f);

        transform.position += dir * speed * Time.deltaTime;
    }

    public void Zoom(float zoomInput)
    {
        if (Mathf.Abs(zoomInput) < 0.01f) return;

        cam.orthographicSize -= zoomInput * zoomSpeed * 0.01f;
        cam.orthographicSize = Mathf.Clamp(cam.orthographicSize, minZoom, maxZoom);
    }

    void OnDrawGizmos()
    {
        if (boundsCollider == null) return;

        // Draw map bounds
        Gizmos.color = Color.green;
        Bounds bounds = boundsCollider.bounds;
        Gizmos.DrawWireCube(bounds.center, bounds.size);

        // Draw camera view
        if (cam != null)
        {
            Gizmos.color = Color.yellow;

            float camHeight = cam.orthographicSize * 2f;
            float camWidth = camHeight * cam.aspect;

            Vector3 camSize = new Vector3(camWidth, camHeight, 0f);
            Gizmos.DrawWireCube(cam.transform.position, camSize);
        }
    }
    void ClampPosition()
    {
        if (boundsCollider == null || cam == null) return;

        Bounds bounds = boundsCollider.bounds;

        float camHeight = cam.orthographicSize;
        float camWidth = cam.aspect * camHeight;

        Vector3 pos = transform.position;

        pos.x = Mathf.Clamp(pos.x, bounds.min.x + camWidth, bounds.max.x - camWidth);
        pos.y = Mathf.Clamp(pos.y, bounds.min.y + camHeight, bounds.max.y - camHeight);

        transform.position = pos;
    }
}