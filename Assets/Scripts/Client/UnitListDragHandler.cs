using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class UnitListDragHandler : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    static readonly Color Highlight = new Color(0.7f, 1f, 0.7f, 1f);

    UnitContainer container;
    ScrollRect scroll;
    bool routingToScroll;
    bool unitDrag;

    GameObject ghost;
    CanvasGroup sourceGroup;
    float sourceAlpha = 1f;
    UnitContainer highlighted;
    readonly List<RaycastResult> hits = new();

    void Awake()
    {
        container = GetComponent<UnitContainer>();
    }

    void OnDisable()
    {
        EndScrollRoute(null);
        ClearUnitDragVisuals();
        if (container != null)
            container.suppressNextClick = false;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (container == null || container.isEmpty) return;

        container.suppressNextClick = true;
        eventData.eligibleForClick = false;

        scroll = container.isFormationSlot ? null : GetComponentInParent<ScrollRect>();
        if (scroll != null && scroll.isActiveAndEnabled)
        {
            routingToScroll = true;
            scroll.OnBeginDrag(eventData);
            return;
        }

        BeginUnitDrag(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (routingToScroll)
        {
            if (PointerLeftBench(eventData))
            {
                EndScrollRoute(eventData);
                BeginUnitDrag(eventData);
            }
            else
            {
                scroll.OnDrag(eventData);
                return;
            }
        }

        if (!unitDrag || ghost == null) return;

        ghost.transform.position = eventData.position;
        UpdateHighlight(eventData);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        var machine = container != null ? container.StateMachine : null;
        ReleaseClickSuppression();

        if (routingToScroll)
        {
            EndScrollRoute(eventData);
            return;
        }

        if (!unitDrag)
        {
            ClearUnitDragVisuals();
            return;
        }

        FindDrop(eventData, out var target, out var onBench);
        ClearUnitDragVisuals();
        machine?.OnUnitDropped(container, target, onBench);
    }

    void ReleaseClickSuppression()
    {
        if (!isActiveAndEnabled)
        {
            if (container != null) container.suppressNextClick = false;
            return;
        }

        StartCoroutine(ReleaseClickSuppressionEndOfFrame());
    }

    System.Collections.IEnumerator ReleaseClickSuppressionEndOfFrame()
    {
        yield return new WaitForEndOfFrame();
        if (container != null)
            container.suppressNextClick = false;
    }

    void BeginUnitDrag(PointerEventData eventData)
    {
        if (unitDrag || container == null || container.isEmpty) return;

        unitDrag = true;
        var canvas = GetComponentInParent<Canvas>();
        if (canvas == null) return;
        canvas = canvas.rootCanvas;

        ghost = Instantiate(gameObject, canvas.transform);
        ghost.transform.SetAsLastSibling();
        var cloneHandler = ghost.GetComponent<UnitListDragHandler>();
        if (cloneHandler != null)
        {
            cloneHandler.enabled = false;
            Destroy(cloneHandler);
        }

        FitGhostToSource((RectTransform)ghost.transform, (RectTransform)transform);

        var group = ghost.GetComponent<CanvasGroup>();
        if (group == null) group = ghost.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        group.alpha = 0.85f;
        ghost.transform.position = eventData.position;

        sourceGroup = GetComponent<CanvasGroup>();
        if (sourceGroup == null) sourceGroup = gameObject.AddComponent<CanvasGroup>();
        sourceAlpha = sourceGroup.alpha;
        sourceGroup.alpha = 0.45f;
    }

    public static void FitGhostToSource(RectTransform ghostRect, RectTransform source)
    {
        Vector2 size = source.rect.size;
        if (size.x < 2f || size.y < 2f)
            size = new Vector2(250f, 300f);

        ghostRect.anchorMin = ghostRect.anchorMax = new Vector2(0.5f, 0.5f);
        ghostRect.pivot = new Vector2(0.5f, 0.5f);
        ghostRect.sizeDelta = size;

        Vector3 parentScale = ghostRect.parent != null ? ghostRect.parent.lossyScale : Vector3.one;
        Vector3 sourceScale = source.lossyScale;
        ghostRect.localScale = new Vector3(
            Mathf.Approximately(parentScale.x, 0f) ? 1f : sourceScale.x / parentScale.x,
            Mathf.Approximately(parentScale.y, 0f) ? 1f : sourceScale.y / parentScale.y,
            1f);

        foreach (var mask in ghostRect.GetComponentsInChildren<Mask>(true))
            mask.enabled = false;
        foreach (var rectMask in ghostRect.GetComponentsInChildren<RectMask2D>(true))
            rectMask.enabled = false;

        foreach (var graphic in ghostRect.GetComponentsInChildren<MaskableGraphic>(true))
        {
            graphic.maskable = false;
            graphic.SetAllDirty();
        }

        foreach (var renderer in ghostRect.GetComponentsInChildren<CanvasRenderer>(true))
            renderer.cull = false;
    }

    bool PointerLeftBench(PointerEventData eventData)
    {
        if (scroll == null) return true;
        var area = scroll.viewport != null ? scroll.viewport : scroll.transform as RectTransform;
        if (area == null) return true;
        return !RectTransformUtility.RectangleContainsScreenPoint(area, eventData.position, eventData.pressEventCamera);
    }

    void EndScrollRoute(PointerEventData eventData)
    {
        if (!routingToScroll) return;
        routingToScroll = false;
        if (scroll != null && eventData != null)
            scroll.OnEndDrag(eventData);
    }

    void FindDrop(PointerEventData eventData, out UnitContainer target, out bool onBench)
    {
        target = null;
        onBench = false;
        if (EventSystem.current == null) return;

        hits.Clear();
        EventSystem.current.RaycastAll(eventData, hits);
        foreach (var hit in hits)
        {
            if (ghost != null && hit.gameObject.transform.IsChildOf(ghost.transform))
                continue;

            var card = hit.gameObject.GetComponentInParent<UnitContainer>();
            if (card != null)
            {
                if (card != container)
                {
                    target = card;
                    return;
                }
                continue;
            }

            if (hit.gameObject.GetComponentInParent<UnitListDropCatcher>() != null)
                onBench = true;
        }
    }

    void UpdateHighlight(PointerEventData eventData)
    {
        FindDrop(eventData, out var target, out _);
        if (target != null && !IsLegalTarget(target))
            target = null;

        if (highlighted == target) return;

        if (highlighted != null)
            highlighted.SetTint(Color.white);

        highlighted = target;
        if (highlighted != null)
            highlighted.SetTint(Highlight);
    }

    bool IsLegalTarget(UnitContainer candidate)
    {
        if (candidate == null || candidate == container) return false;
        if (!container.isFormationSlot)
            return candidate.isFormationSlot;
        return true;
    }

    void ClearUnitDragVisuals()
    {
        unitDrag = false;

        if (highlighted != null)
        {
            highlighted.SetTint(Color.white);
            highlighted = null;
        }

        if (sourceGroup != null)
            sourceGroup.alpha = sourceAlpha;

        if (ghost != null)
        {
            Destroy(ghost);
            ghost = null;
        }
    }
}

public class UnitListDropCatcher : MonoBehaviour
{
}
