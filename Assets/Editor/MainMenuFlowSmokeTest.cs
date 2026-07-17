using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class MainMenuFlowSmokeTest
{
    private const string PendingKey = "CyberClub.MainMenuSmoke.Pending";
    private const string PhaseKey = "CyberClub.MainMenuSmoke.Phase";
    private const string BackupPrefsKey = "CyberClub.MainMenuSmoke.Prefs";
    private const string HadSaveKey = "CyberClub.MainMenuSmoke.HadSave";
    private const string HadSettingsKey = "CyberClub.MainMenuSmoke.HadSettings";
    private const string HadCorruptedSettingsKey =
        "CyberClub.MainMenuSmoke.HadCorruptedSettings";
    private const string MainMenuScenePath = "Assets/Scenes/MainMenu.unity";
    private static readonly string SavePath = Path.Combine(
        Application.persistentDataPath,
        "cyber_club_save.json"
    );
    private static readonly string SaveBackupPath = Path.Combine(
        Path.GetTempPath(),
        "cyber_club_main_menu_smoke_save.bak"
    );
    private static readonly string SettingsPath = Path.Combine(
        Application.persistentDataPath,
        "settings.json"
    );
    private static readonly string CorruptedSettingsPath = Path.Combine(
        Application.persistentDataPath,
        "settings.corrupted.json"
    );
    private static readonly string SettingsBackupPath = Path.Combine(
        Path.GetTempPath(),
        "cyber_club_main_menu_smoke_settings.bak"
    );
    private static readonly string CorruptedSettingsBackupPath = Path.Combine(
        Path.GetTempPath(),
        "cyber_club_main_menu_smoke_corrupted_settings.bak"
    );

    private static double nextCheckAt;

    [Serializable]
    private sealed class PreferenceBackup
    {
        public bool hasVolume;
        public float volume;
        public bool hasFullscreen;
        public int fullscreen;
        public bool hasWidth;
        public int width;
        public bool hasHeight;
        public int height;
        public bool hasScale;
        public float scale;
        public bool hasEffects;
        public int effects;
    }

    static MainMenuFlowSmokeTest()
    {
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    public static void Run()
    {
        try
        {
            BackupEnvironment();
            SaveManager.DeleteSaveFile();
            EditorPrefs.SetBool(PendingKey, true);
            EditorPrefs.SetInt(PhaseKey, 0);
            EditorSceneManager.OpenScene(MainMenuScenePath, OpenSceneMode.Single);
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
            nextCheckAt = EditorApplication.timeSinceStartup + 1.5d;
            return;
        }

        if (state == PlayModeStateChange.EnteredEditMode &&
            EditorPrefs.GetInt(PhaseKey, -1) == 7)
        {
            RestoreEnvironment();
            EditorPrefs.DeleteKey(PendingKey);
            EditorPrefs.DeleteKey(PhaseKey);
            Debug.Log("MAIN_MENU_FLOW_SMOKE_TEST: PASS");
            EditorApplication.Exit(0);
        }
    }

    private static void Tick()
    {
        if (!EditorPrefs.GetBool(PendingKey, false) ||
            !EditorApplication.isPlaying ||
            EditorApplication.timeSinceStartup < nextCheckAt)
        {
            return;
        }

        nextCheckAt = double.MaxValue;

        try
        {
            switch (EditorPrefs.GetInt(PhaseKey, 0))
            {
                case 0:
                    VerifyInitialMenuAndStartNewGame();
                    break;
                case 1:
                    VerifyNewGameAndReturnToMenu();
                    break;
                case 2:
                    VerifySavedMenuAndContinue();
                    break;
                case 3:
                    VerifyVersion19AndPrepareVersion18();
                    break;
                case 4:
                    VerifyVersion18CardAndContinue();
                    break;
                case 5:
                    VerifyVersion18LoadAndReturn();
                    break;
                case 6:
                    VerifyFinalMenuAndExit();
                    break;
            }
        }
        catch (Exception exception)
        {
            Fail(exception);
        }
    }

    private static void VerifyInitialMenuAndStartNewGame()
    {
        Require(SceneManager.GetActiveScene().name == "MainMenu",
            "Application did not start in MainMenu.");
        MainMenuController menu = RequireMenuOnlyScene();
        Require(!SaveManager.HasSaveFile(), "Smoke test save was not cleared.");
        Require(!menu.ContinueAvailable,
            "Continue must be disabled without a save.");
        ConfigureSmokeSettings();
        VerifyStoredSettings();
        VerifySettingsRecovery();
        ConfigureSmokeSettings();

        GameSaveData version18 = new GameSaveData
        {
            version = 18,
            currentDay = 6,
            money = 4850,
            clubLevel = 3,
            reputation = 67
        };
        File.WriteAllText(SavePath, JsonUtility.ToJson(version18, true));
        menu.RefreshSaveState();
        Require(menu.ContinueAvailable,
            "Version 18 save was not accepted by the menu.");
        Require(menu.SaveSummary.day == 6 &&
                menu.SaveSummary.balance == 4850 &&
                menu.SaveSummary.clubLevel == 3 &&
                menu.SaveSummary.reputation == 67,
            "Version 18 summary fallback is incorrect.");

        File.WriteAllText(SavePath, "{ definitely not valid json");
        menu.RefreshSaveState();
        Require(menu.SaveIsCorrupted && !menu.ContinueAvailable,
            "Corrupted save state was not shown safely.");
        Require(SaveManager.DeleteSaveFile(),
            "Corrupted save could not be deleted.");
        VerifyStoredSettings();

        GameSaveData version19 = new GameSaveData
        {
            version = 19,
            savedAtUtc = DateTime.UtcNow.ToString("O"),
            savedDay = 9,
            savedBalance = 7777,
            savedClubLevel = 4,
            savedReputation = 73,
            currentDay = 1,
            clubLevel = 1
        };
        File.WriteAllText(SavePath, JsonUtility.ToJson(version19, true));
        menu.RefreshSaveState();
        Require(menu.ContinueAvailable && menu.SaveSummary.day == 9 &&
                menu.SaveSummary.balance == 7777,
            "Version 19 metadata card is incorrect.");

        Invoke(menu, "RequestNewGame");
        Require(GameObject.Find("NewGameConfirmationOverlay") != null &&
                GameObject.Find("NewGameConfirmationOverlay").activeInHierarchy,
            "New game confirmation was not opened.");
        Invoke(menu, "CancelNewGame");
        Require(SaveManager.HasSaveFile(),
            "Canceling new game deleted progress.");

        SchedulePhase(1, 2d);
        Invoke(menu, "StartNewGame");
    }

    private static void VerifyNewGameAndReturnToMenu()
    {
        Require(SceneManager.GetActiveScene().name == "SampleScene",
            "New game did not load SampleScene.");
        Require(Time.timeScale == 1f,
            "New game did not restore Time.timeScale.");
        Require(FirstDayTutorialManager.Instance != null &&
                FirstDayTutorialManager.Instance.IsTutorialActive,
            "New game did not start first-day tutorial.");
        Require(ClubHUDCanvas.Instance.CurrentMode == ClubHUDMode.Expanded,
            "New game did not use the default HUD mode from settings.");
        RequireSingleManagers();
        Require(!SaveManager.HasSaveFile(),
            "New game unexpectedly retained the old save.");
        ClubHUDCanvas.Instance.SetMode(ClubHUDMode.Hidden);
        Require(SaveManager.Instance.TrySaveGame(),
            "Version 19 save could not be created.");

        GameSaveSummary summary = SaveManager.TryReadSaveSummary();
        Require(summary.isValid && summary.day == 1 &&
                summary.balance == 1200 && summary.clubLevel == 1 &&
                summary.reputation == 50,
            "New-game version 19 metadata is incorrect.");

        PauseMenuController pause = PauseMenuController.Instance;
        Require(pause != null, "Pause menu was not created.");
        Invoke(pause, "SetMenuOpen", true);
        Require(Time.timeScale == 0f, "Pause menu did not pause the game.");
        Invoke(pause, "OpenSettings");
        GameSettingsPanel panel = GameSettingsPanel.Instance;
        Require(panel.IsOpen && Time.timeScale == 0f,
            "Settings did not open from pause without resuming the game.");
        panel.ApplyPendingDisplaySettings();
        Require(panel.IsDisplayConfirmationOpen,
            "Display confirmation did not open.");
        SetField(panel, "confirmationRemaining", 0f);
        Invoke(panel, "Update");
        Require(!panel.IsDisplayConfirmationOpen,
            "Display confirmation did not expire while paused.");
        panel.Close();
        Require(pause.BlocksGameplayInput && Time.timeScale == 0f,
            "Closing settings did not return to the paused menu.");
        SchedulePhase(2, 2d);
        Invoke(pause, "SaveAndReturnToMainMenu");
    }

    private static void VerifySavedMenuAndContinue()
    {
        Require(SceneManager.GetActiveScene().name == "MainMenu",
            "Return from pause did not load MainMenu.");
        Require(Time.timeScale == 1f,
            "Return to MainMenu did not restore Time.timeScale.");
        MainMenuController menu = RequireMenuOnlyScene();
        Require(menu.ContinueAvailable && menu.SaveSummary.isValid,
            "Saved game card was not refreshed after returning to menu.");
        VerifyStoredSettings();

        SchedulePhase(3, 2d);
        Invoke(menu, "ContinueGame");
    }

    private static void VerifyVersion19AndPrepareVersion18()
    {
        Require(SceneManager.GetActiveScene().name == "SampleScene",
            "Continue did not load SampleScene.");
        RequireSingleManagers();
        Require(EconomyManager.Instance.Money == 1200 &&
                GameDayManager.Instance.CurrentDay == 1 &&
                ClubReputationManager.Instance.Reputation == 50,
            "Version 19 save was not restored.");
        Require(ClubHUDCanvas.Instance.CurrentMode == ClubHUDMode.Hidden,
            "The saved HUD mode was replaced by the settings default.");

        GameSaveData data = JsonUtility.FromJson<GameSaveData>(
            File.ReadAllText(SavePath)
        );
        data.version = 18;
        data.savedAtUtc = string.Empty;
        data.savedDay = 0;
        data.savedBalance = 0;
        data.savedClubLevel = 0;
        data.savedReputation = 0;
        File.WriteAllText(SavePath, JsonUtility.ToJson(data, true));

        SchedulePhase(4, 2d);
        Invoke(PauseMenuController.Instance, "ReturnWithoutSaving");
    }

    private static void VerifyVersion18CardAndContinue()
    {
        MainMenuController menu = RequireMenuOnlyScene();
        Require(menu.ContinueAvailable && menu.SaveSummary.day == 1 &&
                menu.SaveSummary.balance == 1200,
            "Version 18 fallback card failed after scene transition.");
        SchedulePhase(5, 2d);
        Invoke(menu, "ContinueGame");
    }

    private static void VerifyVersion18LoadAndReturn()
    {
        Require(SceneManager.GetActiveScene().name == "SampleScene",
            "Version 18 continue did not load SampleScene.");
        RequireSingleManagers();
        Require(EconomyManager.Instance.Money == 1200 &&
                GameDayManager.Instance.CurrentDay == 1,
            "Version 18 save was not restored.");
        SchedulePhase(6, 2d);
        Invoke(PauseMenuController.Instance, "ReturnWithoutSaving");
    }

    private static void VerifyFinalMenuAndExit()
    {
        MainMenuController menu = RequireMenuOnlyScene();
        Require(Time.timeScale == 1f,
            "Final return to menu left the game paused.");
        VerifyStoredSettings();
        EditorPrefs.SetInt(PhaseKey, 7);
        Invoke(menu, "ExitGame");
    }

    private static MainMenuController RequireMenuOnlyScene()
    {
        MainMenuController menu = MainMenuController.Instance;
        Require(menu != null, "MainMenuController is missing.");
        Require(UnityEngine.Object.FindObjectsByType<MainMenuController>().Length == 1,
            "MainMenuController was duplicated.");
        int saveManagers = UnityEngine.Object.FindObjectsByType<SaveManager>().Length;
        int dayManagers = UnityEngine.Object.FindObjectsByType<GameDayManager>().Length;
        int economyManagers = UnityEngine.Object.FindObjectsByType<EconomyManager>().Length;
        int spawners = UnityEngine.Object.FindObjectsByType<ClientSpawner>().Length;
        PC[] foundPCs = UnityEngine.Object.FindObjectsByType<PC>();
        int pcs = foundPCs.Length;
        int huds = UnityEngine.Object.FindObjectsByType<ClubHUDCanvas>().Length;
        string pcLocations = string.Join(
            ", ",
            Array.ConvertAll(
                foundPCs,
                pc => $"{pc.name}@{pc.gameObject.scene.name}"
            )
        );
        Require(saveManagers == 0 && dayManagers == 0 && economyManagers == 0 &&
                spawners == 0 && pcs == 0 && huds == 0,
            "Gameplay runtime objects leaked into MainMenu: " +
            $"save={saveManagers}, day={dayManagers}, economy={economyManagers}, " +
            $"spawner={spawners}, pcs={pcs} [{pcLocations}], hud={huds}.");
        Require(PauseMenuController.Instance == null &&
                PCMaintenancePanel.Instance == null &&
                PricingPanel.Instance == null &&
                MarketingPanel.Instance == null,
            "Gameplay panel static state leaked into MainMenu.");
        return menu;
    }

    private static void RequireSingleManagers()
    {
        Require(UnityEngine.Object.FindObjectsByType<SaveManager>().Length == 1 &&
                UnityEngine.Object.FindObjectsByType<GameDayManager>().Length == 1 &&
                UnityEngine.Object.FindObjectsByType<EconomyManager>().Length == 1 &&
                UnityEngine.Object.FindObjectsByType<FirstDayTutorialManager>().Length == 1 &&
                UnityEngine.Object.FindObjectsByType<PauseMenuController>().Length == 1,
            "Gameplay managers were duplicated or missing.");
    }

    private static void VerifyStoredSettings()
    {
        GameSettingsManager manager = GameSettingsManager.Instance;
        Require(manager != null, "GameSettingsManager is missing.");
        Require(Mathf.Abs(GameUserSettings.MasterVolume - 0.42f) < 0.001f &&
                !GameUserSettings.Fullscreen &&
                GameUserSettings.ResolutionWidth == 1280 &&
                GameUserSettings.ResolutionHeight == 720 &&
                Mathf.Abs(GameUserSettings.UIScale - 1.2f) < 0.001f &&
                Mathf.Abs(manager.Settings.musicVolume - 0.67f) < 0.001f &&
                Mathf.Abs(manager.Settings.effectsVolume - 0.73f) < 0.001f &&
                manager.Settings.version == 1,
            "Settings JSON did not survive save deletion or scene load.");
        Require(Mathf.Abs(AudioListener.volume - 0.42f) < 0.001f,
            "Master audio volume was not applied.");
        GameSettingsResources resources =
            Resources.Load<GameSettingsResources>("GameSettingsResources");
        Require(resources != null && resources.MainAudioMixer != null,
            "Main audio mixer resources are missing.");
        Require(resources.MainAudioMixer.GetFloat("MusicVolume", out float musicDb) &&
                resources.MainAudioMixer.GetFloat("EffectsVolume", out float effectsDb) &&
                !Mathf.Approximately(musicDb, effectsDb),
            "Music and Effects were not applied independently.");
        manager.SetMasterVolume(0f);
        Require(Mathf.Approximately(AudioListener.volume, 0f) &&
                resources.MainAudioMixer.GetFloat("MasterVolume", out float masterDb) &&
                Mathf.Approximately(masterDb, -80f),
            "Master volume at zero did not mute the audio channel.");
        manager.SetMasterVolume(0.42f);

        GameObject root = new GameObject("SettingsScaleSmoke");
        ScalableUIRoot scalable = root.AddComponent<ScalableUIRoot>();
        scalable.ApplyCurrentScale();
        Require(Mathf.Abs(root.transform.localScale.x - 1.2f) < 0.001f,
            "The interface scale did not reach scalable UI roots.");
        UnityEngine.Object.Destroy(root);

        string beforePreview = File.ReadAllText(SettingsPath);
        manager.PreviewDisplayMode(
            manager.Settings.resolutionWidth,
            manager.Settings.resolutionHeight,
            new RefreshRate
            {
                numerator = (uint)manager.Settings.refreshRateNumerator,
                denominator = (uint)manager.Settings.refreshRateDenominator
            },
            manager.Settings.fullscreen,
            !manager.Settings.verticalSync
        );
        Require(File.ReadAllText(SettingsPath) == beforePreview,
            "Display preview was written before confirmation.");
        manager.RestoreDisplayPreview();

        GameSettingsPanel panel = GameSettingsPanel.Instance;
        Require(panel != null, "Shared settings panel is missing.");
        panel.Open();
        Require(panel.IsOpen && GameplayInputState.IsBlocked,
            "Open settings panel did not block gameplay input.");
        panel.ShowControlsScreen();
        Require(panel.IsControlsOpen, "Controls screen did not open.");
        panel.HandleBack();
        Require(!panel.IsControlsOpen, "Escape hierarchy did not leave controls.");
        panel.Close();
    }

    private static void ConfigureSmokeSettings()
    {
        GameSettingsManager manager = GameSettingsManager.Instance;
        Require(manager != null, "GameSettingsManager is missing.");
        GameSettingsData data = manager.Settings;
        manager.SetMasterVolume(0.42f);
        manager.SetMusicVolume(0.67f);
        manager.SetEffectsVolume(0.73f);
        manager.SetInterfaceScale(1.2f);
        manager.SetDefaultHUDMode(ClubHUDMode.Expanded);
        manager.ConfirmDisplaySettings(
            1280,
            720,
            new RefreshRate
            {
                numerator = (uint)Mathf.Max(1, data.refreshRateNumerator),
                denominator = (uint)Mathf.Max(1, data.refreshRateDenominator)
            },
            false,
            true
        );
    }

    private static void VerifySettingsRecovery()
    {
        File.WriteAllText(SettingsPath, "{ invalid settings json");
        GameSettingsManager.Instance.LoadSettings();
        GameSettingsManager.Instance.ApplyAllSettings();
        Require(File.Exists(CorruptedSettingsPath),
            "Corrupted settings file was not preserved.");
        Require(GameSettingsManager.Instance.Settings.version == 1,
            "Corrupted settings did not fall back to defaults.");
    }

    private static void SchedulePhase(int phase, double delay)
    {
        EditorPrefs.SetInt(PhaseKey, phase);
        nextCheckAt = EditorApplication.timeSinceStartup + delay;
    }

    private static void Invoke(object target, string methodName, params object[] args)
    {
        Require(target != null, $"Target for {methodName} is missing.");
        MethodInfo method = target.GetType().GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public
        );
        Require(method != null, $"Method {methodName} was not found.");
        method.Invoke(target, args);
    }

    private static void SetField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic
        );
        Require(field != null, $"Field {fieldName} was not found.");
        field.SetValue(target, value);
    }

    private static void BackupEnvironment()
    {
        bool hadSave = File.Exists(SavePath);
        EditorPrefs.SetBool(HadSaveKey, hadSave);
        if (hadSave)
        {
            File.Copy(SavePath, SaveBackupPath, true);
        }

        bool hadSettings = File.Exists(SettingsPath);
        EditorPrefs.SetBool(HadSettingsKey, hadSettings);
        if (hadSettings)
        {
            File.Copy(SettingsPath, SettingsBackupPath, true);
        }

        bool hadCorruptedSettings = File.Exists(CorruptedSettingsPath);
        EditorPrefs.SetBool(HadCorruptedSettingsKey, hadCorruptedSettings);
        if (hadCorruptedSettings)
        {
            File.Copy(
                CorruptedSettingsPath,
                CorruptedSettingsBackupPath,
                true
            );
        }

        PreferenceBackup backup = new PreferenceBackup
        {
            hasVolume = PlayerPrefs.HasKey(GameUserSettings.MasterVolumeKey),
            volume = PlayerPrefs.GetFloat(GameUserSettings.MasterVolumeKey),
            hasFullscreen = PlayerPrefs.HasKey(GameUserSettings.FullscreenKey),
            fullscreen = PlayerPrefs.GetInt(GameUserSettings.FullscreenKey),
            hasWidth = PlayerPrefs.HasKey(GameUserSettings.ResolutionWidthKey),
            width = PlayerPrefs.GetInt(GameUserSettings.ResolutionWidthKey),
            hasHeight = PlayerPrefs.HasKey(GameUserSettings.ResolutionHeightKey),
            height = PlayerPrefs.GetInt(GameUserSettings.ResolutionHeightKey),
            hasScale = PlayerPrefs.HasKey(GameUserSettings.UIScaleKey),
            scale = PlayerPrefs.GetFloat(GameUserSettings.UIScaleKey),
            hasEffects = PlayerPrefs.HasKey(GameUserSettings.ScreenEffectsKey),
            effects = PlayerPrefs.GetInt(GameUserSettings.ScreenEffectsKey)
        };
        EditorPrefs.SetString(BackupPrefsKey, JsonUtility.ToJson(backup));
    }

    private static void RestoreEnvironment()
    {
        if (EditorPrefs.GetBool(HadSaveKey, false) && File.Exists(SaveBackupPath))
        {
            File.Copy(SaveBackupPath, SavePath, true);
        }
        else if (File.Exists(SavePath))
        {
            File.Delete(SavePath);
        }

        if (File.Exists(SaveBackupPath))
        {
            File.Delete(SaveBackupPath);
        }

        RestoreFile(
            SettingsPath,
            SettingsBackupPath,
            EditorPrefs.GetBool(HadSettingsKey, false)
        );
        RestoreFile(
            CorruptedSettingsPath,
            CorruptedSettingsBackupPath,
            EditorPrefs.GetBool(HadCorruptedSettingsKey, false)
        );

        PreferenceBackup backup = JsonUtility.FromJson<PreferenceBackup>(
            EditorPrefs.GetString(BackupPrefsKey, "{}")
        );
        RestoreFloat(GameUserSettings.MasterVolumeKey, backup.hasVolume, backup.volume);
        RestoreInt(GameUserSettings.FullscreenKey, backup.hasFullscreen, backup.fullscreen);
        RestoreInt(GameUserSettings.ResolutionWidthKey, backup.hasWidth, backup.width);
        RestoreInt(GameUserSettings.ResolutionHeightKey, backup.hasHeight, backup.height);
        RestoreFloat(GameUserSettings.UIScaleKey, backup.hasScale, backup.scale);
        RestoreInt(GameUserSettings.ScreenEffectsKey, backup.hasEffects, backup.effects);
        PlayerPrefs.Save();
        Time.timeScale = 1f;
        EditorPrefs.DeleteKey(BackupPrefsKey);
        EditorPrefs.DeleteKey(HadSaveKey);
        EditorPrefs.DeleteKey(HadSettingsKey);
        EditorPrefs.DeleteKey(HadCorruptedSettingsKey);
    }

    private static void RestoreFile(
        string destination,
        string backup,
        bool existed)
    {
        if (existed && File.Exists(backup))
        {
            File.Copy(backup, destination, true);
        }
        else if (File.Exists(destination))
        {
            File.Delete(destination);
        }

        if (File.Exists(backup))
        {
            File.Delete(backup);
        }
    }

    private static void RestoreFloat(string key, bool existed, float value)
    {
        if (existed) PlayerPrefs.SetFloat(key, value);
        else PlayerPrefs.DeleteKey(key);
    }

    private static void RestoreInt(string key, bool existed, int value)
    {
        if (existed) PlayerPrefs.SetInt(key, value);
        else PlayerPrefs.DeleteKey(key);
    }

    private static void Fail(Exception exception)
    {
        RestoreEnvironment();
        EditorPrefs.DeleteKey(PendingKey);
        EditorPrefs.DeleteKey(PhaseKey);
        Debug.LogError($"MAIN_MENU_FLOW_SMOKE_TEST: FAIL\n{exception}");
        EditorApplication.Exit(1);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
