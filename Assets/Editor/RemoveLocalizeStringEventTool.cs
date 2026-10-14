using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using TMPro;
using UnityEngine.Localization.Components;
using System.Collections.Generic;

public class RemoveLocalizeStringEventTool : EditorWindow
{
    [MenuItem("Tools/Localization/Remove LocalizeStringEvent from TMP")]
    public static void RemoveLocalizeStringEvents()
    {
        if (!EditorUtility.DisplayDialog("Confirm", "This will iterate through ALL SCENES in your project and remove LocalizeStringEvent components from any GameObject that has a TextMeshPro component. Make sure you have committed your changes to source control before doing this. Proceed?", "Yes", "Cancel"))
        {
            return;
        }

        string[] sceneGuids = AssetDatabase.FindAssets("t:Scene");
        string originalScene = SceneManager.GetActiveScene().path;
        bool originalSceneIsDirty = EditorSceneManager.GetActiveScene().isDirty;
        
        if (originalSceneIsDirty)
        {
            EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();
        }

        int removedCount = 0;
        int scenesModified = 0;

        for (int i = 0; i < sceneGuids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(sceneGuids[i]);
            
            // Skip scenes from packages
            if (path.StartsWith("Packages/")) continue;

            EditorUtility.DisplayProgressBar("Processing Scenes", $"Checking {path}", (float)i / sceneGuids.Length);

            Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            
            bool modifiedScene = false;

            // Get all root objects
            GameObject[] rootObjects = scene.GetRootGameObjects();
            
            foreach (var root in rootObjects)
            {
                // Find all LocalizeStringEvent in children (including inactive)
                var localizeEvents = root.GetComponentsInChildren<LocalizeStringEvent>(true);
                
                foreach (var locEvent in localizeEvents)
                {
                    // Check if it also has a TMP_Text component
                    if (locEvent.gameObject.GetComponent<TMP_Text>() != null)
                    {
                        Undo.RecordObject(locEvent.gameObject, "Remove LocalizeStringEvent");
                        DestroyImmediate(locEvent);
                        modifiedScene = true;
                        removedCount++;
                    }
                }
            }

            if (modifiedScene)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                scenesModified++;
            }
        }

        EditorUtility.ClearProgressBar();

        // Restore original scene
        if (!string.IsNullOrEmpty(originalScene))
        {
            EditorSceneManager.OpenScene(originalScene, OpenSceneMode.Single);
        }

        EditorUtility.DisplayDialog("Complete", $"Successfully removed {removedCount} LocalizeStringEvent components across {scenesModified} scenes.", "OK");
    }
}
