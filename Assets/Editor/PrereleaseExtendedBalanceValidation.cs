using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

public static class PrereleaseExtendedBalanceValidation
{
    [Serializable]
    private sealed class InvestmentRecord
    {
        public string investment;
        public int purchaseDay;
        public int cost;
        public int targetMinDays;
        public int targetMaxDays;
        public int recoveredDay;
        public int paybackDays = -1;
        public string status = "not_recovered";
    }

    [Serializable]
    private sealed class ExtendedScenarioResult
    {
        public string scenario;
        public int durationDays;
        public int randomSeed;
        public GameplayTelemetryExport telemetry;
        public List<InvestmentRecord> investments = new();
        public bool completedWithoutGameOver;
    }

    [Serializable]
    private sealed class ExtendedQAReport
    {
        public string generatedAtUtc;
        public string applicationVersion;
        public List<ExtendedScenarioResult> scenarios = new();
        public int totalDays;
        public int totalWarnings;
        public int investmentsMeasured;
        public int investmentsInsideTarget;
        public bool normalRangePassed;
        public string reportStatus;
    }

    private enum ExtendedScenarioKind
    {
        Cautious,
        Aggressive,
        Automation,
        Premium,
        CrisisRecovery
    }

    public static void Run()
    {
        ExtendedQAReport report = new ExtendedQAReport
        {
            generatedAtUtc = DateTime.UtcNow.ToString("O"),
            applicationVersion = Application.version
        };

        report.scenarios.Add(RunScenario(
            "Cautious 15 days", ExtendedScenarioKind.Cautious, 15, 22001));
        report.scenarios.Add(RunScenario(
            "Aggressive 15 days", ExtendedScenarioKind.Aggressive, 15, 22002));
        report.scenarios.Add(RunScenario(
            "Automation 15 days", ExtendedScenarioKind.Automation, 15, 22003));
        report.scenarios.Add(RunScenario(
            "Premium 15 days", ExtendedScenarioKind.Premium, 15, 22004));
        report.scenarios.Add(RunScenario(
            "Crisis recovery 10 days", ExtendedScenarioKind.CrisisRecovery, 10, 22005));

        foreach (ExtendedScenarioResult scenario in report.scenarios)
        {
            report.totalDays += scenario.durationDays;
            report.totalWarnings += scenario.telemetry.warnings.Count;
            report.investmentsMeasured += scenario.investments.Count;
            foreach (InvestmentRecord investment in scenario.investments)
            {
                if (investment.status == "inside_target")
                    report.investmentsInsideTarget++;
            }
        }

        GameplayTelemetrySummary normal = report.scenarios[0].telemetry.summary;
        report.normalRangePassed =
            normal.finalClubLevel >= 4 && normal.finalClubLevel <= 5 &&
            normal.finalPCCount >= 7 && normal.finalPCCount <= 11 &&
            normal.finalUnlockedRooms >= 1 &&
            normal.finalResearchLevels >= 3 && normal.finalResearchLevels <= 7 &&
            normal.finalReputation >= 45 && normal.finalReputation <= 85 &&
            normal.endingBalance >= 3000 && normal.endingBalance <= 12000;
        bool allScenariosCompleted =
            report.scenarios.TrueForAll(item => item.completedWithoutGameOver);
        GameplayDayTelemetry automationLast =
            report.scenarios[2].telemetry.days[^1];
        bool automationProgress =
            automationLast.technicianHired && automationLast.cleanerHired &&
            automationLast.researchLevels >= 3;
        GameplayTelemetrySummary premium = report.scenarios[3].telemetry.summary;
        bool premiumProgress = premium.finalUnlockedRooms >= 2 &&
            CountVIPClients(report.scenarios[3].telemetry.days) > 0;
        List<GameplayTelemetryWarning> crisisWarnings =
            report.scenarios[^1].telemetry.warnings;
        bool crisisDiagnostics =
            ContainsWarning(crisisWarnings, "BALANCE_WARNING") &&
            ContainsWarning(crisisWarnings, "REPUTATION_LOCK") &&
            ContainsWarning(crisisWarnings, "PRICE_REJECTION_HIGH") &&
            ContainsWarning(crisisWarnings, "INVENTORY_LOSS");
        report.reportStatus = report.normalRangePassed && allScenariosCompleted &&
            automationProgress && premiumProgress && crisisDiagnostics
                ? "PASS"
                : "REVIEW";

        string directory = Path.Combine(
            Application.persistentDataPath,
            "Diagnostics"
        );
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "prerelease_extended_qa.json");
        File.WriteAllText(path, JsonUtility.ToJson(report, true));
        Debug.Log($"[BALANCE] Extended prerelease QA report: {path}");

        Require(report.normalRangePassed,
            "Normal 15-day scenario is outside prerelease control ranges: " +
            $"level={normal.finalClubLevel}, PCs={normal.finalPCCount}, " +
            $"rooms={normal.finalUnlockedRooms}, research={normal.finalResearchLevels}, " +
            $"reputation={normal.finalReputation}, balance={normal.endingBalance}.");
        Require(allScenariosCompleted,
            "At least one extended scenario reached an unintended game over.");
        Require(automationProgress,
            "Automation scenario did not establish staff and research progression.");
        Require(premiumProgress,
            "Premium scenario did not unlock and use the VIP room.");
        Require(ContainsWarning(crisisWarnings, "BALANCE_WARNING"),
            "Crisis scenario did not produce three-day balance diagnostics.");
        Require(ContainsWarning(crisisWarnings, "REPUTATION_LOCK"),
            "Crisis scenario did not produce REPUTATION_LOCK diagnostics.");
        Require(ContainsWarning(crisisWarnings, "PRICE_REJECTION_HIGH"),
            "Crisis scenario did not produce pricing diagnostics.");
        Require(ContainsWarning(crisisWarnings, "INVENTORY_LOSS"),
            "Crisis scenario did not produce inventory diagnostics.");
    }

    private static ExtendedScenarioResult RunScenario(
        string name,
        ExtendedScenarioKind kind,
        int duration,
        int seed)
    {
        ResetState();
        GameplayTelemetryManager.Instance.ResetForValidation();
        UnityEngine.Random.InitState(seed);
        ExtendedScenarioResult result = new ExtendedScenarioResult
        {
            scenario = name,
            durationDays = duration,
            randomSeed = seed,
            completedWithoutGameOver = true
        };

        for (int day = 1; day <= duration; day++)
        {
            PrepareDay(day);
            if (day == 1)
            {
                EconomyManager.Instance.AddBonusMoney(
                    500,
                    EconomyTransactionCategory.TutorialReward
                );
            }

            ApplyDecisions(kind, day, result.investments);
            int served = GetServedCount(kind, day);
            int lost = GetLostCount(kind, day);
            for (int index = 0; index < served; index++)
            {
                ClientType type = GetClientType(kind, day, index);
                CompleteSession(type);
                ClientSatisfaction satisfaction = GetSatisfaction(kind, day);
                ClubReputationManager.Instance.RegisterServedClient(
                    type,
                    satisfaction,
                    kind == ExtendedScenarioKind.CrisisRecovery && day <= 6 ? 24f : 3f,
                    kind == ExtendedScenarioKind.CrisisRecovery && day <= 6 ? 30f : 88f,
                    kind == ExtendedScenarioKind.CrisisRecovery && day <= 6 ? 35f : 90f
                );
            }

            RecordLostClients(kind, day, lost);
            ExerciseInventoryAndStaff(kind, day);
            ApplySyntheticUtilization(kind, day);
            GameDayManager.Instance.QACompleteCurrentDay();

            if (BankruptcyManager.Instance.IsGameOver)
            {
                result.completedWithoutGameOver = false;
                break;
            }
        }

        result.telemetry = GameplayTelemetryManager.Instance.CreateExportData();
        EstimatePaybacks(result.investments, result.telemetry.days);
        return result;
    }

    private static void PrepareDay(int day)
    {
        ClubRandomEventManager.Instance.RestoreState(null, true);
        DailyGoalManager.Instance.RestoreState(
            day,
            0,
            int.MaxValue,
            1,
            ClubReputationManager.Instance.ServedClients,
            EconomyManager.Instance.TotalIncome,
            true
        );
    }

    private static void ApplyDecisions(
        ExtendedScenarioKind kind,
        int day,
        List<InvestmentRecord> investments)
    {
        switch (kind)
        {
            case ExtendedScenarioKind.Cautious:
                PricingManager.Instance.RestoreState(110, 110, 105);
                if (day >= 2 && FindPC("PC_01").Tier == PCTier.Basic)
                    TryUpgrade("PC_01", day, investments);
                if (day >= 3 && ClubResearchManager.Instance.TotalPurchasedLevels < 1)
                    TryResearch(ClubResearchType.WholesalePurchasing, day, investments);
                if (day >= 4 && PCExpansionManager.Instance.PurchasedPCCount < 1)
                    TryExpansion(day, investments);
                if (day >= 6) TryUnlockRoom("PrivateRoom01", day, investments);
                if (day >= 8 && ClubResearchManager.Instance.TotalPurchasedLevels < 2)
                    TryResearch(ClubResearchType.EnergyEfficiency, day, investments);
                if (day >= 10 && PCExpansionManager.Instance.PurchasedPCCount < 2)
                    TryExpansion(day, investments);
                if (day >= 12 && ClubResearchManager.Instance.TotalPurchasedLevels < 3)
                    TryResearch(ClubResearchType.ReliableComponents, day, investments);
                break;

            case ExtendedScenarioKind.Aggressive:
                PricingManager.Instance.RestoreState(125, 135, 145);
                if (day >= 2 && PCExpansionManager.Instance.PurchasedPCCount < 1)
                    TryExpansion(day, investments);
                if (day >= 2 && FindPC("PC_01").Tier == PCTier.Basic)
                    TryUpgrade("PC_01", day, investments);
                if (day >= 3 && !MarketingManager.Instance.HasActiveCampaign)
                    TryMarketing(MarketingCampaignType.SocialMedia, day, investments);
                if (day >= 4 && PCExpansionManager.Instance.PurchasedPCCount < 3)
                    TryExpansion(day, investments);
                if (day >= 5 && FindPC("PC_02").Tier == PCTier.Basic)
                    TryUpgrade("PC_02", day, investments);
                if (day >= 6 && ClubResearchManager.Instance.TotalPurchasedLevels < 2)
                    TryResearch(ClubResearchType.BrandPromotion, day, investments);
                if (day >= 7) TryUnlockRoom("PrivateRoom01", day, investments);
                if (day >= 8 && InternetProviderManager.Instance.ActivePlan == InternetPlanType.Basic)
                    TryInternet(InternetPlanType.Gaming, day, investments);
                break;

            case ExtendedScenarioKind.Automation:
                PricingManager.Instance.RestoreState(110, 115, 110);
                if (day >= 3 && !CleanerManager.Instance.CleanerHired)
                    TryCleaner(day, investments);
                if (day >= 5 && !TechnicianManager.Instance.TechnicianHired)
                    TryTechnician(day, investments);
                bool staffReady = CleanerManager.Instance.CleanerHired &&
                    TechnicianManager.Instance.TechnicianHired;
                if (staffReady && day >= 6 &&
                    ClubResearchManager.Instance.TotalPurchasedLevels < 1)
                    TryResearch(ClubResearchType.EfficientCleaning, day, investments);
                if (staffReady && day >= 8 &&
                    ClubResearchManager.Instance.TotalPurchasedLevels < 2)
                    TryResearch(ClubResearchType.DurableEquipment, day, investments);
                if (staffReady && day >= 10 &&
                    ClubResearchManager.Instance.TotalPurchasedLevels < 3)
                    TryResearch(ClubResearchType.ReliableComponents, day, investments);
                if (staffReady && day >= 12 &&
                    ClubResearchManager.Instance.TotalPurchasedLevels < 4)
                    TryResearch(ClubResearchType.EnergyEfficiency, day, investments);
                if (staffReady && day >= 11 &&
                    PCExpansionManager.Instance.PurchasedPCCount < 1)
                    TryExpansion(day, investments);
                if (staffReady && day >= 13)
                    TryUnlockRoom("PrivateRoom01", day, investments);
                break;

            case ExtendedScenarioKind.Premium:
                PricingManager.Instance.RestoreState(105, 120, 135);
                if (day >= 2 && FindPC("PC_01").Tier != PCTier.Premium)
                    TryUpgrade("PC_01", day, investments);
                if (day >= 5) TryUnlockRoom("PrivateRoom01", day, investments);
                if (day >= 6 && ClubResearchManager.Instance.TotalPurchasedLevels < 2)
                    TryResearch(ClubResearchType.NetworkOptimization, day, investments);
                if (day >= 8 && InternetProviderManager.Instance.ActivePlan == InternetPlanType.Basic)
                    TryInternet(InternetPlanType.Gaming, day, investments);
                if (day >= 12) TryUnlockRoom("VIPRoom01", day, investments);
                if (day >= 13 && !MarketingManager.Instance.HasActiveCampaign)
                    TryMarketing(MarketingCampaignType.VIPPromotion, day, investments);
                break;

            case ExtendedScenarioKind.CrisisRecovery:
                bool recovery = day >= 7;
                PricingManager.Instance.RestoreState(
                    recovery ? 100 : 160,
                    recovery ? 105 : 160,
                    recovery ? 110 : 160
                );
                if (day == 1) TryExpansion(day, investments);
                if (day == 2 && FindPC("PC_01").Tier == PCTier.Basic)
                    TryUpgrade("PC_01", day, investments);
                if (day == 3 && !MarketingManager.Instance.HasActiveCampaign)
                    TryMarketing(MarketingCampaignType.SocialMedia, day, investments);
                break;
        }
    }

    private static int GetServedCount(ExtendedScenarioKind kind, int day)
    {
        return kind switch
        {
            ExtendedScenarioKind.Cautious => day == 1 ? 5 : day == 2 ? 6 : day == 3 ? 7 : day == 15 ? 11 : 10,
            ExtendedScenarioKind.Aggressive => day == 1 ? 5 : 10,
            ExtendedScenarioKind.Automation => day == 1 ? 5 : 12,
            ExtendedScenarioKind.Premium => day == 1 ? 5 : 12,
            ExtendedScenarioKind.CrisisRecovery => day switch
            {
                <= 3 => 3,
                4 => 0,
                5 => 1,
                6 => 4,
                _ => 8
            },
            _ => 5
        };
    }

    private static int GetLostCount(ExtendedScenarioKind kind, int day)
    {
        return kind switch
        {
            ExtendedScenarioKind.Aggressive when day >= 3 => 2,
            ExtendedScenarioKind.Premium when day >= 6 && day < 12 => 2,
            ExtendedScenarioKind.CrisisRecovery when day <= 6 => 5,
            _ => 0
        };
    }

    private static ClientType GetClientType(
        ExtendedScenarioKind kind,
        int day,
        int index)
    {
        ClientType preferred = ClientType.Regular;
        if (kind == ExtendedScenarioKind.Premium && day >= 3)
            preferred = index % 2 == 0 ? ClientType.VIP : ClientType.Gamer;
        else if ((kind == ExtendedScenarioKind.Aggressive ||
                  kind == ExtendedScenarioKind.Automation) && day >= 3)
            preferred = index % 2 == 0 ? ClientType.Gamer : ClientType.Regular;

        return SelectPC(preferred) != null
            ? preferred
            : ClientType.Regular;
    }

    private static ClientSatisfaction GetSatisfaction(
        ExtendedScenarioKind kind,
        int day)
    {
        if (kind == ExtendedScenarioKind.CrisisRecovery)
            return day <= 6 ? ClientSatisfaction.Poor : ClientSatisfaction.Normal;

        int reputation = ClubReputationManager.Instance.Reputation;
        if (reputation >= 82) return ClientSatisfaction.Poor;
        return day <= 2 ? ClientSatisfaction.Normal : ClientSatisfaction.Excellent;
    }

    private static void RecordLostClients(
        ExtendedScenarioKind kind,
        int day,
        int count)
    {
        for (int index = 0; index < count; index++)
        {
            bool capacityLoss = kind == ExtendedScenarioKind.Aggressive &&
                day >= 5 && index == 0;
            int tolerance = capacityLoss ? 200 : 100;
            ClientType type = kind == ExtendedScenarioKind.Premium
                ? ClientType.VIP
                : ClientType.Regular;
            DemandAnalyticsManager.Instance.RecordClientDeparture(type, tolerance);
            ClubReputationManager.Instance.RegisterLostClient(type, 30f);
        }
    }

    private static void ExerciseInventoryAndStaff(
        ExtendedScenarioKind kind,
        int day)
    {
        if (kind == ExtendedScenarioKind.CrisisRecovery && day <= 6)
        {
            ConsumableInventoryManager inventory = ConsumableInventoryManager.Instance;
            inventory.RestoreState(0, 0, inventory.TotalItemsSold,
                inventory.TotalConsumableRevenue, inventory.MissedSales);
            SetField(inventory, "forcePurchaseDecisions", true);
            SetField(inventory, "forceEnergyDrinkPurchase", true);
            SetField(inventory, "forceSnackPurchase", true);
            for (int index = 0; index < 6; index++)
                inventory.TrySellToClient(ClientType.Regular);
        }

        if (kind != ExtendedScenarioKind.Automation)
        {
            if (kind != ExtendedScenarioKind.CrisisRecovery || day >= 7)
                MaintainCriticalEquipment();
            return;
        }

        PC pc = FindPC("PC_01");
        if (TechnicianManager.Instance.TechnicianHired)
        {
            pc.SetEquipmentCondition(PCEquipmentType.Mouse, 10f);
            TechnicianManager.Instance.TryServiceCriticalEquipment();
        }
        if (CleanerManager.Instance.CleanerHired)
        {
            ClubCleanlinessManager.Instance.EnsureTutorialTrash(pc);
            IReadOnlyList<TrashItem> trash =
                ClubCleanlinessManager.Instance.ActiveTrashItems;
            if (trash.Count > 0)
            {
                string source = trash[0].SourcePCName;
                if (ClubCleanlinessManager.Instance.CleanTrash(trash[0]))
                    CleanerManager.Instance.ReportTrashCleaned(source);
            }
        }

        MaintainCriticalEquipment(15f);
    }

    private static void MaintainCriticalEquipment(float threshold = 30f)
    {
        foreach (PC pc in UnityEngine.Object.FindObjectsByType<PC>())
        {
            if (pc != null && pc.HasRoomAccess && pc.IsFree &&
                pc.LowestEquipmentCondition <= threshold)
            {
                pc.TryRepairAllEquipment();
            }
        }
    }

    private static void ApplySyntheticUtilization(
        ExtendedScenarioKind kind,
        int day)
    {
        if (kind != ExtendedScenarioKind.Aggressive || day < 5) return;
        DemandTierAnalyticsData basic =
            DemandAnalyticsManager.Instance.CurrentReport.basic;
        basic.accessiblePCSeconds = 100f;
        basic.occupiedPCSeconds = 95f;
    }

    private static void CompleteSession(ClientType clientType)
    {
        PC selected = SelectPC(clientType);
        Require(selected != null, $"No compatible PC for {clientType}.");
        int before = EconomyManager.Instance.Money;
        Require(selected.TryReserve() && selected.TryOccupyReserved(clientType),
            $"Could not occupy {selected.name} for {clientType}.");
        Require(EconomyManager.Instance.Money == before + selected.LastSessionIncome,
            "Extended scenario session credited an unexpected amount.");
        Invoke(selected, "CompleteSession");
    }

    private static PC SelectPC(ClientType clientType)
    {
        PC selected = null;
        int bestScore = int.MinValue;
        foreach (PC pc in UnityEngine.Object.FindObjectsByType<PC>())
        {
            if (pc == null || !pc.IsAvailable || !pc.HasRoomAccess ||
                pc.HasBrokenEquipment || !Client.IsTierCompatible(clientType, pc.Tier))
                continue;

            int score = clientType switch
            {
                ClientType.VIP => pc.Tier == PCTier.Premium ? 30 : 0,
                ClientType.Gamer => pc.Tier == PCTier.Gaming ? 30 : 20,
                _ => pc.Tier == PCTier.Basic ? 30 : 10
            };
            if (score <= bestScore) continue;
            bestScore = score;
            selected = pc;
        }
        return selected;
    }

    private static void TryExpansion(int day, List<InvestmentRecord> investments)
    {
        int before = EconomyManager.Instance.Money;
        if (PCExpansionManager.Instance.TryPurchaseNextPC())
            AddInvestment("Additional PC", day, before - EconomyManager.Instance.Money,
                2, 4, investments);
    }

    private static void TryUpgrade(
        string pcName,
        int day,
        List<InvestmentRecord> investments)
    {
        PC pc = FindPC(pcName);
        PCTier beforeTier = pc.Tier;
        int before = EconomyManager.Instance.Money;
        Invoke(pc, "TryUpgrade");
        if (pc.Tier == beforeTier) return;

        string label = pc.Tier == PCTier.Gaming
            ? "Gaming upgrade"
            : "Premium upgrade";
        AddInvestment(
            label,
            day,
            before - EconomyManager.Instance.Money,
            pc.Tier == PCTier.Gaming ? 3 : 4,
            pc.Tier == PCTier.Gaming ? 5 : 7,
            investments
        );
    }

    private static void TryResearch(
        ClubResearchType type,
        int day,
        List<InvestmentRecord> investments)
    {
        int before = EconomyManager.Instance.Money;
        if (ClubResearchManager.Instance.TryPurchaseResearch(type))
            AddInvestment("Research I", day, before - EconomyManager.Instance.Money,
                4, 8, investments);
    }

    private static void TryCleaner(int day, List<InvestmentRecord> investments)
    {
        int before = EconomyManager.Instance.Money;
        if (CleanerManager.Instance.TryHireCleaner())
            AddInvestment("Cleaner", day, before - EconomyManager.Instance.Money,
                4, 7, investments);
    }

    private static void TryTechnician(int day, List<InvestmentRecord> investments)
    {
        int before = EconomyManager.Instance.Money;
        if (TechnicianManager.Instance.TryHireTechnician())
            AddInvestment("Technician", day, before - EconomyManager.Instance.Money,
                5, 8, investments);
    }

    private static void TryInternet(
        InternetPlanType plan,
        int day,
        List<InvestmentRecord> investments)
    {
        int before = EconomyManager.Instance.Money;
        if (InternetProviderManager.Instance.TrySwitchPlan(plan))
        {
            AddInvestment(
                plan == InternetPlanType.Gaming ? "Internet Gaming" : "Internet Professional",
                day,
                before - EconomyManager.Instance.Money,
                plan == InternetPlanType.Gaming ? 4 : 6,
                plan == InternetPlanType.Gaming ? 6 : 10,
                investments
            );
        }
    }

    private static void TryMarketing(
        MarketingCampaignType campaign,
        int day,
        List<InvestmentRecord> investments)
    {
        int before = EconomyManager.Instance.Money;
        if (MarketingManager.Instance.TryStartCampaign(campaign))
            AddInvestment("Marketing campaign", day,
                before - EconomyManager.Instance.Money, 1, 3, investments);
    }

    private static void TryUnlockRoom(
        string doorId,
        int day,
        List<InvestmentRecord> investments)
    {
        RoomDoor door = RoomUnlockManager.Instance.FindDoor(doorId);
        if (door == null || door.IsUnlocked) return;
        int before = EconomyManager.Instance.Money;
        door.Interact();
        if (!door.IsUnlocked) return;

        AddInvestment("Room", day, before - EconomyManager.Instance.Money,
            6, 12, investments);
        RestoreRoomPCTiers(doorId);
    }

    private static void RestoreRoomPCTiers(string doorId)
    {
        if (doorId == "PrivateRoom01")
        {
            FindPC("PC_10").RestoreTier(PCTier.Gaming);
            FindPC("PC_11").RestoreTier(PCTier.Gaming);
        }
        else if (doorId == "VIPRoom01")
        {
            FindPC("PC_12").RestoreTier(PCTier.Premium);
            FindPC("PC_13").RestoreTier(PCTier.Premium);
        }
    }

    private static void AddInvestment(
        string label,
        int day,
        int cost,
        int targetMin,
        int targetMax,
        List<InvestmentRecord> investments)
    {
        if (cost <= 0) return;
        investments.Add(new InvestmentRecord
        {
            investment = label,
            purchaseDay = day,
            cost = cost,
            targetMinDays = targetMin,
            targetMaxDays = targetMax
        });
    }

    private static void EstimatePaybacks(
        List<InvestmentRecord> investments,
        IReadOnlyList<GameplayDayTelemetry> days)
    {
        foreach (InvestmentRecord investment in investments)
        {
            float baseline = GetPreInvestmentBaseline(days, investment.purchaseDay);
            float recovered = 0f;
            foreach (GameplayDayTelemetry day in days)
            {
                if (day.day <= investment.purchaseDay) continue;
                recovered += Mathf.Max(0f, day.netResult - baseline);
                if (recovered < investment.cost) continue;

                investment.recoveredDay = day.day;
                investment.paybackDays = day.day - investment.purchaseDay;
                investment.status = investment.paybackDays < investment.targetMinDays
                    ? "too_fast"
                    : investment.paybackDays > investment.targetMaxDays
                        ? "too_slow"
                        : "inside_target";
                break;
            }
        }
    }

    private static float GetPreInvestmentBaseline(
        IReadOnlyList<GameplayDayTelemetry> days,
        int purchaseDay)
    {
        int count = 0;
        int total = 0;
        for (int index = days.Count - 1; index >= 0 && count < 3; index--)
        {
            if (days[index].day >= purchaseDay) continue;
            total += days[index].netResult;
            count++;
        }
        return count > 0 ? total / (float)count : 0f;
    }

    private static void ResetState()
    {
        EconomyManager.Instance.RestoreState(1200, 0, 0);
        ClubReputationManager.Instance.RestoreState(50, 0, 0, 0, 0, 0);
        ClubProgressionManager.Instance.RestoreState(1, 0);
        PricingManager.Instance.RestoreState(100, 100, 100);
        ConsumableInventoryManager.Instance.RestoreState(0, 5, 0, 0, 0);
        InternetProviderManager.Instance.RestoreState(InternetPlanType.Basic);
        MarketingManager.Instance.RestoreState(MarketingCampaignType.None, 0);
        ClubRandomEventManager.Instance.RestoreState(null, true);
        ClubResearchManager.Instance.RestoreState(null);
        TechnicianManager.Instance.RestoreState(false);
        CleanerManager.Instance.RestoreState(false);
        ClubCleanlinessManager.Instance.RestoreState(null);
        BankruptcyManager.Instance.RestoreState(0);
        SetField(BankruptcyManager.Instance, "isGameOver", false);
        SetField(BankruptcyManager.Instance, "gameOverDay", 0);
        SetField(BankruptcyManager.Instance, "finalBalance", 0);
        FirstDayTutorialManager.Instance.RestoreState(true, true, 0, 0, false);
        DailyFinancialReportManager.Instance.RestoreState(null, null, 1);
        DemandAnalyticsManager.Instance.RestoreState(null, null, 1);
        GameDayManager.Instance.RestoreState(1, 120f, 0, 0);

        foreach (PC pc in UnityEngine.Object.FindObjectsByType<PC>())
        {
            if (pc != null && pc.name.StartsWith("PC_", StringComparison.Ordinal) &&
                int.TryParse(pc.name.Substring(3), out int number) &&
                number >= 6 && number <= 9)
            {
                UnityEngine.Object.DestroyImmediate(pc.gameObject);
            }
        }
        SetField(PCExpansionManager.Instance, "nextSlotIndex", 0);
        PCExpansionManager.Instance.RestorePurchasedPCs(0);
        ClubLayoutBuilder.EnsureRuntimeLayout().BuildLayout();

        foreach (RoomDoor door in UnityEngine.Object.FindObjectsByType<RoomDoor>())
            door.RestoreState(false);
        foreach (PC pc in UnityEngine.Object.FindObjectsByType<PC>())
        {
            int number = int.TryParse(pc.name.Substring(3), out int parsed)
                ? parsed
                : 0;
            PCTier tier = number is 10 or 11
                ? PCTier.Gaming
                : number is 12 or 13
                    ? PCTier.Premium
                    : PCTier.Basic;
            pc.RestoreTier(tier);
            pc.RestoreEquipmentCondition(100f, 100f, 100f);
            pc.SetState(PCState.Free);
            SetField(pc, "breakdownChance", 0f);
        }
    }

    private static PC FindPC(string name)
    {
        foreach (PC pc in UnityEngine.Object.FindObjectsByType<PC>())
            if (pc != null && pc.name == name) return pc;
        throw new InvalidOperationException($"{name} was not found.");
    }

    private static object Invoke(object target, string methodName)
    {
        MethodInfo method = target.GetType().GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
        );
        if (method == null)
            throw new MissingMethodException(target.GetType().Name, methodName);
        try
        {
            return method.Invoke(target, null);
        }
        catch (TargetInvocationException exception)
        {
            throw exception.InnerException ?? exception;
        }
    }

    private static void SetField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic
        );
        if (field == null)
            throw new MissingFieldException(target.GetType().Name, fieldName);
        field.SetValue(target, value);
    }

    private static bool ContainsWarning(
        IReadOnlyList<GameplayTelemetryWarning> warnings,
        string code)
    {
        foreach (GameplayTelemetryWarning warning in warnings)
            if (warning.code == code) return true;
        return false;
    }

    private static int CountVIPClients(
        IReadOnlyList<GameplayDayTelemetry> days)
    {
        int total = 0;
        foreach (GameplayDayTelemetry day in days)
            total += day.vipClients;
        return total;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
