using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro; 
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

public class DropdownLanguage : MonoBehaviour
{
    [SerializeField] private TMP_Dropdown dropdown;

    private IEnumerator Start()
    {
        // 1. Chờ hệ thống Localization khởi tạo xong để tránh lỗi NullReference
        yield return LocalizationSettings.InitializationOperation;

        if (dropdown == null)
            dropdown = GetComponent<TMP_Dropdown>();

        // 2. Tạo danh sách các lựa chọn (Options) cho Dropdown dựa trên số lượng Locales bạn có
        var options = new List<TMP_Dropdown.OptionData>();
        int selectedIndex = 0;

        for (int i = 0; i < LocalizationSettings.AvailableLocales.Locales.Count; ++i)
        {
            var locale = LocalizationSettings.AvailableLocales.Locales[i];

            // Kiểm tra xem ngôn ngữ nào đang được chọn hiện tại để gán cho Dropdown
            if (LocalizationSettings.SelectedLocale == locale)
                selectedIndex = i;

            // Thêm tên của Locale (ví dụ: English (en), Vietnamese (vi)) vào Dropdown
            options.Add(new TMP_Dropdown.OptionData(locale.Formatter.ToString()));
        }

        // 3. Cập nhật Dropdown UI
        dropdown.options = options;
        dropdown.value = selectedIndex;

        // 4. Lắng nghe sự kiện khi người dùng chọn một ngôn ngữ khác trên Dropdown
        dropdown.onValueChanged.AddListener(OnLocaleSelected);
    }

    private void OnLocaleSelected(int index)
    {
        // 5. Thay đổi ngôn ngữ hiện tại của game
        Locale locale = LocalizationSettings.AvailableLocales.Locales[index];
        LocalizationSettings.SelectedLocale = locale;

        // 6. Gọi hàm SaveLocale từ file LanguageSeclector.cs CỦA BẠN để lưu ngôn ngữ vào PlayerPrefs
        LanguageSeclector.SaveLocale(locale);
    }
}
