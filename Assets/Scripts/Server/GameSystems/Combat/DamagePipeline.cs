// Server/GameSystems/Combat/DamagePipeline.cs
// The one place damage is computed and applied. Server only — formulas never reach the client.
// Integer percent math, one division at the end:
// Final = max(1, min(Cap, floor(base × mult% × (100 − reduction%) / 10000))).

public struct DamageContext
{
    public UnitData Source;
    public UnitData Target;
    public int Base;              // effect baseValue
    public int ReductionPercent;  // target's DamageReduction, clamped to 0..MaxReductionPercent
    public int Cap;               // max damage this hit; int.MaxValue = no cap. Lowest cap wins (use Min when setting)
    public int Final;             // HP actually removed
}

public static class DamagePipeline
{
    public const int MaxReductionPercent = 90;

    // Side-effect free (hooks must not change game state here): safe to call for previews (future AI)
    public static DamageContext Compute(ServerMatchSession session, UnitData source, UnitData target, int baseValue)
    {
        var ctx = new DamageContext
        {
            Source = source,
            Target = target,
            Base = baseValue,
            ReductionPercent = System.Math.Clamp(target.DamageReduction, 0, MaxReductionPercent),
            Cap = int.MaxValue,
        };

        PassiveHooks.PreDamage(session, ref ctx); // passives adjust the inputs before the single final division

        long raw = (long)ctx.Base * source.DamageMultiplier * (100 - ctx.ReductionPercent) / 10000;
        raw = System.Math.Min(raw, ctx.Cap);
        ctx.Final = (int)System.Math.Max(1, raw); // no hit ever deals 0 (a future full block/immunity must bypass this)
        return ctx;
    }

    // Returns true if the target died
    public static bool Apply(ServerMatchSession session, ref DamageContext ctx)
    {
        ctx.Target.CurrentHP -= ctx.Final;
        PassiveHooks.PostDamage(session, ctx); // before Kill, so the target's effects still exist
        if (ctx.Target.CurrentHP > 0) return false;

        session.Kill(ctx.Target.Id);
        return true;
    }
}
