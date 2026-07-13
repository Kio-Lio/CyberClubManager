using System;
using UnityEngine;

public sealed class BankruptcyManager : MonoBehaviour
{
    public static BankruptcyManager Instance { get; private set; }

    [Header("Bankruptcy Settings")]
    [SerializeField] private int bankruptcyThreshold = -500;
    [SerializeField, Min(1)] private int consecutiveDebtDaysToLose = 2;

    private int consecutiveDebtDays;
    private bool isGameOver;
    private int gameOverDay;
    private int finalBalance;

    public int BankruptcyThreshold => bankruptcyThreshold;
    public int ConsecutiveDebtDays => consecutiveDebtDays;
    public int ConsecutiveDebtDaysToLose => consecutiveDebtDaysToLose;
    public bool IsGameOver => isGameOver;
    public int GameOverDay => gameOverDay;
    public int FinalBalance => finalBalance;

    public event Action StatusChanged;
    public event Action GameOverTriggered;

    public void RestoreState(int savedConsecutiveDebtDays)
    {
        consecutiveDebtDays = Mathf.Clamp(
            savedConsecutiveDebtDays,
            0,
            consecutiveDebtDaysToLose - 1
        );

        StatusChanged?.Invoke();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void Start()
    {
        Time.timeScale = 1f;

        if (GameDayManager.Instance == null)
        {
            Debug.LogWarning("GameDayManager is missing. Bankruptcy checks are disabled.");
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

        if (Instance == this)
        {
            Time.timeScale = 1f;
            Instance = null;
        }
    }

    private void OnDayEnded(int completedDay, int income, int expenses, int profit)
    {
        if (isGameOver)
        {
            return;
        }

        if (EconomyManager.Instance == null)
        {
            Debug.LogWarning("EconomyManager is missing. Bankruptcy cannot be checked.");
            return;
        }

        int currentBalance = EconomyManager.Instance.Money;

        if (currentBalance <= bankruptcyThreshold)
        {
            consecutiveDebtDays++;
            Debug.LogWarning(
                $"Financial warning: balance {currentBalance}. " +
                $"Critical days: {consecutiveDebtDays}/{consecutiveDebtDaysToLose}."
            );
        }
        else
        {
            if (consecutiveDebtDays > 0)
            {
                Debug.Log("Balance recovered. Critical debt-day counter reset.");
            }

            consecutiveDebtDays = 0;
        }

        StatusChanged?.Invoke();

        if (consecutiveDebtDays >= consecutiveDebtDaysToLose)
        {
            TriggerGameOver(completedDay, currentBalance);
        }
    }

    private void TriggerGameOver(int completedDay, int currentBalance)
    {
        if (isGameOver)
        {
            return;
        }

        isGameOver = true;
        gameOverDay = completedDay;
        finalBalance = currentBalance;

        Debug.LogError(
            $"Club went bankrupt on day {gameOverDay}. " +
            $"Final balance: {finalBalance}."
        );

        Time.timeScale = 0f;
        StatusChanged?.Invoke();
        GameOverTriggered?.Invoke();
    }

    private void OnValidate()
    {
        bankruptcyThreshold = Mathf.Min(0, bankruptcyThreshold);
        consecutiveDebtDaysToLose = Mathf.Max(1, consecutiveDebtDaysToLose);
    }
}
