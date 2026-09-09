using UnityEngine;

/// <summary>
/// Server-only elite knowledge. Maps unit speed to step range via tiers.
/// Each tier defines a speed range and its corresponding step range.
/// A unit's speed falls into exactly one tier — that tier's formula applies.
/// Lives in Assets/Data/Server/ — never shipped to clients.
/// </summary>
[CreateAssetMenu(fileName = "SpeedConfig", menuName = "SRPG/Speed Config")]
public class SpeedConfig : ScriptableObject
{
    [System.Serializable]
    public struct SpeedTier
    {
        [Tooltip("Inclusive speed range for this tier. x = min, y = max.")]
        public Vector2Int speedRange;

        [Tooltip("Step range for this tier. x = low step, y = high step. y should equal x or x+1.")]
        public Vector2Int stepRange;
    }

    [Header("Speed Tiers (must be non-overlapping, ordered low to high)")]
    public SpeedTier[] tiers;

    // -------------------------------------------------------
    // Lookup
    // -------------------------------------------------------

    /// <summary>
    /// Finds the tier for the given speed and returns (lowStep, highStep).
    /// Falls back to the closest tier if speed is out of all ranges.
    /// </summary>
    public Vector2Int GetStepRange(int speed)
    {
        if (tiers == null || tiers.Length == 0)
        {
            Debug.LogError("[SpeedConfig] No tiers defined!");
            return new Vector2Int(8, 9); // safe fallback
        }

        // Find matching tier
        foreach (var tier in tiers)
        {
            if (speed >= tier.speedRange.x && speed <= tier.speedRange.y)
                return ComputeStepRange(speed, tier);
        }

        // Speed is out of all defined ranges — use nearest tier
        if (speed < tiers[0].speedRange.x)
        {
            Debug.LogWarning($"[SpeedConfig] Speed {speed} below all tiers — using lowest tier.");
            return ComputeStepRange(tiers[0].speedRange.x, tiers[0]);
        }

        Debug.LogWarning($"[SpeedConfig] Speed {speed} above all tiers — using highest tier.");
        var last = tiers[tiers.Length - 1];
        return ComputeStepRange(last.speedRange.y, last);
    }

    /// <summary>
    /// Gets the next reset step, alternating low/high within the matched tier.
    /// </summary>
    public int GetNextStep(int speed, bool useLow)
    {
        Vector2Int range = GetStepRange(speed);
        return useLow ? range.x : range.y;
    }

    // -------------------------------------------------------
    // Internal
    // -------------------------------------------------------

    private Vector2Int ComputeStepRange(int speed, SpeedTier tier)
    {
        int speedMin = tier.speedRange.x;
        int speedMax = tier.speedRange.y;

        // If tier has only one speed value, return step range directly
        if (speedMin == speedMax)
            return new Vector2Int(tier.stepRange.x, tier.stepRange.y);

        // Normalize speed within tier (higher speed = higher t)
        float t = (float)(speed - speedMin) / (speedMax - speedMin);

        // Map inversely — higher speed = lower step
        float stepFloat = Mathf.Lerp(tier.stepRange.y, tier.stepRange.x, t);

        int low  = Mathf.FloorToInt(stepFloat);
        int high = low + 1;

        // Clamp to tier's defined step range
        low  = Mathf.Clamp(low,  tier.stepRange.x, tier.stepRange.y);
        high = Mathf.Clamp(high, tier.stepRange.x, tier.stepRange.y);

        return new Vector2Int(low, high);
    }
}