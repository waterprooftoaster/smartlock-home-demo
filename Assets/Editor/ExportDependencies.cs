using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

public class ExportDependenciesClass
{
    [MenuItem("Tools/Export Scene Dependencies to CSV")]
    static void ExportDependencies()
    {
        // Step 1: Find all scene GUIDs
        string[] sceneGuids = AssetDatabase.FindAssets("t:Scene");
        string[] scenePaths = new string[sceneGuids.Length];

        for (int i = 0; i < sceneGuids.Length; i++)
        {
            scenePaths[i] = AssetDatabase.GUIDToAssetPath(sceneGuids[i]);
        }

        // Step 2: Get all dependencies
        string[] dependencies = AssetDatabase.GetDependencies(scenePaths, true); // true = recursive

        // Step 3: Write to CSV or TXT
        string outputPath = "Assets/SceneDependencies.csv";  // or .txt
        StringBuilder sb = new StringBuilder();

        sb.AppendLine("DependencyPath");

        foreach (string dependency in dependencies)
        {
            sb.AppendLine(dependency);
        }

        File.WriteAllText(outputPath, sb.ToString(), Encoding.UTF8);

        Debug.Log($"Exported {dependencies.Length} dependencies to {outputPath}");
        AssetDatabase.Refresh(); // Refresh to show the new file in the Project panel
    }
}