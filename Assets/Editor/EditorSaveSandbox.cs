using System.IO;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class EditorSaveSandbox
{
    static EditorSaveSandbox()
    {
        SaveStorageProfile.UseQASandbox();
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    [MenuItem("Tools/Cyber Club/QA/Clear Sandbox Save")]
    private static void ClearSandboxSave()
    {
        string path = SaveStorageProfile.QASavePath;
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        Debug.Log($"[QA] Sandbox save cleared: {path}");
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingEditMode ||
            state == PlayModeStateChange.EnteredPlayMode ||
            state == PlayModeStateChange.EnteredEditMode)
        {
            SaveStorageProfile.UseQASandbox();
        }
    }
}
