using System;
using UnityEngine;

public sealed class EconomyManager : MonoBehaviour
{
    public static EconomyManager Instance { get; private set; }

    [SerializeField] private int money = 0;

    public int Money => money;
    public int TotalIncome { get; private set; }
    public int TotalExpenses { get; private set; }
    public event Action<int> MoneyChanged;
    public event Action<EconomyTransactionRecord> TransactionRecorded;

    public void RestoreState(
        int savedMoney,
        int savedTotalIncome,
        int savedTotalExpenses)
    {
        money = savedMoney;
        TotalIncome = Mathf.Max(0, savedTotalIncome);
        TotalExpenses = Mathf.Max(0, savedTotalExpenses);
        MoneyChanged?.Invoke(money);
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

    public void AddMoney(
        int amount,
        EconomyTransactionCategory category = EconomyTransactionCategory.OtherIncome)
    {
        if (amount <= 0)
        {
            Debug.LogWarning("Нельзя добавить нулевую или отрицательную сумму.");
            return;
        }

        money += amount;
        TotalIncome += amount;
        Debug.Log($"Получено денег: {amount}. Баланс клуба: {money}");
        MoneyChanged?.Invoke(money);
        TransactionRecorded?.Invoke(new EconomyTransactionRecord(amount, true, true, category));
    }

    public void AddBonusMoney(
        int amount,
        EconomyTransactionCategory category = EconomyTransactionCategory.DailyGoalReward)
    {
        if (amount <= 0)
        {
            Debug.LogWarning("Бонус должен быть больше нуля.");
            return;
        }

        money += amount;
        Debug.Log($"Получен бонус: {amount}. Баланс клуба: {money}");
        MoneyChanged?.Invoke(money);
        TransactionRecorded?.Invoke(new EconomyTransactionRecord(amount, true, false, category));
    }

    public bool SpendMoney(
        int amount,
        EconomyTransactionCategory category = EconomyTransactionCategory.OtherExpense)
    {
        if (amount <= 0)
        {
            Debug.LogWarning("Нельзя потратить нулевую или отрицательную сумму.");
            return false;
        }

        if (money < amount)
        {
            Debug.Log("Недостаточно денег.");
            return false;
        }

        money -= amount;
        TotalExpenses += amount;
        Debug.Log($"Потрачено денег: {amount}. Баланс клуба: {money}");
        MoneyChanged?.Invoke(money);
        TransactionRecorded?.Invoke(new EconomyTransactionRecord(amount, false, false, category));
        return true;
    }

    public void ApplyMandatoryExpense(
        int amount,
        EconomyTransactionCategory category = EconomyTransactionCategory.OtherExpense)
    {
        if (amount <= 0)
        {
            Debug.LogWarning("Обязательный расход должен быть больше нуля.");
            return;
        }

        money -= amount;
        TotalExpenses += amount;

        Debug.Log($"Обязательные расходы: {amount}. Баланс клуба: {money}");
        MoneyChanged?.Invoke(money);
        TransactionRecorded?.Invoke(new EconomyTransactionRecord(amount, false, false, category));
    }
}
