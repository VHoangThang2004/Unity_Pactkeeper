using UnityEngine;

/// <summary>
/// Watches local match state each frame and fires PlotDirector triggers
/// at meaningful game moments. Only used in C0_S1 — not referenced by
/// any other scene or script.
///
/// Add to C0_S1 scene alongside C0_S1_TutorialStarter.
/// Wire session and interactionSystem in Inspector.
/// </summary>
public class TutorialTrigger : MonoBehaviour
{
    [SerializeField] private ClientMatchSession session;
    [SerializeField] private ClientInteractionSystem interactionSystem;

    // Track which triggers have already fired — each fires at most once
    private bool _firedPlayerTurn = false;
    private bool _firedUnitSelected = false;
    private bool _firedSkillSelected = false;

    void Update()
    {
        if (session == null) return;

        // Only check when game is stable (not syncing/resolving)
        if (session.SyncState != SyncStateValue.Idle) return;

        // "PlayerTurn" — fires first time the decision window opens on player's turn
        if (!_firedPlayerTurn
            && session.Timeline.isPaused
            && session.IsMyTurn()
            && session.ownedReadyUnitIds.Count > 0)
        {
            _firedPlayerTurn = true;
            PlotDirector.Trigger("PlayerTurn");
        }

        // "UnitSelected" — fires when player selects a unit
        if (!_firedUnitSelected
            && session.selectedUnitId != -1)
        {
            _firedUnitSelected = true;
            PlotDirector.Trigger("UnitSelected");
        }

        // "UnitDeselected" — fires when player deselects (goes back to None)
        // Reset UnitSelected flag so it can fire again if needed
        if (_firedUnitSelected
            && session.selectedUnitId == -1)
        {
            _firedUnitSelected = false;
        }
    }

    // Call this from outside if you want to reset all triggers
    // (e.g. on scene reload)
    public void ResetAll()
    {
        _firedPlayerTurn = false;
        _firedUnitSelected = false;
        _firedSkillSelected = false;
    }
}