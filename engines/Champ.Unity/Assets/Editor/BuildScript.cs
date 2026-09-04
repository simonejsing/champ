using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Champ.Unity.Editor
{
    /// <summary>
    /// Builds the standalone player. Works from the Editor menu, or headless via:
    /// Unity.exe -batchmode -nographics -quit -projectPath engines/Champ.Unity
    ///     -executeMethod Champ.Unity.Editor.BuildScript.BuildWindowsCli
    ///     -buildOutput Build/Windows/Champ.exe
    /// </summary>
    public static class BuildScript
    {
        const string SceneName = "Assets/Scenes/Main.unity";
        const string DefaultWindowsOutput = "Build/Windows/Champ.exe";

        [MenuItem("Champ/Build Standalone Windows")]
        public static void BuildWindowsMenu() => Build(BuildTarget.StandaloneWindows64, DefaultWindowsOutput);

        public static void BuildWindowsCli() =>
            Build(BuildTarget.StandaloneWindows64, GetArg("-buildOutput") ?? DefaultWindowsOutput);

        static void Build(BuildTarget target, string outputPath)
        {
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { SceneName },
                locationPathName = outputPath,
                target = target,
                options = BuildOptions.None
            });

            var summary = report.summary;
            Debug.Log($"Build {summary.result} — {summary.totalSize} bytes, " +
                      $"{summary.totalErrors} errors, {summary.totalWarnings} warnings, output: {outputPath}");

            if (summary.result != BuildResult.Succeeded)
                EditorApplication.Exit(1);
        }

        static string GetArg(string name)
        {
            var args = Environment.GetCommandLineArgs();
            var i = Array.IndexOf(args, name);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        }
    }
}
