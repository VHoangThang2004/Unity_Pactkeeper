using System;
using UnityEngine;

public static class UnitAnimationSpeed
{
    public const float FastMultiplier = 2f;

    public static float Multiplier { get; private set; } = 1f;
    public static event Action Changed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Load()
    {
        Multiplier = PlayerPrefs.GetInt("BattleSpeedFast", 0) == 1 ? FastMultiplier : 1f;
    }

    public static void SetFast(bool fast)
    {
        Multiplier = fast ? FastMultiplier : 1f;
        PlayerPrefs.SetInt("BattleSpeedFast", fast ? 1 : 0);
        Changed?.Invoke();
    }

    public static bool IsFast => Multiplier > 1f;

    public static float Duration(float seconds)
    {
        return seconds / Mathf.Max(Multiplier, 0.01f);
    }
}
