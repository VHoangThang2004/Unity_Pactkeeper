using UnityEngine;

public abstract class ServerPassiveEffectBase : EffectBase
{
    public override InstantType InstantType => InstantType.PassiveBuff;
    public abstract EffectType EffectType { get; }

    [Header("Duration")]
    public bool isPermanent = false;
    public int durationInstants = 1;

    // Modifies unit derived stats — called by UnitRecalculator
    public virtual void ApplyStatModifier(UnitData unit) { }

    // Returns 1 ResolveResult so client can sync unit data
    public abstract ResolveResult Apply(
        ServerMatchSession session,
        int sourceUnitId,
        Vector3Int target);
}