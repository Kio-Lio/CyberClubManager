using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public static class GameUserSettings
{
    // Legacy keys remain public for save-compatibility tests and migration tools.
    public const string MasterVolumeKey = "MasterVolume";
    public const string FullscreenKey = "Fullscreen";
    public const string ResolutionWidthKey = "ResolutionWidth";
    public const string ResolutionHeightKey = "ResolutionHeight";
    public const string UIScaleKey = "UIScale";
    public const string ScreenEffectsKey = "ScreenEffects";

    private static GameSettingsData Settings =>
        GameSettingsManager.Instance != null
            ? GameSettingsManager.Instance.Settings
            : new GameSettingsData();

    public static float MasterVolume => Settings.masterVolume;
    public static bool Fullscreen => Settings.fullscreen;
    public static int ResolutionWidth => Settings.resolutionWidth;
    public static int ResolutionHeight => Settings.resolutionHeight;
    public static float UIScale => Settings.interfaceScale;
    public static bool ScreenEffectsEnabled => true;

    public static void ApplyDisplayAndAudio()
    {
        GameSettingsManager.Instance?.ApplyAllSettings();
    }

    public static void ApplyCanvasScale(
        CanvasScaler scaler,
        Vector2 baseReferenceResolution)
    {
        if (scaler != null)
        {
            scaler.referenceResolution = baseReferenceResolution;
        }
    }

    public static void Save(
        float masterVolume,
        bool fullscreen,
        int resolutionWidth,
        int resolutionHeight,
        float uiScale,
        bool screenEffectsEnabled)
    {
        GameSettingsManager manager = GameSettingsManager.Instance;
        if (manager == null)
        {
            return;
        }

        GameSettingsData current = manager.Settings;
        manager.SetMasterVolume(masterVolume);
        manager.SetInterfaceScale(uiScale);
        manager.SetDisplayMode(
            resolutionWidth,
            resolutionHeight,
            new RefreshRate
            {
                numerator = (uint)Mathf.Max(1, current.refreshRateNumerator),
                denominator = (uint)Mathf.Max(1, current.refreshRateDenominator)
            },
            fullscreen
        );
    }

    public static List<Vector2Int> GetSupportedResolutions()
    {
        return GameSettingsManager.Instance != null
            ? GameSettingsManager.Instance.GetSupportedResolutions()
            : new List<Vector2Int> { new(1920, 1080) };
    }
}
