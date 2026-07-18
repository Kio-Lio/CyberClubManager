using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class PlaytestBuild
{
    public const string Version = "0.1.0";
    public const string BuildDirectory = "Builds/Playtest-0.1.0";
    public const string ExecutablePath =
        BuildDirectory + "/CyberClubManager.exe";
    public const string ArchivePath =
        "Builds/CyberClubManager-Playtest-0.1.0-Windows-x64.zip";

    private const string IconPath =
        "Assets/Art/Icons/PlaytestAppIcon.png";
    private const string DocumentsDirectory = "Playtest";

    [MenuItem("Cyber Club Manager/Playtest/Configure Project")]
    public static void ConfigureProject()
    {
        PlayerSettings.companyName = "Kio-Lio";
        PlayerSettings.productName = "Cyber Club Manager";
        PlayerSettings.bundleVersion = Version;

        Texture2D icon = AssetDatabase.LoadAssetAtPath<Texture2D>(IconPath);
        if (icon == null)
        {
            throw new InvalidOperationException(
                $"Windows icon was not found at {IconPath}."
            );
        }

        int iconCount = PlayerSettings.GetIconSizes(
            NamedBuildTarget.Standalone,
            IconKind.Application
        ).Length;
        Texture2D[] icons = Enumerable.Repeat(icon, iconCount).ToArray();
        PlayerSettings.SetIcons(
            NamedBuildTarget.Standalone,
            icons,
            IconKind.Application
        );

        AssetDatabase.SaveAssets();
        Debug.Log("PLAYTEST_PROJECT_SETTINGS: PASS");
    }

    [MenuItem("Cyber Club Manager/Playtest/Build 0.1.0")]
    public static void Build()
    {
        ConfigureProject();

        string absoluteBuildDirectory = Path.GetFullPath(BuildDirectory);
        if (Directory.Exists(absoluteBuildDirectory))
        {
            Directory.Delete(absoluteBuildDirectory, true);
        }

        Directory.CreateDirectory(absoluteBuildDirectory);
        BuildReport report = BuildPipeline.BuildPlayer(
            new BuildPlayerOptions
            {
                scenes = GetEnabledScenes(),
                locationPathName = ExecutablePath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            }
        );

        if (report.summary.result != BuildResult.Succeeded)
        {
            throw new InvalidOperationException(
                $"Playtest build failed: {report.summary.result}"
            );
        }

        RemoveDoNotShipArtifacts();
        CopyDocument("README_RU.txt");
        CopyDocument("FEEDBACK_TEMPLATE.txt");
        CopyDocument("THIRD_PARTY_NOTICES.txt");
        ValidatePackageFiles();

        Debug.Log(
            $"PLAYTEST_BUILD: PASS ({report.summary.totalSize} bytes)"
        );
    }

    [MenuItem("Cyber Club Manager/Playtest/Create ZIP")]
    public static void CreateArchive()
    {
        ValidatePackageFiles();

        string absoluteArchivePath = Path.GetFullPath(ArchivePath);
        if (File.Exists(absoluteArchivePath))
        {
            File.Delete(absoluteArchivePath);
        }

        ZipFile.CreateFromDirectory(
            Path.GetFullPath(BuildDirectory),
            absoluteArchivePath,
            System.IO.Compression.CompressionLevel.Optimal,
            false
        );
        Debug.Log($"PLAYTEST_ARCHIVE: PASS ({absoluteArchivePath})");
    }

    private static string[] GetEnabledScenes()
    {
        string[] scenes = EditorBuildSettings.scenes
            .Where(scene => scene.enabled)
            .Select(scene => scene.path)
            .ToArray();
        if (scenes.Length < 2 ||
            scenes[0] != "Assets/Scenes/MainMenu.unity" ||
            scenes[1] != "Assets/Scenes/SampleScene.unity")
        {
            throw new InvalidOperationException(
                "Build Settings must start with MainMenu and SampleScene."
            );
        }

        return scenes;
    }

    private static void CopyDocument(string fileName)
    {
        File.Copy(
            Path.Combine(DocumentsDirectory, fileName),
            Path.Combine(BuildDirectory, fileName),
            true
        );
    }

    private static void RemoveDoNotShipArtifacts()
    {
        foreach (string directory in Directory.GetDirectories(
                     BuildDirectory,
                     "*_BurstDebugInformation_DoNotShip",
                     SearchOption.TopDirectoryOnly))
        {
            Directory.Delete(directory, true);
        }
    }

    private static void ValidatePackageFiles()
    {
        string[] requiredFiles =
        {
            "CyberClubManager.exe",
            "CyberClubManager_Data",
            "UnityCrashHandler64.exe",
            "UnityPlayer.dll",
            "README_RU.txt",
            "FEEDBACK_TEMPLATE.txt",
            "THIRD_PARTY_NOTICES.txt"
        };

        foreach (string fileName in requiredFiles)
        {
            string path = Path.Combine(BuildDirectory, fileName);
            if (!File.Exists(path) && !Directory.Exists(path))
            {
                throw new FileNotFoundException(
                    $"Playtest package is missing {fileName}.",
                    path
                );
            }
        }
    }
}
