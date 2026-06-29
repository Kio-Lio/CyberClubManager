using System;
using UnityEngine;

public sealed class EconomyManager : MonoBehaviour
{
    public static EconomyManager Instance { get; private set; }

    [SerializeField] private int money = 0;

    public int Money => money;
    public event Action<int> MoneyChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    public void AddMoney(int amount)
    {
        if (amount <= 0)
        {
            Debug.LogWarning("Нельзя добавить нулевую или отрицательную сумму.");
            return;
        }

        money += amount;
        Debug.Log($"Получено денег: {amount}. Баланс клуба: {money}");
        MoneyChanged?.Invoke(money);
    }

    public bool SpendMoney(int amount)
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
        Debug.Log($"Потрачено денег: {amount}. Баланс клуба: {money}");
        MoneyChanged?.Invoke(money);
        return true;
    }
}
