using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class ClientUnit : MonoBehaviour
{
    [Header("Unit Data")]
    public UnitData data;

    public HashSet<Vector3Int> reachableCells = new HashSet<Vector3Int>();

    [SerializeField] private GameObject hoverUnit;
    [SerializeField] private float moveDuration = 0.2f;
    [SerializeField] private Animator animator;

    private bool isMoving = false;

    // -------------------------------------------------------
    // Init (called by ClientSpawner)
    // -------------------------------------------------------

    public void Init(UnitData unitData)
    {
        data = unitData;
    }

    public void SetPosition(Tilemap tilemap, Vector3Int cell)
    {
        data.CurrentCell = cell;
        transform.position = tilemap.GetCellCenterWorld(cell);
    }

    // -------------------------------------------------------
    // Hover
    // -------------------------------------------------------

    public void HoverPosition(Tilemap tilemap, Vector3Int cell)
    {
        if (cell == data.CurrentCell)
        {
            HoverCancel();
            return;
        }
        hoverUnit.SetActive(true);
        hoverUnit.transform.position = tilemap.GetCellCenterWorld(cell);
    }

    public void HoverCancel()
    {
        hoverUnit.SetActive(false);
    }

    // -------------------------------------------------------
    // Range
    // -------------------------------------------------------

    public void CalculateRange(Tilemap tilemap)
    {
        reachableCells.Clear();

        for (int x = -data.MoveRange; x <= data.MoveRange; x++)
        {
            for (int y = -data.MoveRange; y <= data.MoveRange; y++)
            {
                int dist = Mathf.Abs(x) + Mathf.Abs(y);
                if (dist > data.MoveRange) continue;

                Vector3Int cell = data.CurrentCell + new Vector3Int(x, y, 0);
                if (!tilemap.HasTile(cell)) continue;

                // TODO: check obstacles

                reachableCells.Add(cell);
            }
        }
    }

    // -------------------------------------------------------
    // Movement
    // -------------------------------------------------------

    public void MoveTo(Tilemap tilemap, Vector3Int targetCell)
    {
        if (isMoving) return;
        var path = GridPathfinder.FindPath(tilemap, data.CurrentCell, targetCell);
        if (path == null)
        {
            Debug.LogWarning($"[ClientUnit] No path found to {targetCell}");
            return;
        }
        StartCoroutine(MovePathRoutine(tilemap, path));
    }

    IEnumerator MovePathRoutine(Tilemap tilemap, List<Vector3Int> path)
    {
        isMoving = true;

        if (animator != null)
        {
            animator.SetBool("IsMoving", true);
            animator.SetBool("IsIdling", false);
        }

        foreach (var cell in path)
        {
            Vector3 start = transform.position;
            Vector3 end = tilemap.GetCellCenterWorld(cell);

            if (animator != null)
                animator.SetFloat("MoveX", end.x - start.x);

            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / moveDuration;
                transform.position = Vector3.Lerp(start, end, t);
                yield return null;
            }

            transform.position = end;
            data.CurrentCell = cell;
        }

        isMoving = false;

        if (animator != null)
        {
            animator.SetBool("IsMoving", false);
            animator.SetBool("IsIdling", true);
        }
    }
}