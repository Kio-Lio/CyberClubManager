using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public static class GameUserSettings
{
    public const string MasterVolumeKey = "MasterVolume";
    public const string FullscreenKey = "Fullscreen";
    public const string ResolutionWidthKey = "ResolutionWidth";
    public const string ResolutionHeightKey = "ResolutionHeight";
    public const string UIScaleKey = "UIScale";
    public const string ScreenEffectsKey = "ScreenEffects";

    public static float MasterVolume =>
        Mathf.Clamp01(PlayerPrefs.GetFloat(MasterVolumeKey, 1f));

    public static bool Fullscreen =>
        PlayerPrefs.GetInt(FullscreenKey, Screen.fullScreen ? 1 : 0) != 0;

    public static int ResolutionWidth => PlayerPrefs.GetInt(
        ResolutionWidthKey,
        Screen.currentResolution.width
    );

    public static int ResolutionHeight => PlayerPrefs.GetInt(
        ResolutionHeightKey,
        Screen.currentResolution.height
    );

    public static float UIScale => Mathf.Clamp(
        PlayerPrefs.GetFloat(UIScaleKey, 1f),
        0.75f,
        1.5f
    );

    public static bool ScreenEffectsEnabled =>
        PlayerPrefs.GetInt(ScreenEffectsKey, 1) != 0;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ApplyOnStartup()
    {
        ApplyDisplayAndAudio();
    }

    public static void ApplyDisplayAndAudio()
    {
        AudioListener.volume = MasterVolume;
        Screen.SetResolution(
            ResolutionWidth,
            ResolutionHeight,
            Fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed
        );
    }

    public static void ApplyCanvasScale(
        CanvasScaler scaler,
        Vector2 baseReferenceResolution)
    {
        if (scaler == null)
        {
            return;
        }

        scaler.referenceResolution = baseReferenceResolution / UIScale;
    }

    public static void Save(
        float masterVolume,
        bool fullscreen,
        int resolutionWidth,
        int resolutionHeight,
        float uiScale,
        bool screenEffectsEnabled)
    {
        PlayerPrefs.SetFloat(MasterVolumeKey, Mathf.Clamp01(masterVolume));
        PlayerPrefs.SetInt(FullscreenKey, fullscreen ? 1 : 0);
        PlayerPrefs.SetInt(ResolutionWidthKey, Mathf.Max(640, resolutionWidth));
        PlayerPrefs.SetInt(ResolutionHeightKey, Mathf.Max(360, resolutionHeight));
        PlayerPrefs.SetFloat(UIScaleKey, Mathf.Clamp(uiScale, 0.75f, 1.5f));
        PlayerPrefs.SetInt(ScreenEffectsKey, screenEffectsEnabled ? 1 : 0);
        PlayerPrefs.Save();
        ApplyDisplayAndAudio();
    }

    public static List<Vector2Int> GetSupportedResolutions()
    {
        List<Vector2Int> result = new List<Vector2Int>();
        HashSet<string> seen = new HashSet<string>();

        foreach (Resolution resolution in Screen.resolutions)
        {
            string key = $"{resolution.width}x{resolution.height}";
            if (seen.Add(key))
            {
                result.Add(new Vector2Int(resolution.width, resolution.height));
            }
        }

        Vector2Int current = new Vector2Int(
            ResolutionWidth,
            ResolutionHeight
        );
        string currentKey = $"{current.x}x{current.y}";

        if (seen.Add(currentKey))
        {
            result.Add(current);
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
}
