// Client/GameSystems/Effects/ClientEffectBase.cs
using System.Collections;

public abstract class ClientActiveEffectBase : EffectBase
{
    // Shown in the unit's status strip / inspect panel when this effect is active on a unit (buffs, debuffs, passives)
    [UnityEngine.Header("Status Display")]
    public UnityEngine.Sprite statusIcon;
    public string statusName;
    public bool isDebuff;

    // Effects this one grants or applies (e.g. Tanker passive → Iron Body, ApplyBuff → its buffs).
    // Client-only mirror of the server link, used to describe the full chain once (ClientEffectRegistry.Describe).
    [UnityEngine.Tooltip("Effects this effect grants/applies — their descriptions are shown after this one")]
    public int[] relatedEffectIds = new int[0];

    // Replays the visual for this effect on the client
    // Called once per ResolveResult in order
    // If resolveDuration == 0f — skip animation, apply partial immediately
    public abstract string GetDescription(UnitData unit,ClientEffectRegistry effectRegistry);
    public abstract IEnumerator Replay(
        ResolveResult result,
        ClientScene scene,
        ClientMatchSession session);
}