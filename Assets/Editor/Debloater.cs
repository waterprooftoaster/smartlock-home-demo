
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEditor.VersionControl;


public class Debloater : EditorWindow
{
    private static string[] unusedAssets;
    private static string projectPath = Application.dataPath;

    [MenuItem("Tools/Debloater")]
    public static void ShowWindow()
    {
        GetWindow(typeof(Debloater));
    }

    void OnEable()
    {
        FindUnusedAssets();
    }

    void OnGUI()
    {
        GUILayout.Label($"Found {unusedAssets.Length} unused assets", EditorStyles.boldLabel);
        if (GUILayout.Button("Move to /UnusedAssets"))
        {
            MoveUnusedAssets();
        }
        if (GUILayout.Button("Delete Unused Assets"))
        {
            DeleteUnusedAssets();
        }
         if (GUILayout.Button("Export as .txt"))
        {
            ExportAsTxt();
        }
        if (GUILayout.Button("Print on Console"))
        {
            
        }
    }
    
    static void FindUnusedAssets()
    {
        // Step 1: Find all used assets
        string[] sceneAssetPaths = AssetDatabase.FindAssets("t:Scene")
             .Select(AssetDatabase.GUIDToAssetPath)
             .ToArray();
        HashSet<string> usedAssetPaths =
        new HashSet<string>(AssetDatabase.GetDependencies(sceneAssetPaths,
                                                          true));
        foreach (string assetPath in sceneAssetPaths)
        {
            usedAssetPaths.Add(assetPath);
        }

        // Step 2: Get all assets
        string[] allAssetGuids = AssetDatabase.FindAssets("t:Object");
        string[] allAssetPaths = new string[allAssetGuids.Length];
        for (int j = 0; j < allAssetGuids.Length; j++)
        {
            allAssetPaths[j] = AssetDatabase.GUIDToAssetPath(allAssetGuids[j]);
        }

        // Step 3: Cross Reference and find unused assets
        foreach (string assetPath in allAssetPaths)
        {
            if (usedAssetPaths.Contains(assetPath))
            {
                continue;
            }
            else
            {
                unusedAssets.Append(assetPath);
            }
        }
    }

    static void ExportAsTxt()
    {
        
    }

    static void MoveUnusedAssets()
    {
        string unusedDir = Path.Combine(projectPath, "Assets/unusedAssets");
        if (!Directory.Exists(unusedDir))
        {
            Directory.CreateDirectory(unusedDir);
        }

        foreach (string assetPath in unusedAssets)
        {
            // string destPath = Path.Combine(unusedDir, Path.GetFileName(assetPath));
            AssetDatabase.MoveAsset(assetPath, "Assets/unusedAssets");
        }
        Debug.Log
        ($"Exported {unusedAssets.Length} unused assets to {unusedDir}");
    }

    static void DeleteUnusedAssets()
    {
        foreach (string assetPath in unusedAssets)
        {
            AssetDatabase.DeleteAsset(assetPath);
        }
    }
    
}
