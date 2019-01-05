using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace UnityAiNpc.Editor
{
    /// <summary>
    /// Player builds. Batch mode:
    /// <c>Unity -batchmode -quit -projectPath . -executeMethod UnityAiNpc.Editor.BuildScript.BuildWindows</c>
    /// </summary>
    public static class BuildScript
    {
        public const string WindowsOutput = "Builds/Windows/UnityAiNpc.exe";

        [MenuItem("AI NPC/Build Windows Player")]
        public static void BuildWindows()
        {
            if (!File.Exists(SceneBuilder.ScenePath)) SceneBuilder.Build();

            var options = new BuildPlayerOptions
            {
                scenes = new[] { SceneBuilder.ScenePath },
                locationPathName = WindowsOutput,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None,
            };
            var report = BuildPipeline.BuildPlayer(options);
            var summary = report.summary;
            Debug.Log($"[AI NPC] Build {summary.result}: {summary.outputPath} ({summary.totalSize / (1024 * 1024)} MB, {summary.totalErrors} errors)");

            // Copy .env.example next to the exe so players know where the key goes.
            if (summary.result == BuildResult.Succeeded && File.Exists(".env.example"))
                File.Copy(".env.example", Path.Combine(Path.GetDirectoryName(WindowsOutput) ?? ".", ".env.example"), true);

            if (Application.isBatchMode)
                EditorApplication.Exit(summary.result == BuildResult.Succeeded ? 0 : 1);
        }
    }
}

    public static object EncodeToken(object data = null)
    {
        if (data == null) return null;
        return data;
    }