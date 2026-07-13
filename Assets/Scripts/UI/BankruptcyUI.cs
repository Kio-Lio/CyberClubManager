using UnityEngine;

public sealed class BankruptcyUI : MonoBehaviour
{
    [SerializeField] private int fontSize = 24;
    [SerializeField] private Vector2 statusPosition = new Vector2(20f, 195f);

    private GUIStyle labelStyle;
    private GUIStyle gameOverStyle;

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

        if (manager.IsGameOver)
        {
            DrawGameOverWindow(manager);
        }
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

        if (gameOverStyle == null)
        {
            gameOverStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 28,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true
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

    private void DrawGameOverWindow(BankruptcyManager manager)
    {
        const float width = 560f;
        const float height = 220f;

        Rect windowRect = new Rect(
            (Screen.width - width) / 2f,
            (Screen.height - height) / 2f,
            width,
            height
        );

        GUI.Box(windowRect, string.Empty);

        string gameOverText =
            "КЛУБ ОБАНКРОТИЛСЯ\n\n" +
            $"Пройдено дней: {manager.GameOverDay}\n" +
            $"Итоговый баланс: {manager.FinalBalance} ₽\n\n" +
            "Останови Play Mode для новой попытки.";

        GUI.Label(windowRect, gameOverText, gameOverStyle);
    }
}
