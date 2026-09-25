using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

/// <summary>
/// Applies saved locale on startup. Pair with DropdownLanguage (Auto Translate) for UI switching.
/// </summary>
public class LanguageSeclector : MonoBehaviour
{
    private const string PrefKey = "SelectedLocaleCode";

    void Start()
    {
        ApplySavedLocale();
    }

    public static void SaveLocale(Locale locale)
    {
        if (locale == null) return;
        PlayerPrefs.SetString(PrefKey, locale.Identifier.Code);
        PlayerPrefs.Save();
        LocalizationSettings.SelectedLocale = locale;
    }

    public static void ApplySavedLocale()
    {
        if (LocalizationSettings.AvailableLocales == null ||
            LocalizationSettings.AvailableLocales.Locales == null ||
            LocalizationSettings.AvailableLocales.Locales.Count == 0)
            return;

        string code = PlayerPrefs.GetString(PrefKey, "en");
        foreach (var locale in LocalizationSettings.AvailableLocales.Locales)
        {
            if (locale.Identifier.Code == code)
            {
                LocalizationSettings.SelectedLocale = locale;
                return;
            }
        }

        LocalizationSettings.SelectedLocale = LocalizationSettings.AvailableLocales.Locales[0];
    }
}