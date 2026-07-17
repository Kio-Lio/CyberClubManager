using System;

[Serializable]
public sealed class GameSettingsData
{
    public int version = 1;
    public int resolutionWidth = 1920;
    public int resolutionHeight = 1080;
    public int refreshRateNumerator = 60;
    public int refreshRateDenominator = 1;
    public bool fullscreen = true;
    public bool verticalSync = true;
    public float masterVolume = 1f;
    public float musicVolume = 0.8f;
    public float effectsVolume = 0.9f;
    public float interfaceScale = 1f;
    public ClubHUDMode defaultHUDMode = ClubHUDMode.Compact;
    public string inputBindingOverridesJson = string.Empty;

    public GameSettingsData Clone()
    {
        return (GameSettingsData)MemberwiseClone();
    }
}
