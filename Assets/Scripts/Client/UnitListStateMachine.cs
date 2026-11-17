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
                foreach (UnitContainer item in manager.unitListItems)
                    item.ShowDefault();
                break;

            case UnitListState.FormationConfig:
                foreach (UnitContainer item in manager.unitListItems)
                    item.ShowDefault();
                break;
        }
    }

    // -------------------------------------------------------
    // Input handlers (called by UnitContainer)
    // -------------------------------------------------------

    public void OnContainerClicked(UnitContainer container)
    {
        if (container == null || container.isEmpty) return;
        GoToUnitConfig(container.ownedUnitId);
    }

    public void OnUnitDropped(UnitContainer source, UnitContainer target, bool droppedOnBench)
    {
        if (source == null || source.isEmpty) return;

        if (!source.isFormationSlot)
        {
            if (target != null && target.isFormationSlot)
                PlaceListItemIntoSlot(source, target);
            return;
        }

        if (target != null && target.isFormationSlot && target != source)
        {
            MoveOrSwapFormation(source, target);
            return;
        }

        if (target != null && !target.isFormationSlot)
        {
            PlaceListItemIntoSlot(target, source);
            return;
        }

        if (droppedOnBench)
            OnClickRemove(source);
    }

    public void PlaceListItemIntoSlot(UnitContainer listItem, UnitContainer slot)
    {
        if (listItem == null || slot == null || !slot.isFormationSlot || listItem.isEmpty) return;

        OwnedUnitDto unit = manager.ownedUnits.Find(u => u.ownedUnitId == listItem.ownedUnitId);
        if (unit == null) return;

        manager.BindUnit(slot, unit, true);
        listItem.gameObject.SetActive(false);

        TransitionTo(UnitListState.None);
        SaveFormation();
    }

    void MoveOrSwapFormation(UnitContainer from, UnitContainer to)
    {
        if (from == null || to == null || from == to || from.isEmpty) return;

        if (to.isEmpty)
        {
            OwnedUnitDto moving = manager.ownedUnits.Find(u => u.ownedUnitId == from.ownedUnitId);
            if (moving == null) return;
            manager.BindUnit(to, moving, true);
            from.SetEmpty();
        }
        else
        {
            OwnedUnitDto a = manager.ownedUnits.Find(u => u.ownedUnitId == from.ownedUnitId);
            OwnedUnitDto b = manager.ownedUnits.Find(u => u.ownedUnitId == to.ownedUnitId);
            if (a == null || b == null) return;
            manager.BindUnit(from, b, true);
            manager.BindUnit(to, a, true);
        }

        TransitionTo(UnitListState.None);
        SaveFormation();
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
        PlaceListItemIntoSlot(listItem, selectedSlot);
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