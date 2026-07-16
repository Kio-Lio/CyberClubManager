using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;

public static class PrereleaseBuild
{
    public static void BuildDevelopment()
    {
        Build(
            "Builds/Development/CyberClubManager.exe",
            BuildOptions.Development
        );
    }

    public static void BuildRelease()
    {
        Build(
            "Builds/Release/CyberClubManager.exe",
            BuildOptions.None
        );
    }

    private static void Build(string outputPath, BuildOptions options)
    {
        string[] scenes = EditorBuildSettings.scenes
            .Where(scene => scene.enabled)
            .Select(scene => scene.path)
            .ToArray();

        BuildReport report = BuildPipeline.BuildPlayer(
            new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = outputPath,
                target = BuildTarget.StandaloneWindows64,
                options = options
            }
        );

        if (report.summary.result != BuildResult.Succeeded)
        {
            throw new InvalidOperationException(
                $"Player build failed: {report.summary.result}"
            );
        }
    }
}
