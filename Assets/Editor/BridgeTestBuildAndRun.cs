using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// One-shot CLI helper for Legacy Lock Chain verification builds only.
/// Not part of runtime business logic.
/// </summary>
public static class BridgeTestBuildAndRun
{
    const string OutputApk = "Builds/Android/BridgeTest-Legacy.apk";

    public static void BuildAndRun()
    {
        var scenes = EditorBuildSettings.scenes
            .Where(s => s.enabled)
            .Select(s => s.path)
            .ToArray();

        if (scenes.Length == 0)
            throw new InvalidOperationException("No enabled scenes in EditorBuildSettings.");

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(OutputApk)));

        var opts = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = OutputApk,
            target = BuildTarget.Android,
            options = BuildOptions.AutoRunPlayer
        };

        Debug.Log($"[BridgeTestBuild] Building scenes={string.Join(",", scenes)} → {OutputApk}");
        BuildReport report = BuildPipeline.BuildPlayer(opts);
        BuildSummary summary = report.summary;

        Debug.Log(
            $"[BridgeTestBuild] result={summary.result} errors={summary.totalErrors} " +
            $"warnings={summary.totalWarnings} size={summary.totalSize} " +
            $"time={summary.totalTime.TotalSeconds:F1}s");

        if (summary.result != BuildResult.Succeeded)
            EditorApplication.Exit(1);
        else
            EditorApplication.Exit(0);
    }
}
