#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public sealed class GameplayTelemetryManager : MonoBehaviour
{
    public static GameplayTelemetryManager Instance { get; private set; }

    private readonly List<GameplayDayTelemetry> completedDays = new();
    private int dayStartBalance;
    private int servedAtStart;
    private int lostAtStart;
    private int excellentAtStart;
    private int normalAtStart;
    private int poorAtStart;
    private int missedSalesAtStart;
    private int regularThisDay;
    private int gamerThisDay;
    private int vipThisDay;
    private string randomEventThisDay = ClubRandomEventType.None.ToString();

    public IReadOnlyList<GameplayDayTelemetry> CompletedDays => completedDays;

    private void Awake()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
#else
        enabled = false;
        Destroy(this);
#endif
    }

    private void Start()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        CaptureDayStart();
        if (GameDayManager.Instance != null)
            GameDayManager.Instance.DayEnded += OnDayEnded;
        if (ClubReputationManager.Instance != null)
            ClubReputationManager.Instance.ClientFeedbackCreated += OnClientFeedback;
        if (ClubRandomEventManager.Instance != null)
        {
            ClubRandomEventManager.Instance.EventTriggered += OnEventTriggered;
            if (ClubRandomEventManager.Instance.HasActiveEvent)
                randomEventThisDay = ClubRandomEventManager.Instance.ActiveEventType.ToString();
        }
#endif
    }

    private void OnDestroy()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (GameDayManager.Instance != null)
            GameDayManager.Instance.DayEnded -= OnDayEnded;
        if (ClubReputationManager.Instance != null)
            ClubReputationManager.Instance.ClientFeedbackCreated -= OnClientFeedback;
        if (ClubRandomEventManager.Instance != null)
            ClubRandomEventManager.Instance.EventTriggered -= OnEventTriggered;
        if (Instance == this) Instance = null;
#endif
    }

    private void CaptureDayStart()
    {
        dayStartBalance = EconomyManager.Instance?.Money ?? 0;
        ClubReputationManager reputation = ClubReputationManager.Instance;
        servedAtStart = reputation?.ServedClients ?? 0;
        lostAtStart = reputation?.LostClients ?? 0;
        excellentAtStart = reputation?.ExcellentClients ?? 0;
        normalAtStart = reputation?.NormalClients ?? 0;
        poorAtStart = reputation?.PoorClients ?? 0;
        missedSalesAtStart = ConsumableInventoryManager.Instance?.MissedSales ?? 0;
    }

    private void OnClientFeedback(ClientFeedbackData feedback)
    {
        if (!feedback.WasServed) return;
        switch (feedback.ClientType)
        {
            case ClientType.Regular: regularThisDay++; break;
            case ClientType.Gamer: gamerThisDay++; break;
            case ClientType.VIP: vipThisDay++; break;
        }
    }

    private void OnEventTriggered(ClubRandomEventType eventType, string message)
    {
        randomEventThisDay = eventType.ToString();
    }

    private void OnDayEnded(int completedDay, int income, int expenses, int result)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        GameplayDayTelemetry telemetry = BuildTelemetry(completedDay);
        completedDays.Add(telemetry);
        if (completedDays.Count > 20) completedDays.RemoveAt(0);

        Debug.Log(
            $"[BALANCE] День {completedDay}: баланс {telemetry.startBalance} -> " +
            $"{telemetry.endBalance}, результат {telemetry.netResult}, " +
            $"клиенты {telemetry.servedClients}, потеряно {telemetry.lostClients}."
        );

        regularThisDay = 0;
        gamerThisDay = 0;
        vipThisDay = 0;
        randomEventThisDay = ClubRandomEventType.None.ToString();
        CaptureDayStart();
#endif
    }

    private GameplayDayTelemetry BuildTelemetry(int completedDay)
    {
        DailyFinancialReportData financial = DailyFinancialReportManager.Instance?.LastReport;
        DemandAnalyticsReportData demand = DemandAnalyticsManager.Instance?.LastReport;
        ClubReputationManager reputation = ClubReputationManager.Instance;
        ClubCleanlinessManager cleanliness = ClubCleanlinessManager.Instance;
        ClubProgressionManager progression = ClubProgressionManager.Instance;

        int brokenPCs = 0;
        int criticalEquipmentPCs = 0;
        foreach (PC pc in FindObjectsByType<PC>())
        {
            if (pc.IsBroken) brokenPCs++;
            if (pc.LowestEquipmentCondition <= 20f) criticalEquipmentPCs++;
        }

        int unlockedRooms = 0;
        if (RoomUnlockManager.Instance != null)
        {
            foreach (RoomDoor door in RoomUnlockManager.Instance.RoomDoors)
                if (door != null && door.IsUnlocked) unlockedRooms++;
        }

        int endBalance = EconomyManager.Instance?.Money ?? 0;
        BankruptcyManager bankruptcy = BankruptcyManager.Instance;

        return new GameplayDayTelemetry
        {
            day = completedDay,
            startBalance = dayStartBalance,
            endBalance = endBalance,
            revenue = financial?.Revenue ?? 0,
            bonuses = financial?.Bonuses ?? 0,
            expenses = financial?.TotalExpenses ?? 0,
            netResult = financial?.NetCashChange ?? 0,
            servedClients = reputation != null ? reputation.ServedClients - servedAtStart : 0,
            lostClients = reputation != null ? reputation.LostClients - lostAtStart : 0,
            regularClients = regularThisDay,
            gamerClients = gamerThisDay,
            vipClients = vipThisDay,
            excellentSatisfaction = reputation != null ? reputation.ExcellentClients - excellentAtStart : 0,
            normalSatisfaction = reputation != null ? reputation.NormalClients - normalAtStart : 0,
            poorSatisfaction = reputation != null ? reputation.PoorClients - poorAtStart : 0,
            priceLostClients = demand?.TotalPriceLostClients ?? 0,
            capacityLostClients = demand?.TotalCapacityLostClients ?? 0,
            queueOverflowClients = demand?.queueOverflowClients ?? 0,
            basicUtilization = demand?.basic?.UtilizationPercent ?? 0f,
            gamingUtilization = demand?.gaming?.UtilizationPercent ?? 0f,
            premiumUtilization = demand?.premium?.UtilizationPercent ?? 0f,
            sessionRevenue = financial?.sessionRevenue ?? 0,
            consumableRevenue = financial?.consumableRevenue ?? 0,
            missedConsumableSales = (ConsumableInventoryManager.Instance?.MissedSales ?? 0) - missedSalesAtStart,
            endingCleanliness = cleanliness?.Cleanliness ?? 100f,
            endingTrashCount = cleanliness?.TrashCount ?? 0,
            brokenPCCount = brokenPCs,
            criticalEquipmentPCCount = criticalEquipmentPCs,
            clubLevel = progression?.Level ?? 1,
            clubXP = progression?.Experience ?? 0,
            reputation = reputation?.Reputation ?? 50,
            purchasedPCCount = PCExpansionManager.Instance?.PurchasedPCCount ?? 0,
            unlockedRoomCount = unlockedRooms,
            researchLevels = ClubResearchManager.Instance?.TotalPurchasedLevels ?? 0,
            technicianHired = TechnicianManager.Instance?.TechnicianHired ?? false,
            cleanerHired = CleanerManager.Instance?.CleanerHired ?? false,
            activeInternetPlan = InternetProviderManager.Instance?.ActivePlan.ToString() ?? InternetPlanType.Basic.ToString(),
            activeMarketingCampaign = MarketingManager.Instance?.ActiveCampaign.ToString() ?? MarketingCampaignType.None.ToString(),
            randomEvent = randomEventThisDay,
            bankruptcyRisk = bankruptcy != null &&
                (endBalance <= bankruptcy.BankruptcyThreshold || bankruptcy.ConsecutiveDebtDays > 0)
        };
    }

    [ContextMenu("Export Balance Telemetry")]
    public void ExportTelemetry()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        GameplayTelemetryExport export = new GameplayTelemetryExport
        {
            generatedAtUtc = DateTime.UtcNow.ToString("O"),
            applicationVersion = Application.version,
            days = new List<GameplayDayTelemetry>(completedDays)
        };
        string directory = Path.Combine(Application.persistentDataPath, "Diagnostics");
        Directory.CreateDirectory(directory);
        string filePath = Path.Combine(directory, $"balance_{DateTime.UtcNow:yyyyMMdd_HHmmss}.json");
        File.WriteAllText(filePath, JsonUtility.ToJson(export, true));
        Debug.Log($"Телеметрия сохранена: {filePath}");
#endif
    }
}
#endif
