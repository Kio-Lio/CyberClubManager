using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class EnvironmentLayoutSmokeTest
{
    private const string PendingKey = "CyberClub.EnvironmentLayoutSmoke.Pending";
    private const string FailedKey = "CyberClub.EnvironmentLayoutSmoke.Failed";
    private const string HadSaveKey = "CyberClub.EnvironmentLayoutSmoke.HadSave";
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";

    private static readonly string SavePath = Path.Combine(
        Application.persistentDataPath,
        "cyber_club_save.json"
    );
    private static readonly string SaveBackupPath = Path.Combine(
        Path.GetTempPath(),
        "cyber_club_environment_layout_smoke_save.bak"
    );
    private static readonly List<string> RuntimeErrors = new();

    private static double verifyAt;

    static EnvironmentLayoutSmokeTest()
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
            BackupSaveFile();
            RuntimeErrors.Clear();
            if (File.Exists(SavePath))
            {
                File.Delete(SavePath);
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
            verifyAt = EditorApplication.timeSinceStartup + 4d;
        }
        else if (state == PlayModeStateChange.EnteredEditMode)
        {
            bool failed = EditorPrefs.GetBool(FailedKey, false);
            RestoreSaveFile();
            EditorPrefs.DeleteKey(PendingKey);
            EditorPrefs.DeleteKey(FailedKey);

            if (failed)
            {
                EditorApplication.Exit(1);
                return;
            }

            Debug.Log("ENVIRONMENT_LAYOUT_SMOKE_TEST: PASS");
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
            VerifyEnvironmentLayout();
            EditorApplication.isPlaying = false;
        }
        catch (Exception exception)
        {
            Fail(exception);
        }
    }

    private static void VerifyEnvironmentLayout()
    {
        Require(RuntimeErrors.Count == 0,
            $"Runtime errors were logged: {string.Join(" | ", RuntimeErrors)}");

        ClubLayoutBuilder layout =
            UnityEngine.Object.FindAnyObjectByType<ClubLayoutBuilder>();
        CameraBounds2D cameraBounds =
            UnityEngine.Object.FindAnyObjectByType<CameraBounds2D>();
        CameraFollow cameraFollow =
            UnityEngine.Object.FindAnyObjectByType<CameraFollow>();
        ManagerModeController manager =
            UnityEngine.Object.FindAnyObjectByType<ManagerModeController>();
        Require(layout != null && cameraBounds != null &&
                cameraFollow != null && manager != null,
            "Environment or camera dependencies are missing.");

        Require(Vector2.Distance(layout.RoomCenter, new Vector2(3.2f, 0f)) < 0.01f,
            $"Unexpected room center: {layout.RoomCenter}.");
        Require(Vector2.Distance(layout.RoomSize, new Vector2(24f, 10f)) < 0.01f,
            $"Unexpected room size: {layout.RoomSize}.");
        Bounds clubBounds = cameraBounds.WorldBounds;
        Require(Mathf.Abs(clubBounds.center.x - 3.2f) < 0.01f &&
                clubBounds.size.x < 24f && clubBounds.size.x > 23f,
            $"Camera bounds do not match the useful club area: {clubBounds}.");

        GameObject floor = RequireObject("Floor");
        Require(Vector2.Distance(floor.transform.position, layout.RoomCenter) < 0.01f &&
                Vector2.Distance(floor.transform.localScale, layout.RoomSize) < 0.01f,
            "The dark floor does not cover the normalized club bounds.");
        RequireObject("FloorZone_Reception");
        RequireObject("FloorZone_MainHall");
        RequireObject("FloorZone_PrivateRooms");
        RequireObject("PrivateRoom01_Floor");
        RequireObject("VIPRoom01_Floor");

        ValidateTerminalLayout();
        ValidateVisualScaleAndIndicators();
        ValidatePlaceholderPolicy();
        ValidateCharactersAndTrash();
        ValidateCameraComposition(cameraFollow, cameraBounds, manager);

        PC pc = RequireObject("PC_01").GetComponent<PC>();
        Require(pc != null && manager.TrySelectAtWorldPosition(pc.transform.position) &&
                manager.SelectedBehaviour == pc,
            "PC interaction changed during the layout cleanup.");
        manager.ClearSelection();

        Require(RuntimeErrors.Count == 0,
            $"Runtime errors were logged: {string.Join(" | ", RuntimeErrors)}");
    }

    private static void ValidateTerminalLayout()
    {
        string[] terminalNames =
        {
            "ClubResearchTerminal",
            "InternetProviderTerminal",
            "MarketingTerminal",
            "ConsumableStockTerminal",
            "PricingTerminal",
            "MaintenanceTerminal",
            "PCExpansionTerminal"
        };

        float previousY = float.PositiveInfinity;
        foreach (string terminalName in terminalNames)
        {
            GameObject terminal = RequireObject(terminalName);
            Require(Mathf.Abs(terminal.transform.position.x + 7.55f) < 0.01f,
                $"{terminalName} is outside the service terminal line.");
            Require(terminal.transform.position.y < previousY,
                "Service terminals are not ordered from top to bottom.");
            Require(Vector2.Distance(
                    terminal.transform.localScale,
                    new Vector2(0.7f, 0.9f)) < 0.01f,
                $"{terminalName} has an inconsistent scale.");
            Require(terminal.GetComponent<TerminalVisualPresenter>() != null,
                $"{terminalName} has no visual presenter.");
            Require(!terminal.GetComponent<SpriteRenderer>().enabled,
                $"{terminalName} still shows its placeholder renderer.");
            previousY = terminal.transform.position.y;
        }
    }

    private static void ValidateVisualScaleAndIndicators()
    {
        GameObject pcObject = RequireObject("PC_01");
        Transform workstationTransform =
            pcObject.transform.Find("PCVisual/WorkstationSprite");
        SpriteRenderer workstation = workstationTransform != null
            ? workstationTransform.GetComponent<SpriteRenderer>()
            : null;
        SpriteRenderer reception =
            RequireObject("AdministratorDesk").GetComponent<SpriteRenderer>();
        Require(workstation != null && workstation.sprite != null &&
                reception != null && reception.sprite != null,
            "Workstation or reception sprite is missing.");

        float receptionRatio =
            reception.sprite.bounds.size.x / workstation.sprite.bounds.size.x;
        Require(receptionRatio >= 2.5f && receptionRatio <= 3f,
            $"Reception scale ratio is {receptionRatio:F2}, expected 2.5-3.0.");
        float characterRatio =
            CharacterVisualPresenter.VisualWidth / workstation.sprite.bounds.size.x;
        Require(characterRatio >= 0.35f && characterRatio <= 0.45f,
            $"Character scale ratio is {characterRatio:F2}, expected 0.35-0.45.");

        Transform statusLight = pcObject.transform.Find("PCVisual/StatusLight");
        Transform tierAccent = pcObject.transform.Find("PCVisual/TierAccent");
        Require(statusLight != null && statusLight.localScale.x <= 0.075f &&
                tierAccent != null && tierAccent.localScale.x <= 0.75f &&
                tierAccent.localScale.y <= 0.03f,
            "PC status indicators are still oversized.");

        foreach (RoomDoor door in UnityEngine.Object.FindObjectsByType<RoomDoor>())
        {
            Require(door.GetComponent<RoomDoorVisualPresenter>() != null,
                $"{door.name} has no environment door visual.");
            Require(!door.GetComponent<SpriteRenderer>().enabled,
                $"{door.name} still shows the red/green placeholder.");
        }
    }

    private static void ValidatePlaceholderPolicy()
    {
        ClubWorldVisualBootstrap bootstrap =
            UnityEngine.Object.FindAnyObjectByType<ClubWorldVisualBootstrap>();
        Require(bootstrap != null && !bootstrap.ShowDebugVisuals,
            "Debug visuals are enabled by default.");

        foreach (ClientNavigationNode node in
                 UnityEngine.Object.FindObjectsByType<ClientNavigationNode>())
        {
            foreach (SpriteRenderer renderer in
                     node.GetComponentsInChildren<SpriteRenderer>(true))
            {
                Require(!renderer.enabled,
                    $"Navigation marker is visible: {node.name}/{renderer.name}.");
            }
        }

        GameObject player = GameObject.Find("Player");
        Require(player != null, "Player compatibility object is missing.");
        foreach (SpriteRenderer renderer in
                 player.GetComponentsInChildren<SpriteRenderer>(true))
        {
            Require(!renderer.enabled,
                $"Hidden manager-mode player renderer is visible: {renderer.name}.");
        }

        Require(GameObject.Find("PCPlacementPreview") == null &&
                GameObject.Find("ManagerSelectionIndicator") == null,
            "A contextual technical marker is visible by default.");
    }

    private static void ValidateCharactersAndTrash()
    {
        ClientSpawner spawner =
            UnityEngine.Object.FindAnyObjectByType<ClientSpawner>();
        Require(spawner != null, "ClientSpawner is missing.");
        spawner.QASpawnClient(ClientType.Regular);
        spawner.QASpawnClient(ClientType.Gamer);
        spawner.QASpawnClient(ClientType.VIP);

        Client[] clients = UnityEngine.Object.FindObjectsByType<Client>();
        Require(clients.Length >= 3, "QA clients were not spawned.");
        foreach (Client client in clients)
        {
            CharacterVisualPresenter presenter =
                client.GetComponent<CharacterVisualPresenter>();
            Require(presenter != null &&
                    !client.GetComponent<SpriteRenderer>().enabled &&
                    client.transform.localScale == Vector3.one,
                $"{client.name} still uses a bright square placeholder.");
        }

        CleanerManager cleanerManager = CleanerManager.Instance;
        Require(cleanerManager != null, "CleanerManager is missing.");
        cleanerManager.RestoreState(true);
        CleanerAgent cleaner = cleanerManager.CleanerAgent;
        Require(cleaner != null &&
                cleaner.GetComponent<CharacterVisualPresenter>() != null &&
                !cleaner.GetComponent<SpriteRenderer>().enabled,
            "Cleaner still uses a bright rectangular placeholder.");

        ClubCleanlinessManager cleanliness = ClubCleanlinessManager.Instance;
        PC pc = RequireObject("PC_01").GetComponent<PC>();
        Require(cleanliness != null && pc != null,
            "Cleanliness dependencies are missing.");
        cleanliness.EnsureTutorialTrash(pc);
        Require(cleanliness.ActiveTrashItems.Count > 0,
            "Test trash was not created.");
        TrashItem trash = cleanliness.ActiveTrashItems[0];
        Require(trash.GetComponent<TrashVisualPresenter>() != null &&
                !trash.GetComponent<SpriteRenderer>().enabled,
            "Trash still uses a brown square placeholder.");

        cleanerManager.RestoreState(false);
    }

    private static void ValidateCameraComposition(
        CameraFollow cameraFollow,
        CameraBounds2D cameraBounds,
        ManagerModeController manager)
    {
        Camera gameplayCamera = cameraFollow.GetComponent<Camera>();
        Require(gameplayCamera != null, "Gameplay camera is missing.");

        float originalAspect = gameplayCamera.aspect;
        Vector2Int[] resolutions = { new(1920, 1080), new(1366, 768) };
        foreach (Vector2Int resolution in resolutions)
        {
            gameplayCamera.aspect = resolution.x / (float)resolution.y;
            Require(cameraFollow.ShowOverview(),
                $"Overview failed at {resolution.x}x{resolution.y}.");

            Bounds bounds = cameraBounds.WorldBounds;
            Vector3 minimum = gameplayCamera.WorldToViewportPoint(bounds.min);
            Vector3 maximum = gameplayCamera.WorldToViewportPoint(bounds.max);
            Require(minimum.x >= -0.01f && maximum.x <= 1.01f &&
                    minimum.y >= 0.06f && maximum.y <= 0.89f,
                $"Club framing overlaps HUD or leaves the viewport at " +
                $"{resolution.x}x{resolution.y}: {minimum}..{maximum}.");
        }

        PC pc = RequireObject("PC_01").GetComponent<PC>();
        manager.SelectBehaviour(pc);
        Require(manager.FocusSelectedObject(),
            "Selected PC could not receive camera focus.");
        float selectedViewportX = gameplayCamera.WorldToViewportPoint(
            pc.transform.position
        ).x;
        Require(selectedViewportX >= 0.08f && selectedViewportX <= 0.48f,
            $"Focused object is not left of the selection panel: " +
            $"viewport x={selectedViewportX:F2}.");

        manager.ClearSelection();
        gameplayCamera.aspect = originalAspect;
        cameraFollow.ShowOverview();
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

    private static void OnLogMessageReceived(
        string condition,
        string stackTrace,
        LogType type)
    {
        if ((type == LogType.Error || type == LogType.Exception ||
             type == LogType.Assert) &&
            !condition.StartsWith("ENVIRONMENT_LAYOUT_SMOKE_TEST: FAIL") &&
            stackTrace.Contains("Assets/"))
        {
            RuntimeErrors.Add(condition);
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private static void BackupSaveFile()
    {
        bool hadSave = File.Exists(SavePath);
        EditorPrefs.SetBool(HadSaveKey, hadSave);
        if (hadSave)
        {
            File.Copy(SavePath, SaveBackupPath, true);
        }
        else if (File.Exists(SaveBackupPath))
        {
            File.Delete(SaveBackupPath);
        }
    }

    private static void RestoreSaveFile()
    {
        bool hadSave = EditorPrefs.GetBool(HadSaveKey, false);
        if (hadSave && File.Exists(SaveBackupPath))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SavePath));
            File.Copy(SaveBackupPath, SavePath, true);
            File.Delete(SaveBackupPath);
        }
        else if (!hadSave && File.Exists(SavePath))
        {
            File.Delete(SavePath);
        }

        EditorPrefs.DeleteKey(HadSaveKey);
    }

    private static void Fail(Exception exception)
    {
        Debug.LogError($"ENVIRONMENT_LAYOUT_SMOKE_TEST: FAIL - {exception}");
        EditorPrefs.SetBool(FailedKey, true);
        if (EditorApplication.isPlaying)
        {
            EditorApplication.isPlaying = false;
            return;
        }

        RestoreSaveFile();
        EditorPrefs.DeleteKey(PendingKey);
        EditorPrefs.DeleteKey(FailedKey);
        EditorApplication.Exit(1);
    }
}
