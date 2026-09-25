using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.UI;

namespace EqualchanceGames.Tools.AutoTranslate.UI
{
	public class DropdownLanguage : MonoBehaviour
	{
		private const string PrefKey = "SelectedLocaleCode";

		[SerializeField] private Dropdown dropdown;

		private List<Locale> Locales;

		private void Start()
		{
			dropdown.onValueChanged.AddListener(Dropdown_Change);
			dropdown.ClearOptions();

			Locales = LocalizationSettings.AvailableLocales.Locales;
			if ( Locales != null && Locales.Count != 0)
			dropdown.AddOptions(Locales.Select(w => w.LocaleName).ToList());
		}

		private void Dropdown_Change(int index)
		{
			if (index >= Locales.Count) return;

			var locale = Locales[index];
			PlayerPrefs.SetString(PrefKey, locale.Identifier.Code);
			PlayerPrefs.Save();
			LocalizationSettings.SelectedLocale = locale;
		}
	}
}
