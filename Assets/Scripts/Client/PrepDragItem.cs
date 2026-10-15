using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Added at runtime to each unit list entry during formation prep.
/// Drags a copy of the entry; on release hands the drop point to FormationPrepManager.
/// </summary>
public class PrepDragItem : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    private FormationPrepManager manager;
    private OwnedUnitDto unit;
    private GameObject dragVisual;

    public void Init(FormationPrepManager manager, OwnedUnitDto unit)
    {
        this.manager = manager;
        this.unit = unit;
    }

    public void OnBeginDrag(PointerEventData e)
    {
        var canvas = GetComponentInParent<Canvas>().rootCanvas;
        dragVisual = Instantiate(gameObject, canvas.transform);
        Destroy(dragVisual.GetComponent<PrepDragItem>());
        var group = dragVisual.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false; // let the drop reach the field
        group.alpha = 0.8f;
        dragVisual.transform.position = e.position;
    }

    public void OnDrag(PointerEventData e)
    {
        if (dragVisual != null) dragVisual.transform.position = e.position;
    }

    public void OnEndDrag(PointerEventData e)
    {
        if (dragVisual != null) Destroy(dragVisual);
        manager.DropUnit(unit, e.position);
    }
}
