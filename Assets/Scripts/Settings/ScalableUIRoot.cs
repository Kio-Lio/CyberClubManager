using UnityEngine;

public sealed class ScalableUIRoot : MonoBehaviour
{
    private void OnEnable()
    {
        ApplyCurrentScale();
    }

    public void ApplyCurrentScale()
    {
        float scale = GameSettingsManager.Instance != null
            ? GameSettingsManager.Instance.Settings.interfaceScale
            : 1f;
        transform.localScale = Vector3.one * scale;
    }
}
