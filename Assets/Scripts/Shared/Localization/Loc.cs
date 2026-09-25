using System;
using UnityEngine;
using UnityEngine.Localization.Settings;

/// <summary>
/// Runtime helper for localized strings. Falls back to the key if table/entry is missing.
/// </summary>
public static class Loc
{
    public static string Get(string table, string key)
    {
        if (string.IsNullOrEmpty(table) || string.IsNullOrEmpty(key))
            return key ?? string.Empty;

        try
        {
            if (!LocalizationSettings.HasSettings)
                return key;

            var result = LocalizationSettings.StringDatabase.GetLocalizedString(table, key);
            return string.IsNullOrEmpty(result) ? key : result;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[Loc] Missing '{table}/{key}': {ex.Message}");
            return key;
        }
    }

    public static string Format(string table, string key, params object[] args)
    {
        var template = Get(table, key);
        try
        {
            return string.Format(template, args);
        }
        catch (FormatException)
        {
            Debug.LogWarning($"[Loc] Format failed for '{table}/{key}'");
            return template;
        }
    }
}
