using UnityEngine;

[CreateAssetMenu(fileName = "MatchConfig", menuName = "SRPG/Match Config")]
public class MatchConfig : ScriptableObject
{
    [Header("Timeline")]
    public int maxInstant = 100;
    public float instantDuration = 2f;
    public float effectsChangedWaitDuration = 2f;

    [Header("Decision Timeouts")]
    public float actWaitWindowPerInstantPerReadyUnit = 15f;
    public int maxWaitDurationMultiplier = 5;
    public float overtimePerTeam = 200f;

    [Header("SP Regen")]
    public int conseRegenCap = 3;

    [Header("Command Log")]
    public int maxLogCount = 10;

    [Header("Session")]
    public int requiredClients = 2;
    public float postInitGracePeriod = 3f;
}