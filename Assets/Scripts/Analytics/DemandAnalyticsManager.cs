using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class DemandAnalyticsManager : MonoBehaviour
{
    public static DemandAnalyticsManager Instance { get; private set; }

    [SerializeField, Min(0.1f)] private float hudRefreshInterval = 1f;

    private readonly List<PC> pcs = new();
    private DemandAnalyticsReportData currentReport = new();
    private DemandAnalyticsReportData lastReport;
    private float hudRefreshTimer;

    public DemandAnalyticsReportData CurrentReport => currentReport;
    public DemandAnalyticsReportData LastReport => lastReport;
    public bool HasLastReport => lastReport != null && lastReport.day > 0;

    public event Action StatusChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        currentReport.Reset(1);
    }

    private void Start()
    {
        PC.PCRegistered += RegisterPC;
        PC.PCUnregistered += UnregisterPC;
        RegisterExistingPCs();

        if (currentReport.day <= 0)
        {
            currentReport.Reset(
                GameDayManager.Instance != null
                    ? GameDayManager.Instance.CurrentDay
                    : 1
            );
        }
    }

    private void Update()
    {
        float deltaTime = Time.deltaTime;
        if (deltaTime <= 0f)
        {
            return;
        }

        SamplePCUtilization(deltaTime);
        hudRefreshTimer -= deltaTime;
        if (hudRefreshTimer > 0f)
        {
            return;
        }

        hudRefreshTimer = hudRefreshInterval;
        StatusChanged?.Invoke();
    }

    private void OnDestroy()
    {
        PC.PCRegistered -= RegisterPC;
        PC.PCUnregistered -= UnregisterPC;

        foreach (PC pc in pcs)
        {
            UnsubscribeFromPC(pc);
        }

        pcs.Clear();
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void RegisterExistingPCs()
    {
        foreach (PC pc in FindObjectsByType<PC>())
        {
            RegisterPC(pc);
        }
    }

    private void RegisterPC(PC pc)
    {
        if (pc == null || pcs.Contains(pc))
        {
            return;
        }

        pcs.Add(pc);
        pc.SessionAnalyticsCompleted += OnSessionAnalyticsCompleted;
    }

    private void UnregisterPC(PC pc)
    {
        if (pc == null)
        {
            return;
        }

        UnsubscribeFromPC(pc);
        pcs.Remove(pc);
    }

    private void UnsubscribeFromPC(PC pc)
    {
        if (pc != null)
        {
            pc.SessionAnalyticsCompleted -= OnSessionAnalyticsCompleted;
        }
    }

    private void SamplePCUtilization(float deltaTime)
    {
        pcs.RemoveAll(pc => pc == null);

        foreach (PC pc in pcs)
        {
            if (pc == null || !pc.HasRoomAccess)
            {
                continue;
            }

            DemandTierAnalyticsData tierData = currentReport.GetTierData(pc.Tier);
            tierData.accessiblePCSeconds += deltaTime;
            if (pc.IsOccupied)
            {
                tierData.occupiedPCSeconds += deltaTime;
            }
        }
    }

    private void OnSessionAnalyticsCompleted(PCSessionAnalyticsData session)
    {
        DemandTierAnalyticsData tierData = currentReport.GetTierData(session.Tier);
        tierData.completedSessions++;
        tierData.sessionRevenue += Mathf.Max(0, session.SessionRevenue);
        StatusChanged?.Invoke();
    }

    public void RecordClientDeparture(
        ClientType clientType,
        int priceTolerancePercent)
    {
        pcs.RemoveAll(pc => pc == null);
        List<PC> compatiblePCs = new();

        foreach (PC pc in pcs)
        {
            if (pc != null && pc.HasRoomAccess &&
                Client.IsTierCompatible(clientType, pc.Tier))
            {
                compatiblePCs.Add(pc);
            }
        }

        if (compatiblePCs.Count == 0)
        {
            return;
        }

        bool hasAffordableTier = false;
        foreach (PC pc in compatiblePCs)
        {
            int pricePercent = PricingManager.Instance != null
                ? PricingManager.Instance.GetPricePercent(pc.Tier)
                : 100;
            if (pricePercent <= priceTolerancePercent)
            {
                hasAffordableTier = true;
                break;
            }
        }

        PC preferredPC = FindPreferredPC(
            compatiblePCs,
            clientType,
            priceTolerancePercent,
            hasAffordableTier
        );
        if (preferredPC == null)
        {
            return;
        }

        DemandTierAnalyticsData tierData = currentReport.GetTierData(preferredPC.Tier);
        if (hasAffordableTier)
        {
            tierData.capacityLostClients++;
        }
        else
        {
            tierData.priceLostClients++;
            tierData.estimatedPriceLostRevenue += EstimateAffordableRevenue(
                preferredPC,
                clientType,
                priceTolerancePercent
            );
        }

        StatusChanged?.Invoke();
    }

    public void RecordQueueOverflow()
    {
        currentReport.queueOverflowClients++;
        StatusChanged?.Invoke();
    }

    public void FinalizeDay(int completedDay)
    {
        currentReport.day = completedDay;
        lastReport = currentReport.Clone();
        currentReport = new DemandAnalyticsReportData();
        currentReport.Reset(completedDay + 1);
        StatusChanged?.Invoke();
    }

    public DemandAnalyticsReportData CreateCurrentSaveData() => currentReport.Clone();
    public DemandAnalyticsReportData CreateLastSaveData() => lastReport?.Clone();

    public void RestoreState(
        DemandAnalyticsReportData savedCurrentReport,
        DemandAnalyticsReportData savedLastReport,
        int currentDay)
    {
        currentReport = savedCurrentReport?.Clone() ?? new DemandAnalyticsReportData();
        if (currentReport.day <= 0)
        {
            currentReport.Reset(currentDay);
        }

        lastReport = savedLastReport?.Clone();
        StatusChanged?.Invoke();
    }

    private static PC FindPreferredPC(
        IEnumerable<PC> compatiblePCs,
        ClientType clientType,
        int priceTolerancePercent,
        bool requireAffordable)
    {
        PC selectedPC = null;
        int selectedPriority = int.MaxValue;
        foreach (PC pc in compatiblePCs)
        {
            int pricePercent = PricingManager.Instance != null
                ? PricingManager.Instance.GetPricePercent(pc.Tier)
                : 100;
            if (requireAffordable && pricePercent > priceTolerancePercent)
            {
                continue;
            }

            int priority = GetTierPriority(clientType, pc.Tier);
            if (priority < selectedPriority)
            {
                selectedPC = pc;
                selectedPriority = priority;
            }
        }

        return selectedPC;
    }

    private static int GetTierPriority(ClientType clientType, PCTier tier)
    {
        return clientType switch
        {
            ClientType.Regular => tier switch
            {
                PCTier.Basic => 0,
                PCTier.Gaming => 1,
                PCTier.Premium => 2,
                _ => 10
            },
            ClientType.Gamer => tier == PCTier.Gaming ? 0 :
                tier == PCTier.Premium ? 1 : 10,
            ClientType.VIP => tier == PCTier.Premium ? 0 : 10,
            _ => 10
        };
    }

    private static int EstimateAffordableRevenue(
        PC pc,
        ClientType clientType,
        int priceTolerancePercent)
    {
        int estimatedPrice = Mathf.RoundToInt(
            pc.BaseSessionPrice * Mathf.Max(1, priceTolerancePercent) / 100f
        );
        return Mathf.Max(
            0,
            estimatedPrice + PC.GetClientSessionBonus(clientType)
        );
    }
}
