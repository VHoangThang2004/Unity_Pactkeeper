using UnityEngine;

public class RopeLine : MonoBehaviour
{
    [Header("Line Renderer")]
    [SerializeField] public LineRenderer lineRenderer;

    [Header("Config")]
    [SerializeField] private float startWidth = 0.1f;
    [SerializeField] private float endWidth = 0.05f;

    void Awake()
    {
        lineRenderer.positionCount = 2;
        lineRenderer.startWidth = startWidth;
        lineRenderer.endWidth = endWidth;
        gameObject.SetActive(false);
    }

    public void Show(Vector3 from, Vector3 to)
    {
        gameObject.SetActive(true);
        SetPositions(from, to);
    }

    public void SetPositions(Vector3 from, Vector3 to)
    {
        lineRenderer.SetPosition(0, from);
        lineRenderer.SetPosition(1, to);
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }
}