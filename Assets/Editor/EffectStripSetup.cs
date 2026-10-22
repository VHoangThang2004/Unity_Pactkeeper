using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One-click setup of UnitEffectStrip (status icons for the selected unit) in the open client match scene.
/// Creates Assets/Prefabs/UI/EffectIcon.prefab and a strip just above the skill slots. Restyle freely afterwards.
/// </summary>
public static class EffectStripSetup
{
    const string IconPrefabPath = "Assets/Prefabs/UI/EffectIcon.prefab";
    const float IconSize = 56f;

    [MenuItem("Tools/Status Effects/Setup Effect Strip In Open Scene")]
    static void Setup()
    {
        var clientScene = Object.FindAnyObjectByType<ClientScene>(FindObjectsInactive.Include);
        var session = Object.FindAnyObjectByType<ClientMatchSession>(FindObjectsInactive.Include);
        if (clientScene == null || session == null)
        {
            EditorUtility.DisplayDialog("Effect Strip", "Open a client match scene first (needs ClientScene + ClientMatchSession).", "OK");
            return;
        }
        if (Object.FindAnyObjectByType<UnitEffectStrip>(FindObjectsInactive.Include) != null)
        {
            EditorUtility.DisplayDialog("Effect Strip", "This scene already has a UnitEffectStrip.", "OK");
            return;
        }

        // Sit next to the skill slots so it reads as part of the selected unit's info
        Transform anchorSlot = clientScene.mainWeaponSkillSlot != null ? clientScene.mainWeaponSkillSlot.transform : null;
        Canvas canvas = anchorSlot != null ? anchorSlot.GetComponentInParent<Canvas>().rootCanvas
                                           : Object.FindAnyObjectByType<Canvas>();
        if (canvas == null)
        {
            EditorUtility.DisplayDialog("Effect Strip", "No Canvas found in the scene.", "OK");
            return;
        }

        var root = new GameObject("UnitEffectStrip", typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(root, "Effect Strip Setup");
        var rootRect = (RectTransform)root.transform;
        rootRect.SetParent(canvas.transform, false);
        rootRect.anchorMin = rootRect.anchorMax = new Vector2(0.5f, 0);
        rootRect.pivot = new Vector2(0.5f, 0);
        rootRect.sizeDelta = new Vector2(600, IconSize);
        if (anchorSlot is RectTransform slotRect)
        {
            // Place the strip right above the weapon slot, in canvas space
            Vector3 world = slotRect.TransformPoint(new Vector3(0, slotRect.rect.yMax, 0));
            rootRect.position = world + new Vector3(0, 8f * canvas.scaleFactor, 0);
        }
        else rootRect.anchoredPosition = new Vector2(0, 160);

        // Icon row (child, so the strip can hide it without disabling itself)
        var row = new GameObject("Icons", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        var rowRect = (RectTransform)row.transform;
        rowRect.SetParent(rootRect, false);
        rowRect.anchorMin = Vector2.zero;
        rowRect.anchorMax = Vector2.one;
        rowRect.offsetMin = rowRect.offsetMax = Vector2.zero;
        var layout = row.GetComponent<HorizontalLayoutGroup>();
        layout.spacing = 6;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = layout.childControlHeight = false;
        layout.childForceExpandWidth = layout.childForceExpandHeight = false;

        var strip = Undo.AddComponent<UnitEffectStrip>(root);
        var so = new SerializedObject(strip);
        so.FindProperty("session").objectReferenceValue = session;
        so.FindProperty("scene").objectReferenceValue = clientScene;
        so.FindProperty("iconContainer").objectReferenceValue = rowRect;
        so.FindProperty("iconPrefab").objectReferenceValue = GetOrCreateIconPrefab();
        so.ApplyModifiedProperties();

        EditorSceneManager.MarkSceneDirty(root.scene);
        Selection.activeGameObject = root;
        Debug.Log("[EffectStripSetup] Done — position UnitEffectStrip where you like, then save the scene (Ctrl+S).");
    }

    static EffectIconUI GetOrCreateIconPrefab()
    {
        var existing = AssetDatabase.LoadAssetAtPath<EffectIconUI>(IconPrefabPath);
        if (existing != null) return existing;

        var go = new GameObject("EffectIcon", typeof(RectTransform), typeof(Image), typeof(Button));
        ((RectTransform)go.transform).sizeDelta = new Vector2(IconSize, IconSize);
        var frame = go.GetComponent<Image>();
        frame.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        frame.type = Image.Type.Sliced;
        frame.color = new Color(0f, 0f, 0f, 0.55f);

        var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        var iconRect = (RectTransform)iconGo.transform;
        iconRect.SetParent(go.transform, false);
        iconRect.anchorMin = Vector2.zero;
        iconRect.anchorMax = Vector2.one;
        iconRect.offsetMin = new Vector2(4, 4);
        iconRect.offsetMax = new Vector2(-4, -4);
        var iconImg = iconGo.GetComponent<Image>();
        iconImg.preserveAspect = true;
        iconImg.raycastTarget = false;

        var textGo = new GameObject("Remaining", typeof(RectTransform), typeof(TextMeshProUGUI));
        var textRect = (RectTransform)textGo.transform;
        textRect.SetParent(go.transform, false);
        textRect.anchorMin = textRect.anchorMax = textRect.pivot = new Vector2(1, 0);
        textRect.anchoredPosition = new Vector2(-2, 0);
        textRect.sizeDelta = new Vector2(30, 24);
        var text = textGo.GetComponent<TextMeshProUGUI>();
        text.fontSize = 20;
        text.fontStyle = FontStyles.Bold;
        text.alignment = TextAlignmentOptions.BottomRight;
        text.outlineWidth = 0.25f;
        text.raycastTarget = false;

        var ui = go.AddComponent<EffectIconUI>();
        var so = new SerializedObject(ui);
        so.FindProperty("icon").objectReferenceValue = iconImg;
        so.FindProperty("remainingText").objectReferenceValue = text;
        so.FindProperty("button").objectReferenceValue = go.GetComponent<Button>();
        so.ApplyModifiedPropertiesWithoutUndo();
        AddStacksText(ui);

        var prefab = PrefabUtility.SaveAsPrefabAsset(go, IconPrefabPath);
        Object.DestroyImmediate(go);
        return prefab.GetComponent<EffectIconUI>();
    }

    // Adds the stack count to an icon prefab created before stacks existed
    [MenuItem("Tools/Status Effects/Add Stack Count To Effect Icon Prefab")]
    static void UpgradeIconPrefab()
    {
        if (AssetDatabase.LoadAssetAtPath<EffectIconUI>(IconPrefabPath) == null)
        {
            EditorUtility.DisplayDialog("Effect Icon", $"No prefab at {IconPrefabPath}. Run the strip setup first.", "OK");
            return;
        }

        var root = PrefabUtility.LoadPrefabContents(IconPrefabPath);
        var ui = root.GetComponent<EffectIconUI>();
        bool added = new SerializedObject(ui).FindProperty("stacksText").objectReferenceValue == null;
        if (added) AddStacksText(ui);
        PrefabUtility.SaveAsPrefabAsset(root, IconPrefabPath);
        PrefabUtility.UnloadPrefabContents(root);

        Debug.Log(added ? "[EffectStripSetup] Stack count added to EffectIcon prefab." : "[EffectStripSetup] EffectIcon prefab already has a stack count.");
    }

    // Big centred number over the icon; EffectIconUI leaves it empty when stacks <= 1
    static void AddStacksText(EffectIconUI ui)
    {
        var go = new GameObject("Stacks", typeof(RectTransform), typeof(TextMeshProUGUI));
        var rect = (RectTransform)go.transform;
        rect.SetParent(ui.transform, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;

        var text = go.GetComponent<TextMeshProUGUI>();
        text.text = "";
        text.fontSize = 28;
        text.fontStyle = FontStyles.Bold;
        text.alignment = TextAlignmentOptions.Center;
        text.outlineWidth = 0.3f;
        text.raycastTarget = false;

        var so = new SerializedObject(ui);
        so.FindProperty("stacksText").objectReferenceValue = text;
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
