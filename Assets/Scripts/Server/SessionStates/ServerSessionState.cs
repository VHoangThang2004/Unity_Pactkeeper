/// <summary>
/// Server-only. Defines all possible states of a match session.
/// Drives sync decisions, input acceptance, and timeline flow.
/// </summary>
public enum ServerSessionState
{
    /// <summary>Match not started or not initialized.</summary>
    None,

    /// <summary>Timeline is ticking forward. Instants passing freely. Safest state for any sync tier.</summary>
    Flowing,

    /// <summary>An instant is frozen. One or more units hit step=0. Evaluating who needs to decide.</summary>
    InstantPaused,

    /// <summary>Waiting for a specific team to respond act or wait. Mid snapshot sent here.</summary>
    DecisionWaiting,

    /// <summary>Team chose to act. Waiting for their unit + target input. Soft input lock on other team.</summary>
    ActionWaiting,

    /// <summary>Action received and validated. Applying it server-side, clients animating. No sync allowed.</summary>
    Resolving,

    /// <summary>Match ended. Max instant reached or win condition met.</summary>
    Finished,
}