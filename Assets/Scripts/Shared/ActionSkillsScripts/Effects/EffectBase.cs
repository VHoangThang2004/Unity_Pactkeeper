using System.Collections;
using UnityEngine;

/// <summary>
/// Base class for all action effects.
/// Server executes Apply() to mutate game state and produce a result.
/// Client uses the result to replay the animation/visual.
/// StepCostMultiplier — each effect carries its own step cost.
/// Total step cost per unit = sum of all effect multipliers from actionRecord.
/// </summary>
public abstract class EffectBase : ScriptableObject
{
    [Header("Step Cost")]
    [Tooltip("Step cost contribution of this effect. Summed across all effects in actionRecord for the unit.")]
    public float stepCostMultiplier = 1f;
    public float resolveDuration = 1f;


    /// <summary>
    /// True = resolve immediately when action is decided.
    /// False = queue to effectRecord, resolve at end of instant by speed order.
    /// </summary>
    public abstract bool IsInstant { get; }

    /// <summary>
    /// Execute this effect on the server.
    /// Mutates session state and returns what happened for client replay.
    /// </summary>// Server side — computes outcome, mutates session
    public abstract EffectResult Apply(ServerMatchSession session, int sourceUnitId, Vector3Int target);

    // Client side — replays outcome visually using the result data
    public abstract IEnumerator Replay(ClientUnit unit, EffectResult result, ClientScene scene,ClientMatchSession session);

    /// <summary>
    /// Actual step cost multiplier — override for dynamic costs (e.g. MoveEffect uses pathLength/moveRange).
    /// Default returns the designer-set value.
    /// </summary>
    public virtual float GetStepMultiplier(EffectResult result, UnitData unit)
    {
        return stepCostMultiplier;
    }
}