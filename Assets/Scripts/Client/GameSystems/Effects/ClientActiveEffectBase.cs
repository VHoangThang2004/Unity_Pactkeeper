// Client/GameSystems/Effects/ClientEffectBase.cs
using System.Collections;

public abstract class ClientActiveEffectBase : EffectBase
{
    // Replays the visual for this effect on the client
    // Called once per ResolveResult in order
    // If resolveDuration == 0f — skip animation, apply partial immediately
    public abstract IEnumerator Replay(
        ResolveResult result,
        ClientScene scene,
        ClientMatchSession session);
}