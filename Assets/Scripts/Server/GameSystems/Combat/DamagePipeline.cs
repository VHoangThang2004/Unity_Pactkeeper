// Server/GameSystems/Combat/DamagePipeline.cs
// The one place damage is computed and applied. Server only — formulas never reach the client.
// Integer percent math, one division at the end:
// Final = max(1, min(Cap, floor(base × mult% × Π(passive %) × (100 − reduction%) / (10000 × 100^n)))).

public struct DamageContext
{
    public UnitData Source;
    public UnitData Target;
    public int Base;              // effect baseValue
    public long MultNum, MultDen; // product of passive multipliers as a fraction (1/1 = none); use Multiply()
    public int ReductionPercent;  // target's DamageReduction, clamped to 0..MaxReductionPercent
    public int Cap;               // max damage this hit; int.MaxValue = no cap. Lowest cap wins (use Min when setting)
    public int Final;             // HP actually removed

    // Passive damage multiplier, e.g. 200 = ×2. Multipliers stack multiplicatively (×2 then ×2 = ×4).
    public void Multiply(int percent)
    {
        MultNum *= percent;
        MultDen *= 100;
        long g = Gcd(MultNum, MultDen); // keep the fraction reduced so many stacks can't overflow (×2 ten times = 1024/1)
        MultNum /= g;
        MultDen /= g;
    }

    static long Gcd(long a, long b)
    {
        while (b != 0) (a, b) = (b, a % b);
        return a == 0 ? 1 : System.Math.Abs(a);
    }
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
            MultNum = 1,
            MultDen = 1,
        };

        PassiveHooks.PreDamage(session, ref ctx); // passives adjust the inputs before the single final division

        long raw = (long)ctx.Base * source.DamageMultiplier * (100 - ctx.ReductionPercent) * ctx.MultNum
                   / (10000L * ctx.MultDen);
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
