using UnityEditor;
using UnityEngine;
using TMPro;
using UnityEngine.Localization.Components;
using UnityEditor.Events;
using UnityEngine.Events;

public class AutoTranslateSetupEditor : Editor
{
    private const string TABLE_NAME = "UI_Table";

    [MenuItem("Tools/Localization/Auto Setup All Texts")]
    public static void AutoSetupTexts()
    {
        TMP_Text[] allTexts = Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include);
        int addedCount = 0;
        int skippedCount = 0;

        foreach (var txt in allTexts)
        {
            if (string.IsNullOrWhiteSpace(txt.text))
            {
                skippedCount++;
                continue;
            }

            string key = txt.text.Trim();

            var localizeEvent = txt.gameObject.GetComponent<LocalizeStringEvent>();
            if (localizeEvent != null)
            {
                skippedCount++;
                continue;
            }

            localizeEvent = txt.gameObject.AddComponent<LocalizeStringEvent>();
            localizeEvent.StringReference.TableReference = TABLE_NAME;
            localizeEvent.StringReference.TableEntryReference = key;

            // GetSetMethod() tìm setter của property text một cách chắc chắn qua API chuẩn
            var setterMethod = typeof(TMP_Text).GetProperty("text")?.GetSetMethod();
            UnityAction<string> methodDelegate = setterMethod != null
                ? System.Delegate.CreateDelegate(typeof(UnityAction<string>), txt, setterMethod) as UnityAction<string>
                : null;

            if (methodDelegate != null)
            {
                UnityEventTools.AddPersistentListener(localizeEvent.OnUpdateString, methodDelegate);
                localizeEvent.OnUpdateString.SetPersistentListenerState(
                    0, UnityEventCallState.EditorAndRuntime);
            }

            EditorUtility.SetDirty(txt.gameObject);
            addedCount++;
        }

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());

        Debug.Log($"[Auto Translate] Đã gắn cho {addedCount} Text. Bỏ qua {skippedCount}. Hãy bấm Ctrl+S!");
    }
}
