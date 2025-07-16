
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Linq;
using UnityEditor;
using UnityEngine;
public class Debloater : EditorWindow
{
    private static List<string> unusedAssets = new List<string>();
    private static List<string> usedAssets = new List<string>();
    private static string projectPath = Application.dataPath;
    static readonly string[] protectedFolders = new[]
    {
        // "Assets/Editor",
        // "Assets/Plugins",
        // "Assets/Resources",
        // "Assets/StreamingAssets",
        // "Assets/Gizmos",
        // "Assets/Standard Assets",
        // "Assets/Scenes"
    };

    static readonly string[] protectedExtensions = new[]
    {
        // ".cs",
        // ".dll",
        // ".asmdef",
        // ".asmref",
        // ".shader",
        // ".cginc",
        // ".hlsl",
        // ".uxml",
        // ".uss"
    };

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

    private bool IsProtected(string path)
    {
        if (protectedExtensions.Any(ext => path.EndsWith(ext)))
            return true;

        if (protectedFolders.Any(folder => path.Replace('\\', '/').StartsWith(folder)))
            return true;

        return false;
    }

    static void FindUnusedAssets()
    {
        // Clear for refreshability
        unusedAssets.Clear();

        // Step 1: Find all used assets
        string[] sceneAssetPaths = AssetDatabase.FindAssets("t:Scene")
             .Select(AssetDatabase.GUIDToAssetPath)
             .ToArray();
        HashSet<string> usedAssetsHash =
        new HashSet<string>(AssetDatabase.GetDependencies(sceneAssetPaths,
                                                          true));
        foreach (string assetPath in sceneAssetPaths)
        {
            usedAssets.Add(assetPath);
            usedAssetsHash.Add(assetPath);
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
            if (usedAssetsHash.Contains(assetPath))
            {
                continue;
            }
            if (IsProtected(assetPath))
            {
                continue;
            }
            else
            {
                unusedAssets.Add(assetPath);
            }
        }
        AssetDatabase.Refresh();
    }

    static void ExportAsTxt()
    {
        // Define Target Path
        string targetDir = Path.Combine(Application.dataPath, "Logs");
        string targetPath = Path.Combine(targetDir, "AssetUsage.txt");

        if (!Directory.Exists(targetDir))
        {
            Directory.CreateDirectory(targetDir);
        }

        // Write the .txt
        using (StreamWriter writer = new StreamWriter(targetPath, false))
        {
            foreach (string path in usedAssets)
            {
                writer.WriteLine(path);
            }

            foreach (string path in unusedAssets)
            {
                writer.WriteLine(path);
            }
        }
        Console.WriteLine($"File written to: {targetPath}");
    }

    static void MoveUnusedAssets()
    {
        string targetDir = Path.Combine(projectPath, "Assets/unusedAssets");
        if (!Directory.Exists(targetDir))
        {
            Directory.CreateDirectory(targetDir);
        }

        foreach (string assetPath in unusedAssets)
        {
            // string destPath = Path.Combine(targetDir, Path.GetFileName(assetPath));
            AssetDatabase.MoveAsset(assetPath, "Assets/unusedAssets");
        }
        Debug.Log
        ($"Exported {unusedAssets.Count} unused assets to {targetDir}");
        Debug.Log
        ($"Note: Delete Unused Asset now will not work, simply delete {targetDir} instead");
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
