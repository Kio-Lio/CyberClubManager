using System;
using UnityEngine;

public enum ConsumableType
{
    EnergyDrink,
    Snack
}

public sealed class ConsumableInventoryManager : MonoBehaviour
{
    public static ConsumableInventoryManager Instance { get; private set; }

    [Header("Stock")]
    [SerializeField, Min(0)] private int initialEnergyDrinkStock = 5;
    [SerializeField, Min(0)] private int initialSnackStock = 5;
    [SerializeField, Min(1)] private int maximumEnergyDrinkStock = 30;
    [SerializeField, Min(1)] private int maximumSnackStock = 30;
    [SerializeField, Min(1)] private int restockPackSize = 5;

    [Header("Prices")]
    [SerializeField, Min(0)] private int energyDrinkPurchasePrice = 60;
    [SerializeField, Min(0)] private int energyDrinkSalePrice = 120;
    [SerializeField, Min(0)] private int snackPurchasePrice = 35;
    [SerializeField, Min(0)] private int snackSalePrice = 80;

    [Header("Testing")]
    [SerializeField] private bool forcePurchaseDecisions;
    [SerializeField] private bool forceEnergyDrinkPurchase;
    [SerializeField] private bool forceSnackPurchase;

    private int energyDrinkStock;
    private int snackStock;
    private int totalItemsSold;
    private int totalConsumableRevenue;
    private int missedSales;
    private string lastStatusMessage = "No additional sales yet.";

    public int EnergyDrinkStock => energyDrinkStock;
    public int SnackStock => snackStock;
    public int MaximumEnergyDrinkStock => maximumEnergyDrinkStock;
    public int MaximumSnackStock => maximumSnackStock;
    public int TotalItemsSold => totalItemsSold;
    public int TotalConsumableRevenue => totalConsumableRevenue;
    public int MissedSales => missedSales;
    public string LastStatusMessage => lastStatusMessage;

    public event Action StatusChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        InitializeDefaultState();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public void InitializeDefaultState()
    {
        energyDrinkStock = Mathf.Clamp(initialEnergyDrinkStock, 0, maximumEnergyDrinkStock);
        snackStock = Mathf.Clamp(initialSnackStock, 0, maximumSnackStock);
        totalItemsSold = 0;
        totalConsumableRevenue = 0;
        missedSales = 0;
        lastStatusMessage = "Stockroom ready.";
        StatusChanged?.Invoke();
    }

    public int GetStock(ConsumableType type)
    {
        return type == ConsumableType.EnergyDrink ? energyDrinkStock : snackStock;
    }

    public int GetMaximumStock(ConsumableType type)
    {
        return type == ConsumableType.EnergyDrink
            ? maximumEnergyDrinkStock
            : maximumSnackStock;
    }

    public int GetSalePrice(ConsumableType type)
    {
        return type == ConsumableType.EnergyDrink
            ? energyDrinkSalePrice
            : snackSalePrice;
    }

    public int GetRestockAmount(ConsumableType type)
    {
        return Mathf.Clamp(restockPackSize, 0, GetMaximumStock(type) - GetStock(type));
    }

    public int GetRestockCost(ConsumableType type)
    {
        int unitPrice = type == ConsumableType.EnergyDrink
            ? energyDrinkPurchasePrice
            : snackPurchasePrice;
        return GetRestockAmount(type) * unitPrice;
    }

    public bool TryRestock(ConsumableType type)
    {
        int amount = GetRestockAmount(type);
        if (amount <= 0)
        {
            lastStatusMessage = "This item is already fully stocked.";
            StatusChanged?.Invoke();
            return false;
        }

        int cost = GetRestockCost(type);
        EconomyManager economy = EconomyManager.Instance;
        if (economy == null || !economy.SpendMoney(
            cost,
            EconomyTransactionCategory.ConsumableRestock
        ))
        {
            lastStatusMessage = $"Restocking requires {cost} RUB.";
            StatusChanged?.Invoke();
            return false;
        }

        if (type == ConsumableType.EnergyDrink)
        {
            energyDrinkStock += amount;
        }
        else
        {
            snackStock += amount;
        }

        lastStatusMessage = $"Restocked {GetDisplayName(type)}: {amount} for {cost} RUB.";
        Debug.Log(lastStatusMessage);
        StatusChanged?.Invoke();
        return true;
    }

    public void TrySellToClient(ClientType clientType)
    {
        bool wantsEnergyDrink = forcePurchaseDecisions
            ? forceEnergyDrinkPurchase
            : UnityEngine.Random.value < GetEnergyDrinkPurchaseChance(clientType);
        bool wantsSnack = forcePurchaseDecisions
            ? forceSnackPurchase
            : UnityEngine.Random.value < GetSnackPurchaseChance(clientType);

        int soldItems = 0;
        int revenue = 0;
        int missed = 0;
        ProcessDemand(ConsumableType.EnergyDrink, wantsEnergyDrink, ref soldItems, ref revenue, ref missed);
        ProcessDemand(ConsumableType.Snack, wantsSnack, ref soldItems, ref revenue, ref missed);

        if (soldItems == 0 && missed == 0)
        {
            return;
        }

        lastStatusMessage = soldItems > 0
            ? $"Sold {soldItems} item(s), revenue {revenue} RUB."
            : $"Missed sales: {missed}.";
        StatusChanged?.Invoke();
    }

    public void RestoreState(
        int savedEnergyDrinkStock,
        int savedSnackStock,
        int savedTotalItemsSold,
        int savedTotalRevenue,
        int savedMissedSales)
    {
        energyDrinkStock = Mathf.Clamp(savedEnergyDrinkStock, 0, maximumEnergyDrinkStock);
        snackStock = Mathf.Clamp(savedSnackStock, 0, maximumSnackStock);
        totalItemsSold = Mathf.Max(0, savedTotalItemsSold);
        totalConsumableRevenue = Mathf.Max(0, savedTotalRevenue);
        missedSales = Mathf.Max(0, savedMissedSales);
        lastStatusMessage = "Stockroom state restored.";
        StatusChanged?.Invoke();
    }

    private void ProcessDemand(
        ConsumableType type,
        bool clientWantsProduct,
        ref int soldItems,
        ref int revenue,
        ref int missed)
    {
        if (!clientWantsProduct)
        {
            return;
        }

        if (GetStock(type) <= 0)
        {
            missedSales++;
            missed++;
            Debug.Log($"Missed sale: {GetDisplayName(type)} is out of stock.");
            return;
        }

        if (type == ConsumableType.EnergyDrink)
        {
            energyDrinkStock--;
        }
        else
        {
            snackStock--;
        }

        int salePrice = GetSalePrice(type);
        EconomyManager.Instance?.AddMoney(
            salePrice,
            EconomyTransactionCategory.ConsumableRevenue
        );
        totalItemsSold++;
        totalConsumableRevenue += salePrice;
        soldItems++;
        revenue += salePrice;
        Debug.Log($"Sold {GetDisplayName(type)} for {salePrice} RUB.");
    }

    private static float GetEnergyDrinkPurchaseChance(ClientType clientType)
    {
        return clientType switch
        {
            ClientType.Regular => 0.20f,
            ClientType.Gamer => 0.45f,
            ClientType.VIP => 0.65f,
            _ => 0f
        };
    }

    private static float GetSnackPurchaseChance(ClientType clientType)
    {
        return clientType switch
        {
            ClientType.Regular => 0.30f,
            ClientType.Gamer => 0.35f,
            ClientType.VIP => 0.50f,
            _ => 0f
        };
    }

    private static string GetDisplayName(ConsumableType type)
    {
        return type == ConsumableType.EnergyDrink ? "energy drink" : "snack";
    }

    private void OnValidate()
    {
        maximumEnergyDrinkStock = Mathf.Max(1, maximumEnergyDrinkStock);
        maximumSnackStock = Mathf.Max(1, maximumSnackStock);
        restockPackSize = Mathf.Max(1, restockPackSize);
        initialEnergyDrinkStock = Mathf.Clamp(initialEnergyDrinkStock, 0, maximumEnergyDrinkStock);
        initialSnackStock = Mathf.Clamp(initialSnackStock, 0, maximumSnackStock);
    }
}
