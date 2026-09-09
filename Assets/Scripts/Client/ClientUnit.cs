using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class ClientUnit : MonoBehaviour
{
    [Header("Unit Data")]
    public UnitData data;

    [SerializeField] public GameObject hoverUnit;
    [SerializeField] public float moveDuration = 0.2f;
    [SerializeField] private Animator animator;

    private bool isMoving = false;

    // -------------------------------------------------------
    // Init
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
    // Movement (visual only — server already authorized)
    // -------------------------------------------------------

    public void MoveTo(Tilemap tilemap, Vector3Int targetCell, ICollection<Vector3Int> occupiedCells = null)
    {
        if (isMoving) return;
        var path = GridPathfinder.FindPath(tilemap, data.CurrentCell, targetCell, occupiedCells);
        if (path == null)
        {
            Debug.LogWarning($"[ClientUnit] No path found to {targetCell} — teleporting.");
            data.CurrentCell = targetCell;
            transform.position = tilemap.GetCellCenterWorld(targetCell);
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
            {
                animator.SetFloat("MoveX", end.x - start.x);
                animator.SetFloat("MoveY", end.y - start.y);
            }

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