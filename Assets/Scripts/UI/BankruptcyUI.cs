using UnityEngine;

public sealed class BankruptcyUI : MonoBehaviour
{
    [SerializeField] private int fontSize = 24;
    [SerializeField] private Vector2 statusPosition = new Vector2(20f, 195f);

    private GUIStyle labelStyle;

    private void Start()
    {
        if (BankruptcyManager.Instance == null)
        {
            Debug.LogWarning("BankruptcyManager is missing. Financial status UI is disabled.");
            return;
        }

        BankruptcyManager.Instance.StatusChanged += OnStatusChanged;
    }

    private void OnDestroy()
    {
        if (BankruptcyManager.Instance != null)
        {
            BankruptcyManager.Instance.StatusChanged -= OnStatusChanged;
        }
    }

    private void OnStatusChanged()
    {
        // OnGUI reads the latest manager state on every repaint.
    }

    private void OnGUI()
    {
        BankruptcyManager manager = BankruptcyManager.Instance;
        if (manager == null)
        {
            return;
        }

        InitializeStyles();
        DrawFinancialRisk(manager);
    }

    private void InitializeStyles()
    {
        if (labelStyle == null)
        {
            labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = fontSize
            };
        }
    }

    private void DrawFinancialRisk(BankruptcyManager manager)
    {
        string statusText;

        if (manager.ConsecutiveDebtDays == 0)
        {
            statusText =
                $"Финансовый риск: отсутствует | " +
                $"Порог банкротства: {manager.BankruptcyThreshold} ₽";
        }
        else
        {
            statusText =
                $"Критический долг: {manager.ConsecutiveDebtDays}/" +
                $"{manager.ConsecutiveDebtDaysToLose} дней | " +
                $"Порог: {manager.BankruptcyThreshold} ₽";
        }

        GUI.Label(
            new Rect(statusPosition.x, statusPosition.y, 800f, 40f),
            statusText,
            labelStyle
        );
    }

}
