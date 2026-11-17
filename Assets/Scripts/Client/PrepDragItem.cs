using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

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
        dragVisual = CreateCardGhost((RectTransform)transform, e.position);
    }

    public void OnDrag(PointerEventData e)
    {
        if (dragVisual != null) dragVisual.transform.position = e.position;
    }

    public void OnEndDrag(PointerEventData e)
    {
        if (dragVisual != null) Destroy(dragVisual);
        dragVisual = null;
        manager.DropUnit(unit, e.position);
    }

    public static GameObject CreateCardGhost(RectTransform source, Vector2 screenPosition)
    {
        var canvas = source.GetComponentInParent<Canvas>().rootCanvas;
        var ghost = Instantiate(source.gameObject, canvas.transform);
        ghost.transform.SetAsLastSibling();

        var drag = ghost.GetComponent<PrepDragItem>();
        if (drag != null)
        {
            drag.enabled = false;
            Destroy(drag);
        }

        UnitListDragHandler.FitGhostToSource((RectTransform)ghost.transform, source);

        var group = ghost.GetComponent<CanvasGroup>();
        if (group == null) group = ghost.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        group.alpha = 0.85f;
        ghost.transform.position = screenPosition;
        return ghost;
    }
}
