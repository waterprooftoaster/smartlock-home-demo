using UnityEditor.Build.Reporting;
using UnityEngine;
using System.IO;

public class BuildReportReader
{
    [MenuItem("Tools/Read Build Report")]
    static void ReadReport()
    {
        string path = "Library/LastBuild.buildreport"; // Change path if needed

        if (File.Exists(path))
        {
            BuildReport report = BuildReport.Open(path);
            Debug.Log("Build Target: " + report.summary.platform);
            Debug.Log("Total Size: " + report.summary.totalSize + " bytes");
            Debug.Log("Result: " + report.summary.result);

            foreach (var step in report.steps)
            {
                Debug.Log($"Step: {step.name}, Duration: {step.duration}");
                foreach (var msg in step.messages)
                {
                    Debug.Log($"{msg.type}: {msg.content}");
                }
            }
        }
        else
        {
            Debug.LogError("Build report not found at: " + path);
        }
    }
}
