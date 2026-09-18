using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UnitContainer : MonoBehaviour
{
    [Header("Config")]
    public bool isFormationSlot = false;
    public int slotIndex = -1; // only for formation slots

    [Header("UI")]
    [SerializeField] private Image unitImage;
    [SerializeField] private TextMeshProUGUI unitNameText;
    [Header("State Panels")]
    [SerializeField] private GameObject defaultPanel;
    [SerializeField] private GameObject formationSlotStatePanel; // configure/remove/change unit buttons
    [SerializeField] private GameObject changeUnitOptionPanel;   // enter team button
    [SerializeField] private GameObject emptySlotStatePanel;     // select unit button

    public string ownedUnitId { get; private set; } = string.Empty;
    public int uId { get; private set; } = -1;
    public bool isEmpty => string.IsNullOrEmpty(ownedUnitId);

    private UnitListStateMachine sm;

    public void Init(UnitListStateMachine stateMachine)
    {
        sm = stateMachine;
        Debug.Log($"[UnitContainer] Init with state machine reference: {sm}");
    }

    public void Setup(string ownedUnitId, int uId, Sprite portrait, string unitName, bool isFormationSlot = true)
    {
        this.ownedUnitId = ownedUnitId;
        this.uId = uId;
        this.isFormationSlot = isFormationSlot;
        if (unitImage != null) unitImage.sprite = portrait;
        if (unitNameText != null) unitNameText.text = unitName;
        ShowDefault();
        Debug.Log($"[UnitContainer] Setup called with ownedUnitId: {ownedUnitId}, uId: {uId}, unitName: {unitName}, isFormationSlot: {isFormationSlot}");
    }

    public void SetEmpty()
    {
        ownedUnitId = string.Empty;
        uId = -1;
        if (unitImage != null) unitImage.sprite = null;
        if (unitNameText != null) unitNameText.text = "";
        ShowEmptySlotState();
    }

    // -------------------------------------------------------
    // Display States
    // -------------------------------------------------------

    public void ShowDefault()
    {
        if (isFormationSlot && isEmpty)
        {
            ShowEmptySlotState();
            return;
        }
        defaultPanel?.SetActive(true);
        formationSlotStatePanel?.SetActive(false);
        changeUnitOptionPanel?.SetActive(false);
        emptySlotStatePanel?.SetActive(false);
        Debug.Log($"[UnitContainer] ShowDefault called for slotIndex: {slotIndex}");
    }

    public void ShowFormationSlotState()
    {
        defaultPanel?.SetActive(false);
        formationSlotStatePanel?.SetActive(true);
        changeUnitOptionPanel?.SetActive(false);
        emptySlotStatePanel?.SetActive(false);
        Debug.Log($"[UnitContainer] ShowFormationSlotState called for slotIndex: {slotIndex}");
    }

    public void ShowChangeUnitOption()
    {
        defaultPanel?.SetActive(false);
        formationSlotStatePanel?.SetActive(false);
        changeUnitOptionPanel?.SetActive(true);
        emptySlotStatePanel?.SetActive(false);
        Debug.Log($"[UnitContainer] ShowChangeUnitOption called for slotIndex: {slotIndex}");
    }

    public void ShowEmptySlotState()
    {
        defaultPanel?.SetActive(false);
        formationSlotStatePanel?.SetActive(false);
        changeUnitOptionPanel?.SetActive(false);
        emptySlotStatePanel?.SetActive(true);
        Debug.Log($"[UnitContainer] ShowEmptySlotState called for slotIndex: {slotIndex}");
    }

    // -------------------------------------------------------
    // Button Callbacks (wired in inspector)
    // -------------------------------------------------------

    public void OnClick()
    {
        Debug.Log($"[UnitContainer] OnClick called for slotIndex: {slotIndex}, isFormationSlot: {isFormationSlot}, isEmpty: {isEmpty}, state {sm?.currentState}");
        sm?.OnContainerClicked(this);
    }

    public void OnClickChangeUnit() // Enter formation config state (all units will show the "enter team" option)
    {
        Debug.Log($"[UnitContainer] OnClickChangeUnit called for slotIndex: {slotIndex}, isFormationSlot: {isFormationSlot}, isEmpty: {isEmpty}, state {sm?.currentState}");

        sm?.OnClickChangeUnit(this);
    }

    public void OnClickConfigure() // Switch to unit config scene
    {
        Debug.Log($"[UnitContainer] OnClickConfigure called for slotIndex: {slotIndex}, isFormationSlot: {isFormationSlot}, isEmpty: {isEmpty}, state {sm?.currentState}");
        sm?.OnClickConfigure(this);
    }

    public void OnClickRemove() // Remove this unit from formation slot and put it back to unit list
    {
        Debug.Log($"[UnitContainer] OnClickRemove called for slotIndex: {slotIndex}, isFormationSlot: {isFormationSlot}, isEmpty: {isEmpty}, state {sm?.currentState}");
        sm?.OnClickRemove(this);
    }

    public void OnClickEnterTeam() // Put this unit into formation slot
    {
        Debug.Log($"[UnitContainer] OnClickEnterTeam called for slotIndex: {slotIndex}, isFormationSlot: {isFormationSlot}, isEmpty: {isEmpty}, state {sm?.currentState}");
        sm?.OnClickEnterTeam(this);
    }

    public void OnClickSelectUnit() // Enter formation config state (select a unit for this formation slot)
    {
        Debug.Log($"[UnitContainer] OnClickSelectUnit called for slotIndex: {slotIndex}, isFormationSlot: {isFormationSlot}, isEmpty: {isEmpty}, state {sm?.currentState}");
        sm?.OnClickSelectUnit(this);
    }
}