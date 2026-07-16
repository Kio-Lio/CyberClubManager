#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;

[Serializable]
public sealed class GameplayTelemetryWarning
{
    public string code;
    public int firstDay;
    public int lastDay;
    public string details;
}
#endif
