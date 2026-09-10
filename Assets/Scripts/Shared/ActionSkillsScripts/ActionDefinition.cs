using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Designer-controlled action definition. Lives in Assets/Data/Actions/.
/// Referenced by skillCardId — both server and client know this definition.
/// Step cost comes from effects, not from the action itself.
/// Pattern mirrors UnitDefinition.
/// </summary>
[CreateAssetMenu(fileName = "Action_", menuName = "SRPG/Action Definition")]
public class ActionDefinition : ScriptableObject
{
    [Header("Identity")]
    public int actionId;
    public string actionName;
    public int range;
    public GridPathfinder.FloodFillTarget targeting; // define target (empty cell/enemy cell/ ally cell/ any cell)
    public bool isUnblockable = false; // use when floodfilling, if is unblockable means this skill can reach through occupied cells.

    [Header("Effects")]
    [Tooltip("Effects executed when this action resolves. Order matters for instant effects.")]
    public EffectBase[] effects;

    /// <summary>
    /// True if ANY effect is non-instant — unit is removed from ready queue.
    /// </summary>
    public bool HasNonInstantEffect()
    {
        foreach (var effect in effects)
            if (effect != null && !effect.IsInstant)
                return true;
        return false;
    }
}