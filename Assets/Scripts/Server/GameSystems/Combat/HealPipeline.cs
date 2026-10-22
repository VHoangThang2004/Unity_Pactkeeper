// Server/GameSystems/Combat/HealPipeline.cs
// The one place healing is computed and applied. Server only.
// B1b: today's exact math. Passive hooks (OnHealDone) are added in a later step.

public struct HealContext
{
    public UnitData Source;
    public UnitData Target;
    public int Amount;    // heal power before the MaxHP clamp
    public int Restored;  // HP actually gained (0 when the target was already full)
}

public static class HealPipeline
{
    // Side-effect free
    public static HealContext Compute(UnitData source, UnitData target, int baseValue)
    {
        return new HealContext
        {
            Source = source,
            Target = target,
            Amount = source.DamageMultiplier * baseValue / 100, // same formula as before: heal scales with DamageMultiplier
        };
    }

    public static void Apply(ServerMatchSession session, ref HealContext ctx)
    {
        int before = ctx.Target.CurrentHP;
        ctx.Target.CurrentHP = System.Math.Min(before + ctx.Amount, ctx.Target.MaxHP);
        ctx.Restored = ctx.Target.CurrentHP - before;
        PassiveHooks.HealDone(session, ctx);
    }
}
