using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

/// <summary>
/// TMP_Dropdown language switcher for Login / settings UI.
/// Pairs with LanguageSeclector (same PlayerPrefs key).
/// </summary>
[RequireComponent(typeof(TMP_Dropdown))]
public class LanguageDropdown : MonoBehaviour
{
    [SerializeField] private TMP_Dropdown dropdown;

    private List<Locale> locales = new();

    void Awake()
    {
        if (dropdown == null)
            dropdown = GetComponent<TMP_Dropdown>();
    }

    void Start()
    {
        StartCoroutine(InitWhenReady());
    }

    IEnumerator InitWhenReady()
    {
        while (!LocalizationSettings.HasSettings)
            yield return null;

        locales = LocalizationSettings.AvailableLocales.Locales;
        if (locales == null || locales.Count == 0)
            yield break;

        LanguageSeclector.ApplySavedLocale();

        dropdown.onValueChanged.RemoveListener(OnDropdownChanged);
        dropdown.ClearOptions();
        dropdown.AddOptions(locales.Select(l => l.LocaleName).ToList());
        dropdown.SetValueWithoutNotify(GetCurrentLocaleIndex());
        dropdown.onValueChanged.AddListener(OnDropdownChanged);
    }

    void OnDropdownChanged(int index)
    {
        if (index < 0 || index >= locales.Count) return;
        LanguageSeclector.SaveLocale(locales[index]);
    }

    int GetCurrentLocaleIndex()
    {
        var selected = LocalizationSettings.SelectedLocale;
        if (selected == null) return 0;

        for (int i = 0; i < locales.Count; i++)
        {
            if (locales[i].Identifier.Code == selected.Identifier.Code)
                return i;
        }

        return 0;
    }
}
