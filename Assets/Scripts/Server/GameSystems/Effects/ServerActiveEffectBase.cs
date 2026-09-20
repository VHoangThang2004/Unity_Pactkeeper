// Server/GameSystems/Effects/ServerEffectBase.cs
using UnityEngine;

public abstract class ServerActiveEffectBase : EffectBase
{
    public abstract EffectType EffectType { get; }

    // Returns exactly 1 ResolveResult
    // Instant and half instant effect can ignore the sourceCell value
    // Half instant effect must offset the value from source cell to unit.currentCell and dertemine the target cell
    public abstract ResolveResult Apply(
        ServerMatchSession session,
        int sourceUnitId,
        Vector3Int sourceCell,
        Vector3Int target);
}