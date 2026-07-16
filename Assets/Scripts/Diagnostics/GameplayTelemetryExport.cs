#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;

[Serializable]
public sealed class GameplayTelemetryExport
{
    public string generatedAtUtc;
    public string applicationVersion;
    public List<GameplayDayTelemetry> days = new();
}
#endif
