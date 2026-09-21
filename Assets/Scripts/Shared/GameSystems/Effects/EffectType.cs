// Shared/GameSystems/Effects/EffectType.cs
public enum EffectType
{
    Move,
    OnEnterRQ,
    PreMoveTiming,
    PostMoveTiming,
    Damage,
    PreDamageTiming,
    PostDamageTiming,
    Heal,
    PreHealTiming,
    PostHealTiming,
    PreCommandTiming,
    PostCommandTiming,
    Status,
    Passive,    // for PassiveBuff effects — client displays buff icon
    ApplyBuff
}