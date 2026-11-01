using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Adds a VfxPlayer (with Prefabs/VFXs/VFXSprite.prefab) to the open client battle scene and wires ClientScene.vfx.
/// Run once per client battle scene (5_ClientMatch, story battle scenes).
/// </summary>
public static class VfxPlayerSetup
{
    const string VfxPrefabPath = "Assets/Prefabs/VFXs/VFXSprite.prefab";

    [MenuItem("Tools/VFX/Setup VfxPlayer In Open Scene")]
    static void Setup()
    {
        var clientScene = Object.FindAnyObjectByType<ClientScene>(FindObjectsInactive.Include);
        if (clientScene == null)
        {
            EditorUtility.DisplayDialog("VfxPlayer", "Open a client battle scene first (needs a ClientScene).", "OK");
            return;
        }

        var player = Object.FindAnyObjectByType<VfxPlayer>(FindObjectsInactive.Include);
        if (player == null)
        {
            var go = new GameObject("VfxPlayer");
            Undo.RegisterCreatedObjectUndo(go, "VfxPlayer Setup");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, clientScene.gameObject.scene);
            player = Undo.AddComponent<VfxPlayer>(go);
        }

        var playerSo = new SerializedObject(player);
        playerSo.FindProperty("vfxPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(VfxPrefabPath);
        playerSo.ApplyModifiedProperties();

        var sceneSo = new SerializedObject(clientScene);
        sceneSo.FindProperty("vfx").objectReferenceValue = player;
        sceneSo.ApplyModifiedProperties();

        EditorSceneManager.MarkSceneDirty(clientScene.gameObject.scene);
        Selection.activeGameObject = player.gameObject;
        Debug.Log("[VfxPlayerSetup] VfxPlayer wired to ClientScene.vfx — save the scene (Ctrl+S).");
    }
}
