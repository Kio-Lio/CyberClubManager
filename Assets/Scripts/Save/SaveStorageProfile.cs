using System.IO;
using UnityEngine;

public static class SaveStorageProfile
{
    private const string PrimarySaveFileName = "cyber_club_save.json";
    private const string QASaveFileName = "cyber_club_qa_save.json";

    private static bool useQASandbox;

    public static bool IsQASandboxActive => useQASandbox;

    public static string PrimarySavePath => Path.Combine(
        Application.persistentDataPath,
        PrimarySaveFileName
    );

    public static string QASavePath => Path.Combine(
        Application.persistentDataPath,
        "Diagnostics",
        "QA",
        QASaveFileName
    );

    public static string ActiveSavePath => useQASandbox
        ? QASavePath
        : PrimarySavePath;

    public static void UseQASandbox()
    {
        useQASandbox = true;
    }

    public static void UsePrimarySave()
    {
        useQASandbox = false;
    }
}
