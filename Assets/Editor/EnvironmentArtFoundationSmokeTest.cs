using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class EnvironmentArtFoundationSmokeTest
{
    private const string PendingKey = "CyberClub.EnvironmentArt.Pending";
    private const string FailedKey = "CyberClub.EnvironmentArt.Failed";
    private const string PrimarySaveFingerprintKey =
        "CyberClub.EnvironmentArt.PrimarySaveFingerprint";
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";

    private static readonly string QASavePath = SaveStorageProfile.QASavePath;
    private static readonly string QASaveBackupPath = Path.Combine(
        Path.GetTempPath(),
        "cyber_club_environment_art_qa_save.bak"
    );
    private static readonly List<string> RuntimeErrors = new();

    private static double verifyAt;

    static EnvironmentArtFoundationSmokeTest()
    {
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        Application.logMessageReceived -= OnLogMessageReceived;
        Application.logMessageReceived += OnLogMessageReceived;
    }

    public static void Run()
    {
        try
        {
            SaveStorageProfile.UseQASandbox();
            BackupQASave();
            CapturePrimarySaveFingerprint();
            RuntimeErrors.Clear();

            if (File.Exists(QASavePath))
            {
                File.Delete(QASavePath);
            }

            EditorPrefs.SetBool(PendingKey, true);
            EditorPrefs.SetBool(FailedKey, false);
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            EditorApplication.isPlaying = true;
        }
        catch (Exception exception)
        {
            Fail(exception);
        }
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (!EditorPrefs.GetBool(PendingKey, false))
        {
            return;
        }

        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            SaveStorageProfile.UseQASandbox();
            verifyAt = EditorApplication.timeSinceStartup + 4d;
        }
        else if (state == PlayModeStateChange.EnteredEditMode)
        {
            bool failed = EditorPrefs.GetBool(FailedKey, false);
            try
            {
                ValidatePrimarySaveFingerprint();
            }
            catch (Exception exception)
            {
                failed = true;
                Debug.LogError(
                    $"ENVIRONMENT_ART_FOUNDATION_SMOKE_TEST: FAIL\n{exception}"
                );
            }
            finally
            {
                RestoreQASave();
                EditorPrefs.DeleteKey(PendingKey);
                EditorPrefs.DeleteKey(FailedKey);
                EditorPrefs.DeleteKey(PrimarySaveFingerprintKey);
            }

            if (failed)
            {
                EditorApplication.Exit(1);
                return;
            }

            Debug.Log("ENVIRONMENT_ART_FOUNDATION_SMOKE_TEST: PASS");
            EditorApplication.Exit(0);
        }
    }

    private static void Tick()
    {
        if (!EditorPrefs.GetBool(PendingKey, false) ||
            !EditorApplication.isPlaying ||
            EditorApplication.timeSinceStartup < verifyAt)
        {
            return;
        }

        verifyAt = double.MaxValue;
        try
        {
            VerifyFoundation();
            EditorApplication.isPlaying = false;
        }
        catch (Exception exception)
        {
            Fail(exception);
        }
    }

    private static void VerifyFoundation()
    {
        Require(RuntimeErrors.Count == 0,
            $"Runtime errors were logged: {string.Join(" | ", RuntimeErrors)}");
        Require(SaveStorageProfile.IsQASandboxActive,
            "Editor Play Mode did not activate the QA save sandbox.");
        Require(!string.Equals(
                SaveStorageProfile.ActiveSavePath,
                SaveStorageProfile.PrimarySavePath,
                StringComparison.OrdinalIgnoreCase),
            "QA and primary save paths are not isolated.");
        Require(SaveManager.Instance != null &&
                SaveManager.Instance.TrySaveGame() && File.Exists(QASavePath),
            "The QA sandbox could not write its isolated save.");

        ValidateEnvironmentHierarchy();
        ValidateArchitecture();
        ValidateCharacters();
        ValidateInteractionAndCamera();

        Require(RuntimeErrors.Count == 0,
            $"Runtime errors were logged: {string.Join(" | ", RuntimeErrors)}");
    }

    private static void ValidateEnvironmentHierarchy()
    {
        SpriteRenderer exterior = RequireRenderer("ExteriorVoid");
        SpriteRenderer floor = RequireRenderer("Floor");
        SpriteRenderer reception = RequireRenderer("FloorZone_Reception");
        SpriteRenderer mainHall = RequireRenderer("FloorZone_MainHall");
        SpriteRenderer service = RequireRenderer("FloorZone_ServiceLine");
        SpriteRenderer privateRooms = RequireRenderer("FloorZone_PrivateRooms");
        SpriteRenderer wall = RequireRenderer("Wall_Top");

        Require(Luminance(exterior.color) < Luminance(floor.color),
            "The exterior is not darker than the club floor.");
        Require(Luminance(floor.color) < Luminance(wall.color),
            "The walls do not separate from the floor.");

        float receptionDifference = ColorDistance(reception.color, mainHall.color);
        float privateDifference = ColorDistance(privateRooms.color, mainHall.color);
        Require(receptionDifference > 0.005f && receptionDifference < 0.08f &&
                privateDifference > 0.005f && privateDifference < 0.08f,
            "Floor zones are either indistinguishable or too strongly colored.");
        Require(ColorDistance(service.color, reception.color) < 0.12f,
            "The service line is too visually detached from reception.");

        Require(RequireObject("FloorJoint_V_00") != null &&
                RequireObject("FloorJoint_H_00") != null &&
                RequireObject("FloorGuide_Reception") != null,
            "Modular floor details are missing.");
        Require(Luminance(RequireRenderer("FloorJoint_V_00").color) <
                Luminance(mainHall.color),
            "Floor joints read as a bright editor grid.");
    }

    private static void ValidateArchitecture()
    {
        foreach (string wallName in new[]
                 {
                     "Wall_Top", "Wall_Left", "Wall_Right",
                     "PrivateRoom01_Wall_Top", "VIPRoom01_Wall_Top"
                 })
        {
            GameObject wall = RequireObject(wallName);
            Require(wall.transform.Find("WallInset") != null,
                $"{wallName} does not use the shared wall surface style.");
        }

        foreach (string roomId in new[] { "PrivateRoom01", "VIPRoom01" })
        {
            RequireObject($"{roomId}_Floor");
            RequireObject($"{roomId}_FloorInset");
            RequireObject($"{roomId}_FloorSpine");
            RoomDoor door = RequireObject($"{roomId}_Door")
                .GetComponent<RoomDoor>();
            Require(door != null &&
                    door.GetComponent<RoomDoorVisualPresenter>() != null,
                $"{roomId} lost its interactive door presentation.");
        }

        GameObject desk = RequireObject("AdministratorDesk");
        BoxCollider2D deskCollider = desk.GetComponent<BoxCollider2D>();
        Require(deskCollider != null &&
                Vector2.Distance(deskCollider.size, new Vector2(3f, 1f)) < 0.02f,
            "The reception desk is outside its art-foundation size range.");

        string[] terminals =
        {
            "ClubResearchTerminal", "InternetProviderTerminal",
            "MarketingTerminal", "ConsumableStockTerminal", "PricingTerminal",
            "MaintenanceTerminal", "PCExpansionTerminal"
        };
        float previousY = float.PositiveInfinity;
        foreach (string terminalName in terminals)
        {
            GameObject terminal = RequireObject(terminalName);
            Require(Mathf.Abs(terminal.transform.position.x + 7.55f) < 0.02f &&
                    terminal.transform.position.y < previousY &&
                    terminal.GetComponent<TerminalVisualPresenter>() != null,
                $"{terminalName} is not aligned with the service line.");
            previousY = terminal.transform.position.y;
        }
    }

    private static void ValidateCharacters()
    {
        Require(CharacterVisualPresenter.VisualWidth >= 0.6f &&
                CharacterVisualPresenter.VisualWidth <= 0.68f,
            "Temporary characters are outside the readable scale range.");

        ClientSpawner spawner =
            UnityEngine.Object.FindAnyObjectByType<ClientSpawner>();
        Require(spawner != null, "ClientSpawner is missing.");
        spawner.QASpawnClient(ClientType.Regular);
        spawner.QASpawnClient(ClientType.Gamer);
        spawner.QASpawnClient(ClientType.VIP);

        foreach (Client client in UnityEngine.Object.FindObjectsByType<Client>())
        {
            CharacterVisualPresenter presenter =
                client.GetComponent<CharacterVisualPresenter>();
            Require(presenter != null &&
                    client.transform.Find("CharacterVisual/UpperLight") != null &&
                    !client.GetComponent<SpriteRenderer>().enabled,
                $"{client.name} does not use the shared character language.");
        }

        CleanerManager.Instance.RestoreState(true);
        CleanerAgent cleaner = CleanerManager.Instance.CleanerAgent;
        Require(cleaner != null &&
                cleaner.GetComponent<CharacterVisualPresenter>() != null &&
                cleaner.transform.Find("CharacterVisual/UpperLight") != null,
            "The cleaner does not use the shared temporary character visual.");
        CleanerManager.Instance.RestoreState(false);
    }

    private static void ValidateInteractionAndCamera()
    {
        CameraFollow cameraFollow =
            UnityEngine.Object.FindAnyObjectByType<CameraFollow>();
        ManagerModeController manager =
            UnityEngine.Object.FindAnyObjectByType<ManagerModeController>();
        Camera camera = cameraFollow != null
            ? cameraFollow.GetComponent<Camera>()
            : null;
        PC pc = RequireObject("PC_01").GetComponent<PC>();
        Require(cameraFollow != null && manager != null && camera != null &&
                pc != null,
            "Camera or manager-mode dependencies are missing.");

        float originalAspect = camera.aspect;
        foreach (Vector2Int resolution in new[]
                 {
                     new Vector2Int(1920, 1080),
                     new Vector2Int(1366, 768)
                 })
        {
            camera.aspect = resolution.x / (float)resolution.y;
            Require(cameraFollow.ShowOverview(),
                $"Overview failed at {resolution.x}x{resolution.y}.");
            Bounds bounds = ClubLayoutBuilder.Instance != null
                ? UnityEngine.Object.FindAnyObjectByType<CameraBounds2D>().WorldBounds
                : default;
            Vector3 minimum = camera.WorldToViewportPoint(bounds.min);
            Vector3 maximum = camera.WorldToViewportPoint(bounds.max);
            Require(minimum.x >= -0.01f && maximum.x <= 1.01f &&
                    minimum.y >= 0.06f && maximum.y <= 0.9f,
                $"Overview framing is unsafe at {resolution.x}x{resolution.y}.");
        }

        camera.aspect = 1920f / 1080f;
        cameraFollow.ShowOverview();
        CaptureCamera(camera, "Overview");

        Require(manager.TrySelectAtWorldPosition(pc.transform.position) &&
                manager.SelectedBehaviour == pc &&
                manager.FocusSelectedObject(),
            "PC selection or Focus interaction is broken.");
        float viewportX = camera.WorldToViewportPoint(pc.transform.position).x;
        Require(viewportX >= 0.1f && viewportX <= 0.47f &&
                cameraFollow.CurrentOrthographicSize <= 5f,
            $"Focus composition is not panel-safe: x={viewportX:F2}.");

        PC neighbour = RequireObject("PC_02").GetComponent<PC>();
        Vector3 neighbourViewport = camera.WorldToViewportPoint(
            neighbour.transform.position
        );
        Require(neighbourViewport.x > 0f && neighbourViewport.x < 0.74f &&
                neighbourViewport.y > 0.08f && neighbourViewport.y < 0.9f,
            "Focus no longer includes the selected PC's neighbour and aisle.");
        CaptureCamera(camera, "Focus");

        manager.ClearSelection();
        camera.aspect = originalAspect;
        cameraFollow.ShowOverview();
        Require(UnityEngine.Object.FindAnyObjectByType<ManagerBuildController>() !=
                null,
            "Manager construction controls are missing.");
    }

    private static void CaptureCamera(Camera camera, string viewName)
    {
        const int width = 1920;
        const int height = 1080;
        string directory = Path.GetFullPath(Path.Combine(
            Application.dataPath,
            "..",
            "TestArtifacts",
            "EnvironmentArtFoundation"
        ));
        Directory.CreateDirectory(directory);

        RenderTexture renderTexture = RenderTexture.GetTemporary(
            width,
            height,
            24,
            RenderTextureFormat.ARGB32
        );
        RenderTexture previousTarget = camera.targetTexture;
        RenderTexture previousActive = RenderTexture.active;
        Texture2D image = new(width, height, TextureFormat.RGB24, false);
        try
        {
            camera.targetTexture = renderTexture;
            camera.Render();
            camera.Render();
            RenderTexture.active = renderTexture;
            image.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
            image.Apply();
            File.WriteAllBytes(
                Path.Combine(directory, $"{viewName}_1920x1080.png"),
                image.EncodeToPNG()
            );
        }
        finally
        {
            camera.targetTexture = previousTarget;
            RenderTexture.active = previousActive;
            RenderTexture.ReleaseTemporary(renderTexture);
            UnityEngine.Object.Destroy(image);
        }
    }

    private static GameObject RequireObject(string objectName)
    {
        GameObject result = GameObject.Find(objectName);
        if (result == null)
        {
            throw new InvalidOperationException($"{objectName} was not found.");
        }
        return result;
    }

    private static SpriteRenderer RequireRenderer(string objectName)
    {
        SpriteRenderer renderer = RequireObject(objectName)
            .GetComponent<SpriteRenderer>();
        Require(renderer != null, $"{objectName} has no SpriteRenderer.");
        return renderer;
    }

    private static float Luminance(Color color)
    {
        return color.r * 0.2126f + color.g * 0.7152f + color.b * 0.0722f;
    }

    private static float ColorDistance(Color first, Color second)
    {
        Vector3 difference = new(
            first.r - second.r,
            first.g - second.g,
            first.b - second.b
        );
        return difference.magnitude;
    }

    private static void CapturePrimarySaveFingerprint()
    {
        EditorPrefs.SetString(
            PrimarySaveFingerprintKey,
            GetFileFingerprint(SaveStorageProfile.PrimarySavePath)
        );
    }

    private static void ValidatePrimarySaveFingerprint()
    {
        string before = EditorPrefs.GetString(
            PrimarySaveFingerprintKey,
            string.Empty
        );
        string after = GetFileFingerprint(SaveStorageProfile.PrimarySavePath);
        Require(string.Equals(before, after, StringComparison.Ordinal),
            "The primary user save changed during the QA smoke test.");
    }

    private static string GetFileFingerprint(string path)
    {
        if (!File.Exists(path))
        {
            return "missing";
        }

        using SHA256 sha256 = SHA256.Create();
        return Convert.ToBase64String(sha256.ComputeHash(File.ReadAllBytes(path)));
    }

    private static void BackupQASave()
    {
        if (File.Exists(QASavePath))
        {
            File.Copy(QASavePath, QASaveBackupPath, true);
        }
        else if (File.Exists(QASaveBackupPath))
        {
            File.Delete(QASaveBackupPath);
        }
    }

    private static void RestoreQASave()
    {
        if (File.Exists(QASaveBackupPath))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(QASavePath));
            File.Copy(QASaveBackupPath, QASavePath, true);
            File.Delete(QASaveBackupPath);
        }
        else if (File.Exists(QASavePath))
        {
            File.Delete(QASavePath);
        }
    }

    private static void OnLogMessageReceived(
        string condition,
        string stackTrace,
        LogType type)
    {
        if ((type == LogType.Error || type == LogType.Exception ||
             type == LogType.Assert) &&
            !condition.StartsWith("ENVIRONMENT_ART_FOUNDATION_SMOKE_TEST: FAIL") &&
            stackTrace.Contains("Assets/"))
        {
            RuntimeErrors.Add(condition);
        }
    }

    private static void Fail(Exception exception)
    {
        EditorPrefs.SetBool(FailedKey, true);
        Debug.LogError($"ENVIRONMENT_ART_FOUNDATION_SMOKE_TEST: FAIL\n{exception}");
        if (EditorApplication.isPlaying)
        {
            EditorApplication.isPlaying = false;
        }
        else
        {
            RestoreQASave();
            EditorPrefs.DeleteKey(PendingKey);
            EditorPrefs.DeleteKey(FailedKey);
            EditorPrefs.DeleteKey(PrimarySaveFingerprintKey);
            EditorApplication.Exit(1);
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
