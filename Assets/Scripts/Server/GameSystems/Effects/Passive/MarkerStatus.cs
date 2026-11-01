// Server/GameSystems/Effects/Passive/MarkerStatus.cs
using UnityEngine;

/// <summary>
/// A status with no behaviour of its own — just a visible icon + stack count driven by another effect
/// (e.g. the Archer's attack counter). The owning passive sets its stacks and removes it.
/// </summary>
[CreateAssetMenu(fileName = "MarkerStatus", menuName = "SRPG/Effects/Server/Passive/MarkerStatus")]
public class MarkerStatus : ServerPassiveEffectBase
{
    public override EffectType EffectType => EffectType.Passive;

    public override ResolveResult Apply(ServerMatchSession session, int sourceUnitId, Vector3Int target)
        => new ResolveResult { EffectId = -1 }; // passive — never directly applied
}
