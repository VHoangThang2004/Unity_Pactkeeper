using UnityEngine;
using UnityEditor;
using System.IO;
using System.Text;
using System.Reflection;
using System.Collections;

public class HierarchyExporter : EditorWindow
{
    [MenuItem("Tools/Export Scene to text")]
    public static void ExportSceneToText()
    {
        GameObject[] rootObjects = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
        StringBuilder sb = new StringBuilder();

        sb.AppendLine($"=== SCENE HIERARCHY & INSPECTOR DUMP ===");
        sb.AppendLine($"Scene Name: {UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}");
        sb.AppendLine($"Generated on: {System.DateTime.Now}\n");

        foreach (GameObject rootObj in rootObjects)
        {
            DumpGameObject(rootObj, sb, 0);
        }

        string filePath = Path.Combine(Application.dataPath, "Scene_Snapshot.txt");
        File.WriteAllText(filePath, sb.ToString());
        
        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog("Export Complete", $"Scene data exported to Assets/Scene_Snapshot.txt", "OK");
    }

    private static void DumpGameObject(GameObject obj, StringBuilder sb, int indentLevel)
    {
        // FILTER A: Skip empty structural / folder GameObjects that have no children and no extra components
        Component[] allComponents = obj.GetComponents<Component>();
        if (allComponents.Length <= 1 && obj.transform.childCount == 0)
        {
            return; 
        }

        string indent = new string(' ', indentLevel * 2); // Reduced indent size to save token space
        sb.AppendLine($"{indent}▶ {obj.name} {(obj.activeSelf ? "" : "[Disabled]")}");

        // Loop through ALL components to give the AI context on physics, colliders, rendering, etc.
        foreach (Component comp in allComponents)
        {
            if (comp == null) continue; // Skip missing/broken scripts

            System.Type type = comp.GetType();

            // Skip the Transform component printout unless it's non-default (keeps files clean)
            if (type == typeof(Transform)) continue; 

            // If it's a standard Unity component, just log its presence and skip deep reflection
            if (!(comp is MonoBehaviour script))
            {
                sb.AppendLine($"{indent}  [Comp] {type.Name}");
                continue;
            }

            sb.AppendLine($"{indent}  [Script] {type.Name}");

            // Fetch public fields and private fields marked with [SerializeField]
            FieldInfo[] fields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            
            foreach (FieldInfo field in fields)
            {
                // FILTER B: Skip compiler-generated backing fields (properties) and hidden fields
                if (field.Name.Contains("<") || System.Attribute.IsDefined(field, typeof(HideInInspector))) 
                    continue;

                if (field.IsPublic || System.Attribute.IsDefined(field, typeof(SerializeField)))
                {
                    object value = field.GetValue(script);
                    
                    // FILTER C: Skip nulls, empty strings, and empty lists/arrays entirely to minimize noise
                    if (value == null) continue;
                    if (value is string str && string.IsNullOrEmpty(str)) continue;
                    if (value is ICollection collection && collection.Count == 0) continue;

                    string valString = value.ToString();
                    
                    // Format Unity object references clean and clear
                    if (value is Object unityObj && unityObj != null)
                    {
                        valString = $"{unityObj.name} ({unityObj.GetType().Name})";
                    }

                    sb.AppendLine($"{indent}    ↳ {field.Name}: {valString}");
                }
            }
        }

        // Recursively handle hierarchy
        for (int i = 0; i < obj.transform.childCount; i++)
        {
            DumpGameObject(obj.transform.GetChild(i).gameObject, sb, indentLevel + 1);
        }
    }
}