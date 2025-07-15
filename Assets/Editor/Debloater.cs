
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Linq;
using UnityEditor;
using UnityEngine;

public class Debloater : EditorWindow
{
    private static List<string> unusedAssets = new List<string>();
    private static string projectPath = Application.dataPath;

    [MenuItem("Tools/Debloater")]
    public static void ShowWindow()
    {
        GetWindow(typeof(Debloater));
    }

    void OnEnable()
    {
        FindUnusedAssets();
    }

    void OnGUI()
    {
        GUILayout.Label($"Found {unusedAssets.Count} unused assets", EditorStyles.boldLabel);
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
            PrintOnConsole();
        }
    }

    static void FindUnusedAssets()
    {   
        // Clear for refreshability
        unusedAssets.Clear();

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
            {AssetDatabase.Refresh();
                unusedAssets.Add(assetPath);
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
        ($"Exported {unusedAssets.Count} unused assets to {unusedDir}");
        Debug.Log
        ($"Note: Delete Unused Asset now will not work, simply delete {unusedDir} instead");
        AssetDatabase.Refresh();
    }

    static void DeleteUnusedAssets()
    {
        foreach (string assetPath in unusedAssets)
        {
            AssetDatabase.DeleteAsset(assetPath);
        }
        AssetDatabase.Refresh();
    }
    
    static void PrintOnConsole()
    {
        foreach (string asset in unusedAssets)
        {
            Debug.Log($"{asset}");
        }
    }
    
}
