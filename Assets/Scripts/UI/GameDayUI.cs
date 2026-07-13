using UnityEngine;

public sealed class GameDayUI : MonoBehaviour
{
    [SerializeField] private int fontSize = 24;
    [SerializeField] private Vector2 timerPosition = new Vector2(20f, 125f);
    [SerializeField] private Vector2 reportPosition = new Vector2(20f, 160f);

    private GUIStyle labelStyle;
    private string lastDayReport = "Итоги прошлого дня: пока нет";

    private void Start()
    {
        if (GameDayManager.Instance == null)
        {
            Debug.LogWarning("GameDayManager is missing. Game day UI is disabled.");
            return;
        }

        GameDayManager.Instance.DayEnded += OnDayEnded;
    }

    private void OnDestroy()
    {
        if (GameDayManager.Instance != null)
        {
            GameDayManager.Instance.DayEnded -= OnDayEnded;
        }
    }

    private void OnDayEnded(int completedDay, int income, int expenses, int profit)
    {
        string resultPrefix = profit >= 0 ? "+" : string.Empty;

        lastDayReport =
            $"День {completedDay}: доход {income} ₽ | " +
            $"расходы {expenses} ₽ | итог {resultPrefix}{profit} ₽";
    }

    private void OnGUI()
    {
        if (labelStyle == null)
        {
            labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = fontSize
            };
        }

        DrawTimer();
        DrawLastDayReport();
    }

    private void DrawTimer()
    {
        if (GameDayManager.Instance == null)
        {
            return;
        }

        int secondsRemaining = Mathf.CeilToInt(GameDayManager.Instance.TimeRemaining);
        int minutes = secondsRemaining / 60;
        int seconds = secondsRemaining % 60;

        string timerText =
            $"День: {GameDayManager.Instance.CurrentDay} | " +
            $"До конца дня: {minutes:00}:{seconds:00}";

        GUI.Label(
            new Rect(timerPosition.x, timerPosition.y, 600f, 40f),
            timerText,
            labelStyle
        );
    }

    private void DrawLastDayReport()
    {
        GUI.Label(
            new Rect(reportPosition.x, reportPosition.y, 900f, 40f),
            lastDayReport,
            labelStyle
        );
    }
}
