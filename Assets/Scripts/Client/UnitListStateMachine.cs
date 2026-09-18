using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class UnitListStateMachine : MonoBehaviour
{
    public enum UnitListState { None, FormationSlot, FormationConfig }

    [Header("Refs")]
    [SerializeField] private SceneConfig sceneConfig;
    [SerializeField] private UnitListManager manager;

    public UnitListState currentState = UnitListState.None;
    private UnitContainer selectedSlot = null;

    // Formation slots — pre-placed in inspector
    // Unit list — spawned at runtime

    public void Init(List<UnitContainer> slots, List<UnitContainer> listItems)
    {
        manager.formationSlots = slots;
        manager.unitListItems = listItems;
        TransitionTo(UnitListState.None);
    }
    public void UpdateUnitListItems(List<UnitContainer> items)
    {
        manager.unitListItems = items;
    }

    // -------------------------------------------------------
    // Transitions
    // -------------------------------------------------------

    void TransitionTo(UnitListState next)
    {
        Debug.Log($"[UnitListStateMachine] Transitioning from {currentState} to {next}");
        currentState = next;
        switch (next)
        {
            case UnitListState.None:
                selectedSlot = null;
                foreach (var slot in manager.formationSlots)
                {
                    if (slot.isEmpty) slot.ShowEmptySlotState();
                    else slot.ShowDefault();
                }
                foreach (var item in manager.unitListItems)
                    item.ShowDefault();
                break;
            case UnitListState.FormationSlot:
                foreach (UnitContainer slot in manager.formationSlots)
                    slot.ShowDefault();
                selectedSlot?.ShowFormationSlotState();
                foreach (UnitContainer item in manager.unitListItems)
                    item.ShowDefault();
                break;

            case UnitListState.FormationConfig:
                selectedSlot?.ShowFormationSlotState();
                foreach (UnitContainer item in manager.unitListItems)
                    item.ShowChangeUnitOption();
                break;
        }
    }

    // -------------------------------------------------------
    // Input handlers (called by UnitContainer)
    // -------------------------------------------------------

    public void OnContainerClicked(UnitContainer container)
    {
        if (container.isFormationSlot)
        {
            if (currentState == UnitListState.None)
            {
                if (!container.isEmpty)
                {
                    selectedSlot = container;
                    TransitionTo(UnitListState.FormationSlot);
                }
            }
            else
            {
                TransitionTo(UnitListState.None);
            }
        }
        else
        {
            // Unit list item clicked in None state → go to unit config
            if (currentState == UnitListState.None)
                GoToUnitConfig(container.ownedUnitId);
            else
                TransitionTo(UnitListState.None);
        }
    }

    public void OnClickChangeUnit(UnitContainer slot)
    {
        if (!slot.isEmpty && currentState != UnitListState.FormationSlot) return;
        selectedSlot = slot;
        TransitionTo(UnitListState.FormationConfig);
    }

    public void OnClickConfigure(UnitContainer slot)
    {
        GoToUnitConfig(slot.ownedUnitId);
    }

    public void OnClickRemove(UnitContainer slot)
    {
        slot.SetEmpty();
        TransitionTo(UnitListState.None);
        // TODO: save formation to backend
        SaveFormation();
    }

    public void OnClickEnterTeam(UnitContainer listItem)
    {
        if (currentState != UnitListState.FormationConfig || selectedSlot == null) return;

        // Swap — put old slot unit back into list
        if (!selectedSlot.isEmpty)
        {
            // Find the list item that matches the slot's unit and restore it to default
            var displaced = manager.unitListItems.Find(u => u.ownedUnitId == selectedSlot.ownedUnitId);
            displaced?.ShowDefault();
        }

        // Move list item data into slot
        var prefab = manager.unitPrefabRegistry.Get(listItem.uId);
        Sprite portrait = prefab != null ? prefab.GetComponent<ClientUnit>()?.unitImg : null;
        selectedSlot.Setup(listItem.ownedUnitId, listItem.uId, portrait, manager.unitPrefabRegistry.GetName(listItem.uId), true);
        // Hide the list item that entered the team
        listItem.gameObject.SetActive(false);

        TransitionTo(UnitListState.None);
        SaveFormation();
    }

    public void OnClickSelectUnit(UnitContainer emptySlot)
    {
        selectedSlot = emptySlot;
        TransitionTo(UnitListState.FormationConfig);
    }

    // -------------------------------------------------------
    // Helpers
    // -------------------------------------------------------

    void GoToUnitConfig(string ownedUnitId)
    {
        PlayerSession.SelectedOwnedUnitId = ownedUnitId;
        SceneManager.LoadScene(sceneConfig.unitConfigScene);
    }
    void SaveFormation()
    {
        var uIds = new List<int>();
        foreach (var slot in manager.formationSlots)
            if (!slot.isEmpty) uIds.Add(slot.uId);

        StartCoroutine(manager.SaveFormationToBackend(uIds));
    }
}