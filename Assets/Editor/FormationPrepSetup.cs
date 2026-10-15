using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One-click setup of FormationPrepManager + a basic prep UI in the open client match scene.
/// Builds: overlay canvas, left unit-list scroll view, top countdown, bottom-right Ready button,
/// and wires every reference (including ClientController.prep).
/// Restyle freely afterwards — only the references matter.
/// </summary>
public static class FormationPrepSetup
{
    const string UnitContainerPath = "Assets/Prefabs/UI/UnitContainer.prefab";
    const string BackendConfigPath = "Assets/Data/Client/ClientBackendConfig.asset";
    const string SceneConfigPath = "Assets/Data/Shared/SceneConfig.asset";
    const string ClassLibraryPath = "Assets/Data/Client/ClassDefinitionRegistry.asset";

    [MenuItem("Tools/Formation Prep/Setup In Open Scene")]
    static void Setup()
    {
        var controller = Object.FindAnyObjectByType<ClientController>(FindObjectsInactive.Include);
        var clientScene = Object.FindAnyObjectByType<ClientScene>(FindObjectsInactive.Include);
        var session = Object.FindAnyObjectByType<ClientMatchSession>(FindObjectsInactive.Include);
        var mapLoader = Object.FindAnyObjectByType<ClientMapLoader>(FindObjectsInactive.Include);
        if (controller == null || clientScene == null || session == null || mapLoader == null)
        {
            EditorUtility.DisplayDialog("Formation Prep", "Open a client match scene first (needs ClientController, ClientScene, ClientMatchSession, ClientMapLoader).", "OK");
            return;
        }
        if (Object.FindAnyObjectByType<FormationPrepManager>(FindObjectsInactive.Include) != null)
        {
            EditorUtility.DisplayDialog("Formation Prep", "This scene already has a FormationPrepManager.", "OK");
            return;
        }

        // Canvas (above the battle UI, below the 8_UnitConfig overlay — give that canvas a higher sort order)
        var canvasGo = new GameObject("FormationPrepCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        SceneManagerMoveTo(canvasGo, controller.gameObject);
        Undo.RegisterCreatedObjectUndo(canvasGo, "Formation Prep Setup");
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 20;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        // Panel: full screen, no graphic, so clicks on empty space reach the battlefield
        var panel = new GameObject("FormationPrepPanel", typeof(RectTransform));
        var panelRect = (RectTransform)panel.transform;
        panelRect.SetParent(canvasGo.transform, false);
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = panelRect.offsetMax = Vector2.zero;

        // Unit list (left column)
        var uiRes = new DefaultControls.Resources
        {
            standard = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd"),
            background = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd"),
            knob = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd"),
            mask = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UIMask.psd"),
        };
        var scrollGo = DefaultControls.CreateScrollView(uiRes);
        scrollGo.name = "UnitList";
        Place(scrollGo, panel.transform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(20, 0), new Vector2(ListWidth, 900));
        var scroll = scrollGo.GetComponent<ScrollRect>();
        scroll.horizontal = false;
        if (scroll.horizontalScrollbar != null) Object.DestroyImmediate(scroll.horizontalScrollbar.gameObject);
        scroll.horizontalScrollbar = null;
        var content = scroll.content;
        ApplyListLayout(content);

        // Countdown (top center)
        var tmpRes = new TMP_DefaultControls.Resources { standard = uiRes.standard };
        var countdownGo = TMP_DefaultControls.CreateText(tmpRes);
        countdownGo.name = "Countdown";
        Place(countdownGo, panel.transform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -20), new Vector2(300, 80));
        var countdown = countdownGo.GetComponent<TextMeshProUGUI>();
        countdown.text = "120";
        countdown.fontSize = 56;
        countdown.alignment = TextAlignmentOptions.Center;

        // Ready button (bottom right)
        var readyGo = TMP_DefaultControls.CreateButton(tmpRes);
        readyGo.name = "ReadyButton";
        Place(readyGo, panel.transform, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-40, 40), new Vector2(240, 80));
        var readyLabel = readyGo.GetComponentInChildren<TextMeshProUGUI>();
        readyLabel.text = "Ready";
        readyLabel.fontSize = 36;

        // Manager on the canvas root (stays active while the panel toggles)
        var mgr = Undo.AddComponent<FormationPrepManager>(canvasGo);
        var controllerSo = new SerializedObject(controller);
        var sceneConfig = controllerSo.FindProperty("sceneConfig").objectReferenceValue
                          ?? AssetDatabase.LoadAssetAtPath<SceneConfig>(SceneConfigPath);

        var so = new SerializedObject(mgr);
        so.FindProperty("scene").objectReferenceValue = clientScene;
        so.FindProperty("session").objectReferenceValue = session;
        so.FindProperty("mapLoader").objectReferenceValue = mapLoader;
        so.FindProperty("backendConfig").objectReferenceValue = AssetDatabase.LoadAssetAtPath<BackendConfig>(BackendConfigPath);
        so.FindProperty("sceneConfig").objectReferenceValue = sceneConfig;
        so.FindProperty("classLibrary").objectReferenceValue = AssetDatabase.LoadAssetAtPath<ClassDefinitionRegistry>(ClassLibraryPath);
        so.FindProperty("panel").objectReferenceValue = panel;
        so.FindProperty("unitListContainer").objectReferenceValue = content;
        so.FindProperty("unitContainerPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(UnitContainerPath);
        so.FindProperty("countdownText").objectReferenceValue = countdown;
        so.FindProperty("readyButton").objectReferenceValue = readyGo.GetComponent<Button>();
        so.ApplyModifiedProperties();

        controllerSo.FindProperty("prep").objectReferenceValue = mgr;
        controllerSo.ApplyModifiedProperties();

        EditorSceneManager.MarkSceneDirty(controller.gameObject.scene);
        Selection.activeGameObject = canvasGo;
        Debug.Log("[FormationPrepSetup] Done — check FormationPrepCanvas, then save the scene (Ctrl+S).");
    }

    // UnitContainer's root is 0x0 — it relies on a GridLayoutGroup sizing it (7_UnitList uses 250x300 cells)
    static readonly Vector2 CardSize = new Vector2(250, 300);
    const float ListWidth = 300f; // card + padding + vertical scrollbar

    static void ApplyListLayout(RectTransform content)
    {
        foreach (var old in content.GetComponents<HorizontalOrVerticalLayoutGroup>())
            Undo.DestroyObjectImmediate(old);

        if (!content.TryGetComponent<GridLayoutGroup>(out var grid)) grid = Undo.AddComponent<GridLayoutGroup>(content.gameObject);
        Undo.RecordObject(grid, "Unit List Layout");
        grid.cellSize = CardSize;
        grid.spacing = new Vector2(0, 20);
        grid.padding = new RectOffset(10, 10, 10, 10);
        grid.childAlignment = TextAnchor.UpperCenter;
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 1;

        if (!content.TryGetComponent<ContentSizeFitter>(out var fitter)) fitter = Undo.AddComponent<ContentSizeFitter>(content.gameObject);
        Undo.RecordObject(fitter, "Unit List Layout");
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
    }

    [MenuItem("Tools/Formation Prep/Fix Unit List Layout")]
    static void FixListLayout()
    {
        var mgr = Object.FindAnyObjectByType<FormationPrepManager>(FindObjectsInactive.Include);
        var content = mgr != null ? new SerializedObject(mgr).FindProperty("unitListContainer").objectReferenceValue as RectTransform : null;
        if (content == null)
        {
            EditorUtility.DisplayDialog("Formation Prep", "No FormationPrepManager with a unit list container in the open scene.", "OK");
            return;
        }

        ApplyListLayout(content);

        var scroll = content.GetComponentInParent<ScrollRect>();
        if (scroll != null)
        {
            var rt = (RectTransform)scroll.transform;
            Undo.RecordObject(rt, "Fix Unit List Layout");
            rt.sizeDelta = new Vector2(ListWidth, rt.sizeDelta.y);
        }

        EditorSceneManager.MarkSceneDirty(mgr.gameObject.scene);
        Debug.Log("[FormationPrepSetup] Unit list now uses 250x300 grid cells like 7_UnitList — save the scene (Ctrl+S).");
    }

    static void SceneManagerMoveTo(GameObject go, GameObject sceneObject) =>
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, sceneObject.scene);

    static void Place(GameObject go, Transform parent, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = pivot;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
    }
}
