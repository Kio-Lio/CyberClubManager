using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class ManagerModeSmokeTest
{
    private const string PendingKey = "CyberClub.ManagerModeSmoke.Pending";
    private const string FailedKey = "CyberClub.ManagerModeSmoke.Failed";
    private const string HadSaveKey = "CyberClub.ManagerModeSmoke.HadSave";
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";
    private static readonly string SavePath = Path.Combine(
        Application.persistentDataPath,
        "cyber_club_save.json"
    );
    private static readonly string SaveBackupPath = Path.Combine(
        Path.GetTempPath(),
        "cyber_club_manager_mode_smoke_save.bak"
    );
    private static readonly List<string> RuntimeErrors = new();

    private static double verifyAt;

    static ManagerModeSmokeTest()
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
            verifyAt = EditorApplication.timeSinceStartup + 3.2d;
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

            Debug.Log("MANAGER_MODE_SMOKE_TEST: PASS");
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
            VerifyManagerMode();
            EditorApplication.isPlaying = false;
        }
        catch (Exception exception)
        {
            Fail(exception);
        }
    }

    private static void VerifyManagerMode()
    {
        Require(RuntimeErrors.Count == 0,
            $"Runtime errors were logged: {string.Join(" | ", RuntimeErrors)}");

        ManagerModeController manager =
            UnityEngine.Object.FindAnyObjectByType<ManagerModeController>();
        Require(manager != null, "ManagerModeController was not created.");

        GameObject player = GameObject.Find("Player");
        Require(player != null, "Player input container was not found.");
        Require(!player.GetComponent<PlayerController>().enabled,
            "PlayerController is still enabled.");
        Require(!player.GetComponent<PlayerInteraction>().enabled,
            "PlayerInteraction is still enabled.");
        Require(!player.GetComponent<Rigidbody2D>().simulated,
            "Player physics is still simulated.");

        foreach (Renderer renderer in player.GetComponentsInChildren<Renderer>(true))
        {
            Require(!renderer.enabled, "A player renderer is still visible.");
        }

        foreach (Collider2D collider in player.GetComponentsInChildren<Collider2D>(true))
        {
            Require(!collider.enabled, "A player collider is still enabled.");
        }

        CameraFollow cameraFollow =
            UnityEngine.Object.FindAnyObjectByType<CameraFollow>();
        Require(cameraFollow != null, "CameraFollow was not found.");
        Require(Mathf.Abs(cameraFollow.ZoomSpeedMultiplier - 4f) < 0.01f,
            "Camera zoom speed multiplier is not configured to 4x.");
        Require(cameraFollow.Target == null,
            "Camera still follows the hidden player.");

        Camera gameplayCamera = cameraFollow.GetComponent<Camera>();
        CameraBounds2D cameraBounds =
            UnityEngine.Object.FindAnyObjectByType<CameraBounds2D>();
        Require(gameplayCamera != null && cameraBounds != null,
            "Camera overview dependencies are missing.");
        Bounds clubBounds = cameraBounds.WorldBounds;
        Require(gameplayCamera.orthographicSize + 0.01f >=
                clubBounds.extents.y &&
            gameplayCamera.orthographicSize * gameplayCamera.aspect + 0.01f >=
                clubBounds.extents.x,
            "Manager camera does not frame the complete club.");

        float overviewSize = gameplayCamera.orthographicSize;
        gameplayCamera.orthographicSize = 4f;
        Vector3 beforePan = cameraFollow.transform.position;
        cameraFollow.Pan(Vector2.right * 0.25f);
        Require(cameraFollow.transform.position != beforePan,
            "Manager camera did not pan.");
        cameraFollow.ShowOverview();
        Require(Mathf.Abs(gameplayCamera.orthographicSize - overviewSize) < 0.1f,
            "Manager camera did not restore the club overview.");

        ManagerNavigationBar navigation =
            UnityEngine.Object.FindAnyObjectByType<ManagerNavigationBar>();
        Require(navigation != null && navigation.ButtonCount == 7,
            "Manager navigation was not created.");
        Require(!navigation.IsExpanded,
            "Manager navigation is not compact by default.");
        navigation.SetExpanded(true);
        Require(navigation.IsExpanded,
            "Compact manager navigation could not be expanded.");
        navigation.SetExpanded(false);
        Canvas navigationCanvas = navigation.GetComponentInParent<Canvas>();
        Require(navigationCanvas != null &&
                navigationCanvas.renderMode == RenderMode.ScreenSpaceOverlay,
            "Manager navigation is not attached to the screen HUD.");
        Require(navigation.TryOpenSection(
                ManagerNavigationSection.Maintenance),
            "Maintenance panel could not be opened from manager navigation.");
        Require(PCMaintenancePanel.Instance != null &&
                PCMaintenancePanel.Instance.IsOpen,
            "Maintenance panel did not open from the screen UI.");
        PCMaintenancePanel.Instance.Close();

        GameObject pcObject = GameObject.Find("PC_01");
        Require(pcObject != null, "PC_01 was not found.");
        Require(pcObject.GetComponent<PCVisualPresenter>() != null,
            "PC_01 did not receive the workstation visual presenter.");
        Require(pcObject.transform.Find("PCVisual") != null,
            "PC_01 layered workstation visual was not created.");
        Transform workstationVisual =
            pcObject.transform.Find("PCVisual/WorkstationSprite");
        SpriteRenderer workstationRenderer = workstationVisual != null
            ? workstationVisual.GetComponent<SpriteRenderer>()
            : null;
        Require(workstationRenderer != null &&
                workstationRenderer.sprite != null,
            "PC_01 tier workstation sprite was not loaded.");
        Require(workstationRenderer.sprite.texture.filterMode == FilterMode.Point,
            "PC workstation sprite is not using pixel filtering.");
        PC visualPC = pcObject.GetComponent<PC>();
        Require(workstationRenderer.sprite.name.Contains("Basic"),
            "PC_01 did not start with the Basic workstation sprite.");
        visualPC.RestoreTier(PCTier.Gaming);
        Require(workstationRenderer.sprite.name.Contains("Gaming"),
            "PC_01 did not switch to the Gaming workstation sprite.");
        visualPC.RestoreTier(PCTier.Premium);
        Require(workstationRenderer.sprite.name.Contains("Premium"),
            "PC_01 did not switch to the Premium workstation sprite.");
        visualPC.RestoreTier(PCTier.Basic);
        Require(!pcObject.GetComponent<SpriteRenderer>().enabled,
            "The old PC placeholder renderer is still visible.");

        PCExpansionTerminal expansionTerminal =
            UnityEngine.Object.FindAnyObjectByType<PCExpansionTerminal>();
        Require(expansionTerminal != null,
            "PC expansion terminal was not found.");
        Require(expansionTerminal.GetComponent<TerminalVisualPresenter>() != null,
            "PC expansion terminal did not receive the terminal visual presenter.");
        Require(expansionTerminal.transform.Find("TerminalVisual") != null,
            "Layered terminal visual was not created.");
        Require(manager.TryFocusAtWorldPosition(pcObject.transform.position),
            "PC_01 was not detected as a clickable object.");
        Require(!string.IsNullOrWhiteSpace(manager.CurrentPrompt),
            "Click interaction did not produce a HUD prompt.");
        Require(manager.TryActivateAtWorldPosition(pcObject.transform.position),
            "PC_01 could not be selected with a left-click action.");
        Require(manager.SelectedBehaviour == pcObject.GetComponent<PC>(),
            "Selected object state is incorrect.");
        Require(manager.FocusSelectedObject(),
            "Selected PC could not receive camera focus.");
        Require(cameraFollow.Target == pcObject.transform &&
                cameraFollow.IsFocused &&
                Mathf.Abs(gameplayCamera.orthographicSize -
                    cameraFollow.FocusOrthographicSize) < 0.1f,
            "Camera focus did not frame the selected PC.");
        Require(manager.ShowClubOverview() && cameraFollow.Target == null &&
                !cameraFollow.IsFocused,
            "Camera did not return from focus to club overview.");
        Require(UnityEngine.Object.FindAnyObjectByType<ManagerSelectionPanel>() != null,
            "Manager selection panel was not created.");

        ManagerCommandBar commandBar =
            UnityEngine.Object.FindAnyObjectByType<ManagerCommandBar>();
        Require(commandBar != null,
            "Manager command bar was not created.");
        Require(!commandBar.IsVisible,
            "Legacy build command bar is still permanently visible.");
        Require(commandBar.CanPurchasePC,
            "Manager command bar rejected an affordable PC purchase.");

        ManagerBuildController buildController =
            UnityEngine.Object.FindAnyObjectByType<ManagerBuildController>();
        Require(buildController != null,
            "ManagerBuildController was not created.");
        Transform buildPanelTransform = ClubHUDCanvas.Instance.transform.Find(
            "ManagerBuildPanel"
        );
        GameObject buildPanel = buildPanelTransform != null
            ? buildPanelTransform.gameObject
            : null;
        Require(buildPanel != null && !buildPanel.activeSelf,
            "Build status panel is visible outside placement mode.");
        Require(commandBar.TryBeginPCPlacement(),
            "PC placement mode did not start from the manager command bar.");
        Require(buildPanel.activeSelf,
            "Build status panel did not appear during placement mode.");

        Vector2 placementPosition = new Vector2(6.5f, -0.5f);
        Require(buildController.IsPlacementValid(placementPosition),
            "Known expansion position was rejected.");
        Require(buildController.TryPlaceAt(placementPosition),
            "PC placement did not complete.");

        GameObject purchasedPC = GameObject.Find("PC_06");
        Require(purchasedPC != null, "PC_06 was not created.");
        Require(Vector2.Distance(
            purchasedPC.transform.position,
            placementPosition
        ) < 0.01f, "PC_06 was not placed at the selected grid position.");
        Require(EconomyManager.Instance.Money == 700,
            "PC purchase did not deduct exactly 500 rubles.");

        PCExpansionManager expansion = PCExpansionManager.Instance;
        PC purchasedPCComponent = purchasedPC.GetComponent<PC>();
        Require(expansion != null && expansion.CanMovePC(purchasedPCComponent),
            "Purchased PC is not available for moving.");
        Require(buildController.BeginPCMove(purchasedPCComponent),
            "PC move mode did not start.");

        Vector2 movedPosition = new Vector2(6.5f, -3.5f);
        Require(buildController.IsPlacementValid(movedPosition),
            "Known moved PC position was rejected.");
        Require(buildController.TryPlaceAt(movedPosition),
            "PC move did not complete.");
        Require(Vector2.Distance(
            purchasedPC.transform.position,
            movedPosition
        ) < 0.01f, "PC_06 was not moved to the selected grid position.");
        Require(EconomyManager.Instance.Money == 700,
            "Moving a PC changed the club balance.");

        Require(SaveManager.Instance.TrySaveGame(),
            "Manager mode state could not be saved.");
        GameSaveData savedData = JsonUtility.FromJson<GameSaveData>(
            File.ReadAllText(SavePath)
        );
        Require(savedData != null && savedData.version == 20,
            "Manager mode save version is not 20.");
        PCSaveData savedPC = savedData.pcs.Find(
            item => item != null && item.objectName == "PC_06"
        );
        Require(savedPC != null && savedPC.hasPosition &&
            Mathf.Abs(savedPC.positionX - movedPosition.x) < 0.01f &&
            Mathf.Abs(savedPC.positionY - movedPosition.y) < 0.01f,
            "PC_06 custom position was not saved.");

        int incomeBeforeSale = EconomyManager.Instance.TotalIncome;
        Require(expansion.CanSellPC(purchasedPCComponent),
            "Last purchased PC is not available for sale.");
        Require(expansion.TrySellPC(purchasedPCComponent),
            "PC sale did not complete.");
        Require(!purchasedPC.activeSelf,
            "Sold PC remained active until the end of the frame.");
        Require(expansion.PurchasedPCCount == 0,
            "PC sale did not release the expansion slot.");
        Require(EconomyManager.Instance.Money == 950,
            "PC sale did not refund exactly 250 rubles.");
        Require(EconomyManager.Instance.TotalIncome == incomeBeforeSale,
            "PC sale refund was counted as operating revenue.");

        Require(SaveManager.Instance.TrySaveGame(),
            "State after the PC sale could not be saved.");
        GameSaveData soldState = JsonUtility.FromJson<GameSaveData>(
            File.ReadAllText(SavePath)
        );
        Require(soldState != null && soldState.purchasedPCCount == 0,
            "Released expansion slot was not saved.");
        Require(soldState.pcs.Find(
            item => item != null && item.objectName == "PC_06"
        ) == null, "Sold PC remained in the save data.");

        Require(UnityEngine.Object.FindAnyObjectByType<PauseMenuController>() != null,
            "Pause menu input component was removed.");
        Require(RuntimeErrors.Count == 0,
            $"Runtime errors were logged: {string.Join(" | ", RuntimeErrors)}");
    }

    private static void OnLogMessageReceived(
        string condition,
        string stackTrace,
        LogType type)
    {
        if ((type == LogType.Error ||
             type == LogType.Exception ||
             type == LogType.Assert) &&
            !condition.StartsWith("MANAGER_MODE_SMOKE_TEST: FAIL") &&
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
        Debug.LogError($"MANAGER_MODE_SMOKE_TEST: FAIL - {exception}");
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
