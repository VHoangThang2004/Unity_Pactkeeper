using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Fixes the "wrong spacing until the panel is opened a second time" bug in scroll contents.
/// Cause: nested ContentSizeFitters (Content has one, and so does each text inside it). Unity sizes the parent
/// before the children's fitters update, so the first layout pass uses stale child heights.
/// Fix (per Content that has ContentSizeFitter + VerticalLayoutGroup):
///   - the layout group controls child height (and doesn't force-expand it),
///   - child ContentSizeFitters are removed (the group now reads TMP preferred height directly),
///   - children with no height source of their own (e.g. pattern containers) get a LayoutElement
///     with their current height, so they don't collapse to 0.
/// </summary>
public static class NestedLayoutFixTool
{
    [MenuItem("Tools/UI/Fix Nested Layout In Open Scene")]
    static void Fix()
    {
        int contents = 0, removed = 0, pinned = 0;

        foreach (var group in Object.FindObjectsByType<VerticalLayoutGroup>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (!group.TryGetComponent<ContentSizeFitter>(out var parentFitter)) continue;
            contents++;

            Undo.RecordObject(group, "Fix Nested Layout");
            group.childControlHeight = true;
            group.childForceExpandHeight = false;

            Undo.RecordObject(parentFitter, "Fix Nested Layout");
            parentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            foreach (Transform child in group.transform)
            {
                if (child.TryGetComponent<ContentSizeFitter>(out var childFitter))
                {
                    Undo.DestroyObjectImmediate(childFitter);
                    removed++;
                }

                bool hasHeightSource = child.GetComponent<TMP_Text>() != null
                                       || child.GetComponent<LayoutGroup>() != null
                                       || child.GetComponent<LayoutElement>() != null;
                if (hasHeightSource) continue;

                var element = Undo.AddComponent<LayoutElement>(child.gameObject);
                element.preferredHeight = ((RectTransform)child).rect.height;
                pinned++;
            }
        }

        EditorSceneManager.MarkAllScenesDirty();
        Debug.Log($"[NestedLayoutFix] {contents} scroll contents fixed: {removed} nested ContentSizeFitters removed, " +
                  $"{pinned} children pinned to their current height. Check the panels, then save the scene (Ctrl+S).");
    }
}
