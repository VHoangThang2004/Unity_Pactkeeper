using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;

public class ExtendedButton : MonoBehaviour, IPointerClickHandler, IPointerDownHandler, IPointerUpHandler
{
    public UnityEvent onLeftClick;
    public UnityEvent onRightClick;
    public UnityEvent onHold;

    [SerializeField] private float holdThreshold = 0.5f;

    private bool isHolding = false;
    private float holdTimer = 0f;
    private bool holdFired = false;

    void Update()
    {
        if (isHolding)
        {
            holdTimer += Time.deltaTime;
            if (holdTimer >= holdThreshold && !holdFired)
            {
                holdFired = true;
                onHold?.Invoke();
            }
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (holdFired) return; // don't fire click if hold was triggered

        if (eventData.button == PointerEventData.InputButton.Left)
            onLeftClick?.Invoke();
        else if (eventData.button == PointerEventData.InputButton.Right)
            onRightClick?.Invoke();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left) return;
        isHolding = true;
        holdTimer = 0f;
        holdFired = false;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        isHolding = false;
    }
}