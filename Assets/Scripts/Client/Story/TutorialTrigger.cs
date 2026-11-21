using UnityEngine;

/// <summary>
/// Watches local match state each frame and fires PlotDirector triggers
/// at meaningful game moments across Scripted Tutorials, Hybrid AI stages, and Challenges.
/// </summary>
public class TutorialTrigger : MonoBehaviour
{
    [SerializeField] private ClientMatchSession session;
    [SerializeField] private ClientInteractionSystem interactionSystem;

    // One-shot trigger guards per battle
    private bool _firedPlayerTurn = false;
    private bool _firedUnitSelected = false;
    private bool _firedEnemyActed = false;
    private bool _firedAllyLowHP = false;
    private bool _firedAllyFallen = false;

    private int _initialAllyCount = -1;
    private bool _sawEnemyActionInResolve = false;

    public void BindBattleReferences(ClientMatchSession matchSession, ClientInteractionSystem interaction)
    {
        if (matchSession != null) session = matchSession;
        if (interaction != null) interactionSystem = interaction;
    }

    void Update()
    {
        if (session == null)
        {
            session = FindAnyObjectByType<ClientMatchSession>();
            if (session == null) return;
        }
        if (interactionSystem == null)
            interactionSystem = FindAnyObjectByType<ClientInteractionSystem>();

        // Detect whether the enemy team performed an action during the resolving phase (only during Free Play)
        if (TutorialContext.IsFreePlayBattle && !_firedEnemyActed && session.LastResolve.HasResolve && session.LastResolve.ResolveResults != null)
        {
            foreach (var res in session.LastResolve.ResolveResults)
            {
                if (res.SourceUnitId > 0 && !session.IsMyUnit(res.SourceUnitId))
                {
                    _sawEnemyActionInResolve = true;
                    break;
                }
            }
        }

        // Only fire UI/plot triggers when game is stable (not syncing/resolving)
        if (session.SyncState != SyncStateValue.Idle) return;

        var ownedTeam = session.GetOwnedTeamData();
        if (ownedTeam != null && _initialAllyCount < 0 && ownedTeam.unitIds != null && ownedTeam.unitIds.Count > 0)
            _initialAllyCount = ownedTeam.unitIds.Count;

        // "PlayerTurn" - fires first time the decision window opens on player's turn
        if (!_firedPlayerTurn
            && session.Timeline.isPaused
            && session.IsMyTurn()
            && session.ownedReadyUnitIds.Count > 0)
        {
            _firedPlayerTurn = true;
            PlotDirector.Trigger("PlayerTurn");
        }

        // Reactive coaching triggers only fire once the stage has entered Free Play
        if (TutorialContext.IsFreePlayBattle)
        {
            // "EnemyActed" - fires once after an enemy unit acts and control settles back cleanly
            if (!_firedEnemyActed && _sawEnemyActionInResolve && session.Timeline.isPaused && session.IsMyTurn())
            {
                _firedEnemyActed = true;
                PlotDirector.Trigger("EnemyActed");
            }

            // "AllyLowHP" & "AllyFallen" - reactive combat triggers for Hybrid/Challenge tips
            if (ownedTeam != null && ownedTeam.unitIds != null)
            {
                if (!_firedAllyFallen && _initialAllyCount > 0 && ownedTeam.unitIds.Count < _initialAllyCount)
                {
                    _firedAllyFallen = true;
                    PlotDirector.Trigger("AllyFallen");
                }

                if (!_firedAllyLowHP)
                {
                    for (int i = 0; i < ownedTeam.unitIds.Count; i++)
                    {
                        var unit = session.GetUnitDataById(ownedTeam.unitIds[i]);
                        if (unit != null && unit.MaxHP > 1 && unit.CurrentHP > 0 && unit.CurrentHP <= 1)
                        {
                            _firedAllyLowHP = true;
                            PlotDirector.Trigger("AllyLowHP");
                            break;
                        }
                    }
                }
            }
        }

        // "UnitSelected" - fires when player selects a unit
        if (!_firedUnitSelected && session.selectedUnitId != -1)
        {
            _firedUnitSelected = true;
            PlotDirector.Trigger("UnitSelected");
        }

        // Reset UnitSelected flag on deselect
        if (_firedUnitSelected && session.selectedUnitId == -1)
        {
            _firedUnitSelected = false;
        }
    }

    public void ResetAll()
    {
        _firedPlayerTurn = false;
        _firedUnitSelected = false;
        _firedEnemyActed = false;
        _firedAllyLowHP = false;
        _firedAllyFallen = false;
        _initialAllyCount = -1;
        _sawEnemyActionInResolve = false;
    }
}
