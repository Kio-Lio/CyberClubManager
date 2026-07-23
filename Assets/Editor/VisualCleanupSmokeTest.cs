using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[InitializeOnLoad]
public static class VisualCleanupSmokeTest
{
    private const string PendingKey = "CyberClub.VisualCleanupSmoke.Pending";
    private const string FailedKey = "CyberClub.VisualCleanupSmoke.Failed";
    private const string HadSaveKey = "CyberClub.VisualCleanupSmoke.HadSave";
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";

    private static readonly string SavePath = SaveStorageProfile.QASavePath;
    private static readonly string SaveBackupPath = Path.Combine(
        Path.GetTempPath(),
        "cyber_club_visual_cleanup_smoke_save.bak"
    );
    private static readonly List<string> RuntimeErrors = new();

    private static double verifyAt;

    static VisualCleanupSmokeTest()
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
            verifyAt = EditorApplication.timeSinceStartup + 3.5d;
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

            Debug.Log("VISUAL_CLEANUP_SMOKE_TEST: PASS");
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
            VerifyVisualCleanup();
            EditorApplication.isPlaying = false;
        }
        catch (Exception exception)
        {
            Fail(exception);
        }
    }

    private static void VerifyVisualCleanup()
    {
        Require(RuntimeErrors.Count == 0,
            $"Runtime errors were logged: {string.Join(" | ", RuntimeErrors)}");

        ClubHUDCanvas hud = UnityEngine.Object.FindAnyObjectByType<ClubHUDCanvas>();
        Require(hud != null, "ClubHUDCanvas was not created.");
        hud.SetMode(ClubHUDMode.Compact);
        RectTransform canvasRect = hud.GetComponent<RectTransform>();
        Require(canvasRect != null, "HUD canvas has no RectTransform.");

        Transform root = hud.transform.Find("GameplayHUDRoot");
        Transform compact = root?.Find("CompactSection");
        Transform economy = compact?.Find("EconomyPanel");
        Transform progression = compact?.Find("ProgressionPanel");
        Transform operations = compact?.Find("OperationsPanel");
        Transform pcStatus = compact?.Find("PCStatusPanel");
        Require(root != null && compact != null && economy != null &&
                progression != null && operations != null && pcStatus != null,
            "One or more permanent HUD blocks are missing.");

        int activePermanentPanels = 0;
        foreach (Transform child in compact)
        {
            if (child.gameObject.activeSelf && child.GetComponent<Image>() != null)
            {
                activePermanentPanels++;
            }
        }
        Require(activePermanentPanels == 4,
            $"Expected four permanent HUD blocks, found {activePermanentPanels}.");
        Require(compact.Find("DailyGoalPanel") == null,
            "Daily goal still occupies a separate permanent panel.");

        Transform warnings = root.Find("WarningSection");
        Transform expanded = root.Find("ExpandedSection");
        Require(warnings != null && !warnings.gameObject.activeSelf,
            "Legacy warning panel is visible.");
        Require(expanded != null && expanded.GetComponent<Image>() == null,
            "Expanded mode still draws a permanent analytics frame.");

        Canvas.ForceUpdateCanvases();
        Rect economyRect = GetRectInCanvas(canvasRect, (RectTransform)economy);
        Rect progressionRect = GetRectInCanvas(canvasRect, (RectTransform)progression);
        Rect operationsRect = GetRectInCanvas(canvasRect, (RectTransform)operations);
        Rect pcStatusRect = GetRectInCanvas(canvasRect, (RectTransform)pcStatus);

        Require(IsInside(canvasRect.rect, economyRect, 15f) &&
                IsInside(canvasRect.rect, progressionRect, 15f) &&
                IsInside(canvasRect.rect, operationsRect, 15f) &&
                IsInside(canvasRect.rect, pcStatusRect, 15f),
            "A permanent HUD block is outside the Canvas safe area.");
        Require(!economyRect.Overlaps(progressionRect) &&
                !progressionRect.Overlaps(operationsRect) &&
                !economyRect.Overlaps(operationsRect),
            "Top HUD blocks overlap.");

        float topReserved = canvasRect.rect.yMax - Mathf.Min(
            economyRect.yMin,
            Mathf.Min(progressionRect.yMin, operationsRect.yMin)
        );
        float bottomReserved = pcStatusRect.yMax - canvasRect.rect.yMin;
        float worldHeightFraction =
            (canvasRect.rect.height - topReserved - bottomReserved) /
            canvasRect.rect.height;
        Require(worldHeightFraction >= 0.75f,
            $"HUD leaves only {worldHeightFraction:P0} of the vertical game view.");
        ValidateRequestedResolutions(worldHeightFraction);

        Transform buildPanel = hud.transform.Find("ManagerBuildPanel");
        Require(buildPanel != null && !buildPanel.gameObject.activeSelf,
            "Build panel is visible outside placement mode.");

        ManagerNavigationBar navigation =
            UnityEngine.Object.FindAnyObjectByType<ManagerNavigationBar>();
        Require(navigation != null && !navigation.IsExpanded,
            $"Manager navigation is not compact by default: " +
            $"exists={navigation != null}, " +
            $"visible={navigation?.IsVisible}, " +
            $"expanded={navigation?.IsExpanded}, " +
            $"blocked={GameplayInputState.IsBlocked}, " +
            $"hud={hud.CurrentMode}.");
        navigation.SetExpanded(true);
        Require(navigation.IsExpanded,
            "Manager navigation did not expand on demand.");
        navigation.SetExpanded(false);

        Require(PricingPanel.Instance != null &&
                PCMaintenancePanel.Instance != null,
            "Administrative panels are missing.");
        Require(navigation.TryOpenSection(ManagerNavigationSection.Pricing),
            "Pricing panel did not open from manager navigation.");
        Require(!navigation.TryOpenSection(ManagerNavigationSection.Maintenance) &&
                !PCMaintenancePanel.Instance.IsOpen,
            "More than one large administrative panel could be opened.");
        PricingPanel.Instance.Close();

        FirstDayTutorialManager tutorial = FirstDayTutorialManager.Instance;
        Transform tutorialRoot = hud.transform.Find("FirstDayTutorialPanelRoot");
        Require(tutorialRoot != null, "Tutorial panel root is missing.");
        if (tutorial != null && tutorial.IsTutorialActive)
        {
            tutorial.SkipTutorial();
        }
        Require(!tutorialRoot.gameObject.activeSelf,
            "Tutorial panel remained visible after completion or skip.");

        ClientFeedbackUI feedback = hud.GetComponent<ClientFeedbackUI>();
        Require(feedback != null && feedback.MaximumVisibleCards <= 3 &&
                feedback.ActiveCardCount <= 3,
            "Client feedback can exceed three visible cards.");

        ManagerModeController manager =
            UnityEngine.Object.FindAnyObjectByType<ManagerModeController>();
        CameraFollow cameraFollow =
            UnityEngine.Object.FindAnyObjectByType<CameraFollow>();
        CameraBounds2D cameraBounds =
            UnityEngine.Object.FindAnyObjectByType<CameraBounds2D>();
        Require(manager != null && cameraFollow != null && cameraBounds != null,
            "Manager camera dependencies are missing.");
        Require(manager.ShowClubOverview(), "Club overview could not be shown.");

        Camera gameplayCamera = cameraFollow.GetComponent<Camera>();
        Bounds bounds = cameraBounds.WorldBounds;
        Require(cameraFollow.Target == null && !cameraFollow.IsFocused &&
                gameplayCamera.orthographicSize + 0.01f >= bounds.extents.y &&
                gameplayCamera.orthographicSize * gameplayCamera.aspect + 0.01f >=
                    bounds.extents.x,
            "Club overview does not frame the useful club bounds.");

        PC pc = GameObject.Find("PC_01")?.GetComponent<PC>();
        Require(pc != null, "PC_01 is missing.");
        Require(manager.TrySelectAtWorldPosition(pc.transform.position) &&
                manager.SelectedBehaviour == pc,
            "PC remained unclickable after debug visuals were hidden.");
        Require(manager.FocusSelectedObject() && cameraFollow.Target == pc.transform &&
                cameraFollow.IsFocused,
            "Camera could not focus the selected PC.");
        Require(manager.ShowClubOverview() && cameraFollow.Target == null,
            "Camera could not return to overview after focus.");
        manager.ClearSelection();

        ClubWorldVisualBootstrap visualBootstrap =
            UnityEngine.Object.FindAnyObjectByType<ClubWorldVisualBootstrap>();
        Require(visualBootstrap != null && !visualBootstrap.ShowDebugVisuals,
            "Runtime debug visuals are enabled by default.");
        foreach (ClientNavigationNode node in
                 UnityEngine.Object.FindObjectsByType<ClientNavigationNode>())
        {
            foreach (SpriteRenderer renderer in
                     node.GetComponentsInChildren<SpriteRenderer>(true))
            {
                Require(!renderer.enabled,
                    $"Navigation marker is visible: {renderer.name}.");
            }
        }

        SpriteRenderer pcPlaceholder = pc.GetComponent<SpriteRenderer>();
        Require(pcPlaceholder == null || !pcPlaceholder.enabled,
            "Legacy PC placeholder renderer is visible.");
        PCExpansionTerminal terminal =
            UnityEngine.Object.FindAnyObjectByType<PCExpansionTerminal>();
        Require(terminal != null &&
                !terminal.GetComponent<SpriteRenderer>().enabled,
            "Legacy terminal placeholder renderer is visible.");
        Require(GameObject.Find("PCPlacementPreview") == null &&
                GameObject.Find("ManagerSelectionIndicator") == null,
            "A technical placement or selection marker is visible by default.");

        foreach (Text text in Resources.FindObjectsOfTypeAll<Text>())
        {
            if (!text.gameObject.scene.IsValid() ||
                !text.gameObject.activeInHierarchy)
            {
                continue;
            }

            string value = text.text ?? string.Empty;
            Require(!value.Contains(Application.persistentDataPath,
                    StringComparison.OrdinalIgnoreCase) &&
                    !value.Contains("cyber_club_save.json",
                    StringComparison.OrdinalIgnoreCase),
                "A technical save path is visible in the gameplay HUD.");
        }

        Require(RuntimeErrors.Count == 0,
            $"Runtime errors were logged: {string.Join(" | ", RuntimeErrors)}");
    }

    private static Rect GetRectInCanvas(
        RectTransform canvasRect,
        RectTransform childRect)
    {
        Vector3[] corners = new Vector3[4];
        childRect.GetWorldCorners(corners);
        Vector3 minimum = canvasRect.InverseTransformPoint(corners[0]);
        Vector3 maximum = canvasRect.InverseTransformPoint(corners[2]);
        return Rect.MinMaxRect(minimum.x, minimum.y, maximum.x, maximum.y);
    }

    private static bool IsInside(Rect parent, Rect child, float margin)
    {
        return child.xMin >= parent.xMin + margin &&
            child.xMax <= parent.xMax - margin &&
            child.yMin >= parent.yMin + margin &&
            child.yMax <= parent.yMax - margin;
    }

    private static void ValidateRequestedResolutions(float worldHeightFraction)
    {
        Vector2Int[] resolutions =
        {
            new(1920, 1080),
            new(1600, 900),
            new(1366, 768),
            new(2560, 1440)
        };

        foreach (Vector2Int resolution in resolutions)
        {
            float aspect = resolution.x / (float)resolution.y;
            Require(Mathf.Abs(aspect - 16f / 9f) < 0.002f,
                $"Unexpected test aspect ratio: {resolution.x}x{resolution.y}.");
            Require(worldHeightFraction >= 0.75f,
                $"HUD is too large at {resolution.x}x{resolution.y}.");
        }
    }

    private static void OnLogMessageReceived(
        string condition,
        string stackTrace,
        LogType type)
    {
        if ((type == LogType.Error || type == LogType.Exception ||
             type == LogType.Assert) &&
            !condition.StartsWith("VISUAL_CLEANUP_SMOKE_TEST: FAIL") &&
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
        Debug.LogError($"VISUAL_CLEANUP_SMOKE_TEST: FAIL - {exception}");
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
