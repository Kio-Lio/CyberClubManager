using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.InputSystem;

public sealed class GameSettingsManager : MonoBehaviour
{
    public static GameSettingsManager Instance { get; private set; }

    private const int CurrentSettingsVersion = 1;
    private const string SettingsFileName = "settings.json";
    private const string CorruptedSettingsFileName = "settings.corrupted.json";

    private GameSettingsData settings;
    private AudioMixer audioMixer;

    public GameSettingsData Settings => settings;
    public string SettingsPath => Path.Combine(Application.persistentDataPath, SettingsFileName);
    public string CorruptedSettingsPath => Path.Combine(
        Application.persistentDataPath,
        CorruptedSettingsFileName
    );

    public event Action SettingsChanged;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        Instance = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance == null)
        {
            new GameObject("GameSettingsManager")
                .AddComponent<GameSettingsManager>();
        }
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        GameSettingsResources resources =
            Resources.Load<GameSettingsResources>("GameSettingsResources");
        audioMixer = resources != null ? resources.MainAudioMixer : null;

        LoadSettings();
        ApplyAllSettings();

        if (GetComponent<GameSettingsPanel>() == null)
        {
            gameObject.AddComponent<GameSettingsPanel>();
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public void LoadSettings()
    {
        if (!File.Exists(SettingsPath))
        {
            settings = CreateDefaultSettings();
            MigrateLegacyPlayerPrefs();
            SaveSettings();
            return;
        }

        try
        {
            string json = File.ReadAllText(SettingsPath);
            GameSettingsData loaded = JsonUtility.FromJson<GameSettingsData>(json);
            if (loaded == null || loaded.version <= 0 ||
                loaded.version > CurrentSettingsVersion)
            {
                throw new InvalidDataException("Unsupported settings version.");
            }

            settings = loaded;
            NormalizeSettings();
        }
        catch (Exception exception)
        {
            Debug.LogWarning(
                $"Файл настроек повреждён и будет восстановлен: {exception.Message}"
            );
            BackupCorruptedSettings();
            settings = CreateDefaultSettings();
            SaveSettings();
        }
    }

    public void SaveSettings()
    {
        settings ??= CreateDefaultSettings();
        NormalizeSettings();

        try
        {
            File.WriteAllText(
                SettingsPath,
                JsonUtility.ToJson(settings, true)
            );
        }
        catch (Exception exception)
        {
            Debug.LogError($"Не удалось сохранить настройки: {exception.Message}");
        }
    }

    public void ApplyAllSettings()
    {
        settings ??= CreateDefaultSettings();
        NormalizeSettings();
        ApplyDisplayMode(
            settings.resolutionWidth,
            settings.resolutionHeight,
            CreateRefreshRate(settings),
            settings.fullscreen
        );
        ApplyVerticalSync();
        ApplyAudioSettings();
        ApplyInterfaceScale();
        ApplyInputOverrides();
    }

    public void SetMasterVolume(float value)
    {
        settings.masterVolume = Mathf.Clamp01(value);
        ApplyAudioSettings();
        CommitChange();
    }

    public void SetMusicVolume(float value)
    {
        settings.musicVolume = Mathf.Clamp01(value);
        ApplyAudioSettings();
        CommitChange();
    }

    public void SetEffectsVolume(float value)
    {
        settings.effectsVolume = Mathf.Clamp01(value);
        ApplyAudioSettings();
        CommitChange();
    }

    public void SetInterfaceScale(float value)
    {
        settings.interfaceScale = NormalizeInterfaceScale(value);
        ApplyInterfaceScale();
        CommitChange();
    }

    public void SetDefaultHUDMode(ClubHUDMode mode)
    {
        settings.defaultHUDMode = Enum.IsDefined(typeof(ClubHUDMode), mode)
            ? mode
            : ClubHUDMode.Compact;
        CommitChange();
    }

    public void SetDisplayMode(
        int width,
        int height,
        RefreshRate refreshRate,
        bool fullscreen)
    {
        NormalizeDisplayValues(ref width, ref height, ref refreshRate, fullscreen);
        StoreDisplayMode(width, height, refreshRate, fullscreen);
        ApplyDisplayMode(width, height, refreshRate, fullscreen);
        CommitChange();
    }

    public void PreviewDisplayMode(
        int width,
        int height,
        RefreshRate refreshRate,
        bool fullscreen,
        bool verticalSync)
    {
        NormalizeDisplayValues(ref width, ref height, ref refreshRate, fullscreen);
        ApplyDisplayMode(width, height, refreshRate, fullscreen);
        QualitySettings.vSyncCount = verticalSync ? 1 : 0;
        Application.targetFrameRate = -1;
    }

    public void RestoreDisplayPreview()
    {
        ApplyDisplayMode(
            settings.resolutionWidth,
            settings.resolutionHeight,
            CreateRefreshRate(settings),
            settings.fullscreen
        );
        ApplyVerticalSync();
    }

    public void ConfirmDisplaySettings(
        int width,
        int height,
        RefreshRate refreshRate,
        bool fullscreen,
        bool verticalSync)
    {
        NormalizeDisplayValues(ref width, ref height, ref refreshRate, fullscreen);
        StoreDisplayMode(width, height, refreshRate, fullscreen);
        settings.verticalSync = verticalSync;
        ApplyDisplayMode(width, height, refreshRate, fullscreen);
        ApplyVerticalSync();
        CommitChange();
    }

    public void SetVerticalSync(bool enabled)
    {
        settings.verticalSync = enabled;
        ApplyVerticalSync();
        CommitChange();
    }

    public void RestoreDefaults()
    {
        settings = CreateDefaultSettings();
        ApplyAllSettings();
        CommitChange();
    }

    public void ResetInputBindings()
    {
        InputSystem.actions?.RemoveAllBindingOverrides();
        settings.inputBindingOverridesJson = string.Empty;
        CommitChange();
    }

    public void SaveInputBindingOverrides()
    {
        InputActionAsset actions = InputSystem.actions;
        settings.inputBindingOverridesJson = actions != null
            ? actions.SaveBindingOverridesAsJson()
            : string.Empty;
        CommitChange();
    }

    public List<Vector2Int> GetSupportedResolutions()
    {
        List<Vector2Int> result = new();
        HashSet<Vector2Int> seen = new();

        foreach (Resolution resolution in Screen.resolutions)
        {
            Vector2Int size = new(resolution.width, resolution.height);
            if (size.x >= 1280 && size.y >= 720 && seen.Add(size))
            {
                result.Add(size);
            }
        }

        Vector2Int selected = new(settings.resolutionWidth, settings.resolutionHeight);
        if (seen.Add(selected))
        {
            result.Add(selected);
        }

        if (result.Count == 0)
        {
            result.Add(new Vector2Int(1920, 1080));
        }

        result.Sort((left, right) =>
        {
            int widthComparison = left.x.CompareTo(right.x);
            return widthComparison != 0
                ? widthComparison
                : left.y.CompareTo(right.y);
        });
        return result;
    }

    public void ApplyInterfaceScale()
    {
        foreach (ScalableUIRoot root in
                 FindObjectsByType<ScalableUIRoot>(FindObjectsInactive.Include))
        {
            root?.ApplyCurrentScale();
        }
    }

    private void ApplyDisplayMode(
        int width,
        int height,
        RefreshRate refreshRate,
        bool fullscreen)
    {
        Screen.SetResolution(
            width,
            height,
            fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed,
            refreshRate
        );
    }

    private void ApplyVerticalSync()
    {
        QualitySettings.vSyncCount = settings.verticalSync ? 1 : 0;
        Application.targetFrameRate = -1;
    }

    private void ApplyAudioSettings()
    {
        AudioListener.volume = settings.masterVolume;
        if (audioMixer == null)
        {
            return;
        }

        audioMixer.SetFloat("MasterVolume", LinearToDecibels(settings.masterVolume));
        audioMixer.SetFloat("MusicVolume", LinearToDecibels(settings.musicVolume));
        audioMixer.SetFloat("EffectsVolume", LinearToDecibels(settings.effectsVolume));
    }

    private void ApplyInputOverrides()
    {
        if (string.IsNullOrWhiteSpace(settings.inputBindingOverridesJson))
        {
            return;
        }

        try
        {
            InputSystem.actions?.LoadBindingOverridesFromJson(
                settings.inputBindingOverridesJson
            );
        }
        catch (Exception exception)
        {
            Debug.LogWarning(
                $"Не удалось применить назначения управления: {exception.Message}"
            );
            settings.inputBindingOverridesJson = string.Empty;
        }
    }

    private void CommitChange()
    {
        SaveSettings();
        SettingsChanged?.Invoke();
    }

    private void StoreDisplayMode(
        int width,
        int height,
        RefreshRate refreshRate,
        bool fullscreen)
    {
        settings.resolutionWidth = width;
        settings.resolutionHeight = height;
        settings.refreshRateNumerator = (int)refreshRate.numerator;
        settings.refreshRateDenominator = (int)refreshRate.denominator;
        settings.fullscreen = fullscreen;
    }

    private void NormalizeSettings()
    {
        settings.version = CurrentSettingsVersion;
        settings.masterVolume = Mathf.Clamp01(settings.masterVolume);
        settings.musicVolume = Mathf.Clamp01(settings.musicVolume);
        settings.effectsVolume = Mathf.Clamp01(settings.effectsVolume);
        settings.interfaceScale = NormalizeInterfaceScale(settings.interfaceScale);
        settings.refreshRateNumerator = Mathf.Max(1, settings.refreshRateNumerator);
        settings.refreshRateDenominator = Mathf.Max(1, settings.refreshRateDenominator);
        settings.resolutionWidth = Mathf.Max(
            settings.fullscreen ? 640 : 1280,
            settings.resolutionWidth
        );
        settings.resolutionHeight = Mathf.Max(
            settings.fullscreen ? 360 : 720,
            settings.resolutionHeight
        );
        if (!Enum.IsDefined(typeof(ClubHUDMode), settings.defaultHUDMode))
        {
            settings.defaultHUDMode = ClubHUDMode.Compact;
        }
        settings.inputBindingOverridesJson ??= string.Empty;
    }

    private static void NormalizeDisplayValues(
        ref int width,
        ref int height,
        ref RefreshRate refreshRate,
        bool fullscreen)
    {
        width = Mathf.Max(fullscreen ? 640 : 1280, width);
        height = Mathf.Max(fullscreen ? 360 : 720, height);
        refreshRate.numerator = Math.Max(1u, refreshRate.numerator);
        refreshRate.denominator = Math.Max(1u, refreshRate.denominator);
    }

    private static RefreshRate CreateRefreshRate(GameSettingsData data)
    {
        return new RefreshRate
        {
            numerator = (uint)Mathf.Max(1, data.refreshRateNumerator),
            denominator = (uint)Mathf.Max(1, data.refreshRateDenominator)
        };
    }

    private GameSettingsData CreateDefaultSettings()
    {
        Resolution resolution = Screen.currentResolution;
        return new GameSettingsData
        {
            version = CurrentSettingsVersion,
            resolutionWidth = Mathf.Max(1280, resolution.width),
            resolutionHeight = Mathf.Max(720, resolution.height),
            refreshRateNumerator = (int)Math.Max(
                1u,
                resolution.refreshRateRatio.numerator
            ),
            refreshRateDenominator = (int)Math.Max(
                1u,
                resolution.refreshRateRatio.denominator
            ),
            fullscreen = true,
            verticalSync = true,
            masterVolume = 1f,
            musicVolume = 0.8f,
            effectsVolume = 0.9f,
            interfaceScale = 1f,
            defaultHUDMode = ClubHUDMode.Compact,
            inputBindingOverridesJson = string.Empty
        };
    }

    private void MigrateLegacyPlayerPrefs()
    {
        bool hasLegacySettings =
            PlayerPrefs.HasKey(GameUserSettings.MasterVolumeKey) ||
            PlayerPrefs.HasKey(GameUserSettings.FullscreenKey) ||
            PlayerPrefs.HasKey(GameUserSettings.ResolutionWidthKey) ||
            PlayerPrefs.HasKey(GameUserSettings.ResolutionHeightKey) ||
            PlayerPrefs.HasKey(GameUserSettings.UIScaleKey);
        if (!hasLegacySettings)
        {
            return;
        }

        settings.masterVolume = PlayerPrefs.GetFloat(
            GameUserSettings.MasterVolumeKey,
            settings.masterVolume
        );
        settings.fullscreen = PlayerPrefs.GetInt(
            GameUserSettings.FullscreenKey,
            settings.fullscreen ? 1 : 0
        ) != 0;
        settings.resolutionWidth = PlayerPrefs.GetInt(
            GameUserSettings.ResolutionWidthKey,
            settings.resolutionWidth
        );
        settings.resolutionHeight = PlayerPrefs.GetInt(
            GameUserSettings.ResolutionHeightKey,
            settings.resolutionHeight
        );
        settings.interfaceScale = PlayerPrefs.GetFloat(
            GameUserSettings.UIScaleKey,
            settings.interfaceScale
        );
        NormalizeSettings();
        Debug.Log("Legacy PlayerPrefs settings migrated to settings.json.");
    }

    private void BackupCorruptedSettings()
    {
        try
        {
            File.Copy(SettingsPath, CorruptedSettingsPath, true);
        }
        catch (Exception exception)
        {
            Debug.LogWarning(
                $"Не удалось сохранить копию повреждённых настроек: {exception.Message}"
            );
        }
    }

    private static float NormalizeInterfaceScale(float value)
    {
        return Mathf.Round(Mathf.Clamp(value, 0.8f, 1.2f) * 10f) / 10f;
    }

    private static float LinearToDecibels(float linearValue)
    {
        return linearValue <= 0.0001f
            ? -80f
            : Mathf.Log10(linearValue) * 20f;
    }
}
