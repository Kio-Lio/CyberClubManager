using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class EnvironmentFoundationPolishSmokeTest
{
    private const string PendingKey =
        "CyberClub.EnvironmentFoundationPolish.Pending";
    private const string FailedKey =
        "CyberClub.EnvironmentFoundationPolish.Failed";
    private const string PrimaryFingerprintKey =
        "CyberClub.EnvironmentFoundationPolish.PrimaryFingerprint";
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";

    private static readonly string QASavePath = SaveStorageProfile.QASavePath;
    private static readonly string QASaveBackupPath = Path.Combine(
        Path.GetTempPath(),
        "cyber_club_environment_polish_qa_save.bak"
    );
    private static readonly List<string> RuntimeErrors = new();

    private static double verifyAt;

    static EnvironmentFoundationPolishSmokeTest()
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
            CapturePrimaryFingerprint();
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
            return;
        }

        if (state != PlayModeStateChange.EnteredEditMode)
        {
            return;
        }

        bool failed = EditorPrefs.GetBool(FailedKey, false);
        try
        {
            ValidatePrimaryFingerprint();
        }
        catch (Exception exception)
        {
            failed = true;
            Debug.LogError(
                $"ENVIRONMENT_FOUNDATION_POLISH_SMOKE_TEST: FAIL\n{exception}"
            );
        }
        finally
        {
            RestoreQASave();
            EditorPrefs.DeleteKey(PendingKey);
            EditorPrefs.DeleteKey(FailedKey);
            EditorPrefs.DeleteKey(PrimaryFingerprintKey);
        }

        if (failed)
        {
            EditorApplication.Exit(1);
            return;
        }

        Debug.Log("ENVIRONMENT_FOUNDATION_POLISH_SMOKE_TEST: PASS");
        EditorApplication.Exit(0);
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
            VerifyPolishPass();
            EditorApplication.isPlaying = false;
        }
        catch (Exception exception)
        {
            Fail(exception);
        }
    }

    private static void VerifyPolishPass()
    {
        Require(RuntimeErrors.Count == 0,
            $"Runtime errors were logged: {string.Join(" | ", RuntimeErrors)}");
        Require(SaveStorageProfile.IsQASandboxActive,
            "Editor Play Mode did not activate the QA save sandbox.");
        Require(!string.Equals(
                SaveStorageProfile.ActiveSavePath,
                SaveStorageProfile.PrimarySavePath,
                StringComparison.OrdinalIgnoreCase),
            "QA and primary saves use the same physical path.");
        Require(SaveManager.Instance != null &&
                SaveManager.Instance.TrySaveGame() &&
                File.Exists(QASavePath),
            "The isolated QA save could not be written.");

        ValidateExteriorAndFloor();
        ValidateWallsAndEntrance();
        ValidateCharacters();
        ValidateExpansionSlots();
        ValidateFeedback();
        ValidateInteractionAndCamera();

        Require(RuntimeErrors.Count == 0,
            $"Runtime errors were logged: {string.Join(" | ", RuntimeErrors)}");
    }

    private static void ValidateExteriorAndFloor()
    {
        SpriteRenderer exterior = RequireRenderer("ExteriorVoid");
        SpriteRenderer floor = RequireRenderer("Floor");
        SpriteRenderer reception = RequireRenderer("FloorZone_Reception");
        SpriteRenderer mainHall = RequireRenderer("FloorZone_MainHall");
        SpriteRenderer service = RequireRenderer("FloorZone_ServiceLine");
        SpriteRenderer privateRooms =
            RequireRenderer("FloorZone_PrivateRooms");
        SpriteRenderer entrance = RequireRenderer("FloorZone_Entrance");
        SpriteRenderer joint = RequireRenderer("FloorJoint_V_00");

        float exteriorLuminance = Luminance(exterior.color);
        float floorLuminance = Luminance(floor.color);
        Require(exteriorLuminance < 0.025f &&
                floorLuminance - exteriorLuminance > 0.08f,
            "Exterior and floor no longer have a strong dark hierarchy.");
        Require(exterior.color.b < 0.04f &&
                exterior.color.b - exterior.color.r < 0.025f,
            "ExteriorVoid still reads as a bright blue field.");
        Require(floorLuminance >= 0.10f && floorLuminance <= 0.16f,
            "The polished base floor is outside the dark readable range.");

        foreach (SpriteRenderer zone in new[]
                 {
                     reception, service, privateRooms, entrance
                 })
        {
            float difference = ColorDistance(zone.color, mainHall.color);
            Require(difference > 0.004f && difference < 0.09f,
                $"{zone.name} is indistinguishable or overly color coded.");
        }

        Require(Luminance(joint.color) < Luminance(mainHall.color) * 0.78f &&
                joint.color.a <= 0.4f,
            "Floor joints read as a bright editor grid.");
    }

    private static void ValidateWallsAndEntrance()
    {
        SpriteRenderer floor = RequireRenderer("Floor");
        SpriteRenderer referenceWall = RequireRenderer("Wall_Top");
        Require(Luminance(referenceWall.color) > Luminance(floor.color) &&
                Luminance(referenceWall.color) < 0.2f,
            "Walls do not separate from the floor or remain too bright.");

        foreach (string wallName in new[]
                 {
                     "Wall_Top", "Wall_Left", "Wall_Right",
                     "PrivateRoom01_Wall_Top",
                     "PrivateRoom01_Wall_Left_Top",
                     "VIPRoom01_Wall_Top",
                     "VIPRoom01_Wall_Left_Top"
                 })
        {
            GameObject wall = RequireObject(wallName);
            Transform inset = wall.transform.Find("WallInset");
            Transform topEdge = wall.transform.Find("WallTopEdge");
            Require(inset != null && topEdge != null,
                $"{wallName} does not use the shared light wall language.");

            float visualThickness = Mathf.Min(
                Mathf.Abs(wall.transform.localScale.x),
                Mathf.Abs(wall.transform.localScale.y)
            );
            Require(visualThickness <= 0.37f,
                $"{wallName} is visually too thick.");
            Require(inset.GetComponent<SpriteRenderer>().color.a <= 0.75f,
                $"{wallName} has an overly heavy wall inset.");
        }

        foreach (string roomId in new[] { "PrivateRoom01", "VIPRoom01" })
        {
            RoomDoor door = RequireObject($"{roomId}_Door")
                .GetComponent<RoomDoor>();
            Require(door != null &&
                    door.GetComponent<RoomDoorVisualPresenter>() != null &&
                    door.GetComponent<BoxCollider2D>() != null,
                $"{roomId} lost its interactive door.");

            bool initialState = door.IsUnlocked;
            door.RestoreState(true);
            Require(!door.GetComponent<BoxCollider2D>().enabled,
                $"{roomId} does not open its navigation passage.");
            door.RestoreState(initialState);
        }

        foreach (string entrancePart in new[]
                 {
                     "EntranceApproach", "FloorZone_Entrance",
                     "EntranceFloorStrip", "EntranceThreshold",
                     "EntrancePost_Left", "EntrancePost_Right"
                 })
        {
            GameObject part = RequireObject(entrancePart);
            Require(part.GetComponent<Collider2D>() == null,
                $"{entrancePart} blocks the entrance navigation.");
        }

        ClientNavigationManager navigation =
            ClientNavigationManager.Instance ??
            ClientNavigationManager.EnsureRuntimeGraph();
        Require(navigation.EntranceNode != null &&
                navigation.QueueNode != null &&
                navigation.BuildPath(
                    navigation.EntranceNode,
                    navigation.QueueNode
                ).Count > 0,
            "The client route through the entrance is broken.");
    }

    private static void ValidateCharacters()
    {
        ClientSpawner spawner =
            UnityEngine.Object.FindAnyObjectByType<ClientSpawner>();
        Require(spawner != null, "ClientSpawner is missing.");
        spawner.QASpawnClient(ClientType.Regular);
        spawner.QASpawnClient(ClientType.Gamer);
        spawner.QASpawnClient(ClientType.VIP);

        foreach (Client client in
                 UnityEngine.Object.FindObjectsByType<Client>())
        {
            CharacterVisualPresenter presenter =
                client.GetComponent<CharacterVisualPresenter>();
            Transform visual = client.transform.Find("CharacterVisual");
            Require(presenter != null && visual != null &&
                    visual.Find("Head") != null &&
                    visual.Find("Body") != null &&
                    visual.Find("Shoulders") != null &&
                    visual.Find("LowerBody") != null &&
                    visual.Find("UpperLight") != null &&
                    visual.Find("Shadow") != null,
                $"{client.name} does not use the polished human silhouette.");
            Require(!client.GetComponent<SpriteRenderer>().enabled,
                $"{client.name} still shows its placeholder renderer.");
            Require(visual.GetComponentsInChildren<Collider2D>(true).Length == 0,
                $"{client.name} visual presenter changed logical collision.");
        }

        CleanerManager.Instance.RestoreState(true);
        CleanerAgent cleaner = CleanerManager.Instance.CleanerAgent;
        Require(cleaner != null &&
                cleaner.GetComponent<CharacterVisualPresenter>() != null &&
                cleaner.transform.Find("CharacterVisual/Shoulders") != null,
            "The cleaner does not share the polished character language.");
        CleanerManager.Instance.RestoreState(false);
    }

    private static void ValidateExpansionSlots()
    {
        PCExpansionSlotVisualPresenter available = null;
        PCExpansionSlotVisualPresenter locked = null;

        for (int index = 0; index < 4; index++)
        {
            string pcName = $"PC_{index + 6:00}";
            PCExpansionSlotVisualPresenter slot =
                RequireObject($"ExpansionSlot_{pcName}")
                    .GetComponent<PCExpansionSlotVisualPresenter>();
            Require(slot != null,
                $"{pcName} has no expansion slot visual presenter.");
            slot.Refresh();

            SpriteRenderer renderer = slot.GetComponent<SpriteRenderer>();
            Require(renderer != null && renderer.color.a <= 0.65f &&
                    Luminance(renderer.color) < 0.16f,
                $"{pcName} slot reads as a bright debug marker.");

            if (!slot.IsOccupied && slot.IsUnlocked && available == null)
            {
                available = slot;
            }
            else if (!slot.IsOccupied && !slot.IsUnlocked && locked == null)
            {
                locked = slot;
            }
        }

        Require(available != null,
            "No available expansion slot could be validated.");
        if (locked != null)
        {
            Require(ColorDistance(
                    available.GetComponent<SpriteRenderer>().color,
                    locked.GetComponent<SpriteRenderer>().color) > 0.01f,
                "Locked and available expansion slots are indistinguishable.");
        }

        ManagerBuildController build =
            UnityEngine.Object.FindAnyObjectByType<ManagerBuildController>();
        Require(build != null && build.BeginPCPlacement(),
            "Construction mode could not expose expansion slots.");
        float buildAlpha =
            available.GetComponent<SpriteRenderer>().color.a;
        Require(buildAlpha >= 0.55f,
            "Available slots do not become clearer during construction.");
        build.CancelPlacement();

        GameObject temporaryPC = new(available.PCName);
        available.Refresh();
        Require(!available.GetComponent<SpriteRenderer>().enabled,
            "The expansion slot remains visible after PC installation.");
        UnityEngine.Object.Destroy(temporaryPC);
    }

    private static void ValidateFeedback()
    {
        ClientFeedbackUI feedback =
            UnityEngine.Object.FindAnyObjectByType<ClientFeedbackUI>();
        Require(feedback != null, "ClientFeedbackUI is missing.");
        Require(feedback.MaximumVisibleCards <= 3 &&
                feedback.PanelSize.x <= 360f &&
                feedback.PanelSize.y <= 100f &&
                feedback.PanelOffset.y <= -170f,
            "Client feedback cards remain too large or overlap the top HUD.");

        ClubReputationManager reputation = ClubReputationManager.Instance;
        Require(reputation != null, "ClubReputationManager is missing.");
        reputation.RegisterServedClient(ClientType.Regular);
        reputation.RegisterServedClient(ClientType.Gamer);
        reputation.RegisterLostClient(ClientType.VIP, 2f);
        Require(feedback.ActiveCardCount <= feedback.MaximumVisibleCards,
            "Client feedback exceeded its visible card limit.");

        GameObject panel = RequireObject("ClientFeedbackPanel");
        RectTransform rect = panel.GetComponent<RectTransform>();
        Require(rect != null &&
                rect.anchoredPosition.y <= -170f &&
                rect.sizeDelta.x <= 360f,
            "Client feedback is outside its compact safe area.");
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
        CameraBounds2D cameraBounds =
            UnityEngine.Object.FindAnyObjectByType<CameraBounds2D>();
        Require(cameraFollow != null && manager != null && camera != null &&
                cameraBounds != null,
            "Manager camera dependencies are missing.");

        float originalAspect = camera.aspect;
        foreach ((int width, int height) resolution in new[]
                 {
                     (1920, 1080),
                     (1366, 768)
                 })
        {
            camera.aspect = resolution.width / (float)resolution.height;
            Require(cameraFollow.ShowOverview(),
                $"Overview failed at {resolution.width}x{resolution.height}.");

            Bounds bounds = cameraBounds.WorldBounds;
            Vector3 minimum = camera.WorldToViewportPoint(bounds.min);
            Vector3 maximum = camera.WorldToViewportPoint(bounds.max);
            Vector3 entrance = camera.WorldToViewportPoint(
                RequireObject("EntranceThreshold").transform.position
            );
            Require(minimum.x >= -0.01f && maximum.x <= 1.01f &&
                    minimum.y >= 0.04f && maximum.y <= 0.93f &&
                    entrance.y >= 0.05f,
                $"Overview framing is unsafe at " +
                $"{resolution.width}x{resolution.height}.");

            CaptureCamera(
                camera,
                $"Overview_{resolution.width}x{resolution.height}",
                resolution.width,
                resolution.height
            );
        }

        camera.aspect = 1920f / 1080f;
        cameraFollow.ShowOverview();
        PC selectedPC = RequireObject("PC_02").GetComponent<PC>();
        Require(selectedPC != null &&
                manager.TrySelectAtWorldPosition(
                    selectedPC.transform.position
                ) &&
                manager.FocusSelectedObject(),
            "PC selection or Focus interaction is broken.");

        Vector3 selectedViewport = camera.WorldToViewportPoint(
            selectedPC.transform.position
        );
        Vector3 neighbourViewport = camera.WorldToViewportPoint(
            RequireObject("PC_03").transform.position
        );
        Vector3 privateRoomViewport = camera.WorldToViewportPoint(
            RequireObject("PrivateRoom01_Floor").transform.position
        );
        Vector3 deskViewport = camera.WorldToViewportPoint(
            RequireObject("AdministratorDesk").transform.position
        );

        Require(selectedViewport.x >= 0.12f &&
                selectedViewport.x <= 0.43f &&
                cameraFollow.CurrentOrthographicSize <= 4.7f,
            "Focus does not keep the selected PC left of the control panel.");
        Require(neighbourViewport.x > 0f && neighbourViewport.x < 0.72f &&
                neighbourViewport.y > 0.08f &&
                neighbourViewport.y < 0.92f,
            "Focus lost the nearest PC or aisle.");
        Require(privateRoomViewport.x >= 0.72f &&
                deskViewport.x <= 0.04f,
            "Remote rooms or a cropped reception desk dominate Focus.");

        CaptureCamera(camera, "Focus_1920x1080", 1920, 1080);

        GameObject terminal = RequireObject("PCExpansionTerminal");
        Require(manager.TrySelectAtWorldPosition(terminal.transform.position) &&
                manager.SelectedBehaviour is PCExpansionTerminal,
            "Terminal click interaction is broken.");

        manager.ClearSelection();
        Require(manager.ShowClubOverview() && !cameraFollow.IsFocused,
            "Closing selection does not return the camera to Overview.");
        camera.aspect = originalAspect;
    }

    private static void CaptureCamera(
        Camera camera,
        string fileName,
        int width,
        int height)
    {
        string directory = Path.GetFullPath(Path.Combine(
            Application.dataPath,
            "..",
            "TestArtifacts",
            "EnvironmentFoundationPolish"
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
                Path.Combine(directory, $"{fileName}.png"),
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
        return color.r * 0.2126f +
            color.g * 0.7152f +
            color.b * 0.0722f;
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

    private static void CapturePrimaryFingerprint()
    {
        EditorPrefs.SetString(
            PrimaryFingerprintKey,
            GetFileFingerprint(SaveStorageProfile.PrimarySavePath)
        );
    }

    private static void ValidatePrimaryFingerprint()
    {
        string before = EditorPrefs.GetString(
            PrimaryFingerprintKey,
            string.Empty
        );
        string after = GetFileFingerprint(SaveStorageProfile.PrimarySavePath);
        Require(string.Equals(before, after, StringComparison.Ordinal),
            "The primary user save changed during the polish smoke test.");
    }

    private static string GetFileFingerprint(string path)
    {
        if (!File.Exists(path))
        {
            return "missing";
        }

        using SHA256 sha256 = SHA256.Create();
        return Convert.ToBase64String(
            sha256.ComputeHash(File.ReadAllBytes(path))
        );
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
            !condition.StartsWith(
                "ENVIRONMENT_FOUNDATION_POLISH_SMOKE_TEST: FAIL") &&
            stackTrace.Contains("Assets/"))
        {
            RuntimeErrors.Add(condition);
        }
    }

    private static void Fail(Exception exception)
    {
        EditorPrefs.SetBool(FailedKey, true);
        Debug.LogError(
            $"ENVIRONMENT_FOUNDATION_POLISH_SMOKE_TEST: FAIL\n{exception}"
        );
        if (EditorApplication.isPlaying)
        {
            EditorApplication.isPlaying = false;
        }
        else
        {
            RestoreQASave();
            EditorPrefs.DeleteKey(PendingKey);
            EditorPrefs.DeleteKey(FailedKey);
            EditorPrefs.DeleteKey(PrimaryFingerprintKey);
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
