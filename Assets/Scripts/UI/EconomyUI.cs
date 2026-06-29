using UnityEngine;

public sealed class EconomyUI : MonoBehaviour
{
    private const int Width = 320;
    private const int Height = 48;

    private int currentMoney;
    private GUIStyle labelStyle;

    private void Start()
    {
        if (EconomyManager.Instance == null)
        {
            Debug.LogWarning("EconomyManager не найден. UI баланса не будет работать.");
            return;
        }

        EconomyManager.Instance.MoneyChanged += UpdateMoneyText;
        UpdateMoneyText(EconomyManager.Instance.Money);
    }

    private void OnDestroy()
    {
        if (EconomyManager.Instance != null)
        {
            EconomyManager.Instance.MoneyChanged -= UpdateMoneyText;
        }
    }

    private void OnGUI()
    {
        labelStyle ??= new GUIStyle(GUI.skin.label)
        {
            fontSize = 32,
            fontStyle = FontStyle.Bold,
            normal = { textColor = Color.white }
        };

        GUI.Label(new Rect(24f, 24f, Width, Height), $"Баланс: {currentMoney} ₽", labelStyle);
    }

    private void UpdateMoneyText(int money)
    {
        currentMoney = money;
    }
}
