// Server/GameSystems/Effects/ServerEffectBase.cs
using UnityEngine;

public abstract class ServerActiveEffectBase : EffectBase
{
    public abstract EffectType EffectType { get; }

    // Returns exactly 1 ResolveResult
    public abstract ResolveResult Apply(
        ServerMatchSession session,
        int sourceUnitId,
        Vector3Int target);
}