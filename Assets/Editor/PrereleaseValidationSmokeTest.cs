using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[InitializeOnLoad]
public static class PrereleaseValidationSmokeTest
{
    private const string PendingKey = "CyberClub.PrereleaseValidation.Pending";
    private const string SampleScenePath = "Assets/Scenes/SampleScene.unity";
    private static readonly string SavePath = Path.Combine(
        Application.persistentDataPath,
        "cyber_club_save.json"
    );
    private static readonly string SaveBackupPath = Path.Combine(
        Path.GetTempPath(),
        "cyber_club_prerelease_validation_save.bak"
    );
    private static readonly string SettingsPath = Path.Combine(
        Application.persistentDataPath,
        "settings.json"
    );
    private static readonly string SettingsBackupPath = Path.Combine(
        Path.GetTempPath(),
        "cyber_club_prerelease_validation_settings.bak"
    );

    private static double runAt;
    private static bool testFailed;

    [Serializable]
    private sealed class BalanceScenarioResult
    {
        public string scenario;
        public int[] endingBalances = new int[5];
        public int[] servedClients = new int[5];
        public int[] lostClients = new int[5];
        public int finalLevel;
        public int finalReputation;
        public int purchasedPCCount;
        public int researchLevels;
        public bool technicianHired;
        public bool cleanerHired;
        public bool bankruptcyRisk;
    }

    [Serializable]
    private sealed class BalanceValidationExport
    {
        public string generatedAtUtc;
        public int randomSeed = 12345;
        public List<BalanceScenarioResult> scenarios = new();
    }

    private enum ScenarioKind
    {
        Cautious,
        Aggressive,
        Poor
    }

    static PrereleaseValidationSmokeTest()
    {
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    public static void Run()
    {
        try
        {
            BackupSave();
            testFailed = false;
            EditorPrefs.SetBool(PendingKey, true);
            EditorSceneManager.OpenScene(SampleScenePath, OpenSceneMode.Single);
            EditorApplication.isPlaying = true;
        }
        catch (Exception exception)
        {
            Fail(exception);
        }
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (!EditorPrefs.GetBool(PendingKey, false)) return;

        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            runAt = EditorApplication.timeSinceStartup + 1.5d;
            return;
        }

        if (state != PlayModeStateChange.EnteredEditMode) return;

        RestoreSave();
        EditorPrefs.DeleteKey(PendingKey);
        if (testFailed)
        {
            EditorApplication.Exit(1);
            return;
        }

        Debug.Log("PRERELEASE_VALIDATION_SMOKE_TEST: PASS");
        EditorApplication.Exit(0);
    }

    private static void Tick()
    {
        if (!EditorPrefs.GetBool(PendingKey, false) ||
            !EditorApplication.isPlaying ||
            EditorApplication.timeSinceStartup < runAt)
        {
            return;
        }

        runAt = double.MaxValue;
        try
        {
            ValidateQAAndEconomicInvariants();
            ValidateSaveCompatibility();
            RunBalanceScenarios();
            PrereleaseExtendedBalanceValidation.Run();
            EditorApplication.isPlaying = false;
        }
        catch (Exception exception)
        {
            Fail(exception);
        }
    }

    private static void ValidateQAAndEconomicInvariants()
    {
        Require(SceneManager.GetActiveScene().name == "SampleScene",
            "Validation did not start in SampleScene.");
        ValidateHUDInvariants();
        Require(GameplayTelemetryManager.Instance != null,
            "GameplayTelemetryManager is missing.");
        Require(PrereleaseQAPanel.Instance != null,
            "PrereleaseQAPanel is missing in the Editor.");

        PrereleaseQAPanel qa = PrereleaseQAPanel.Instance;
        Time.timeScale = 1f;
        qa.OpenPanel();
        Require(qa.IsOpen && Time.timeScale == 0f,
            "Opening QA did not pause the game.");
        qa.SetTimeMultiplier(2f);
        Require(Time.timeScale == 0f,
            "Changing QA speed resumed the game while the panel was open.");
        qa.ClosePanel();
        Require(!qa.IsOpen && Mathf.Approximately(Time.timeScale, 2f),
            "Closing QA did not restore the selected time scale.");
        Time.timeScale = 1f;

        qa.ApplyRandomSeed(12345);
        float[] firstSequence = CaptureRandomSequence();
        qa.ApplyRandomSeed(12345);
        float[] secondSequence = CaptureRandomSequence();
        for (int index = 0; index < firstSequence.Length; index++)
        {
            Require(Mathf.Approximately(firstSequence[index], secondSequence[index]),
                "Fixed random seed is not deterministic.");
        }

        Require(qa.IsTaintedByDebugActions,
            "QA actions did not taint the session.");
        Require(!SaveManager.Instance.TrySaveGame(),
            "A QA-tainted session was written to the normal save.");

        ResetScenarioState();
        PC pc = FindPC("PC_01");
        int moneyBeforeSession = EconomyManager.Instance.Money;
        int reportBeforeSession =
            DailyFinancialReportManager.Instance.CurrentReport.sessionRevenue;
        Require(pc.TryOccupy(), "The invariant session could not start.");
        int sessionIncome = pc.LastSessionIncome;
        Require(!pc.TryOccupy(), "An occupied PC accepted a second session.");
        Require(EconomyManager.Instance.Money == moneyBeforeSession + sessionIncome,
            "A session did not credit exactly once.");
        Require(DailyFinancialReportManager.Instance.CurrentReport.sessionRevenue ==
                reportBeforeSession + sessionIncome,
            "Session revenue was not recorded exactly once.");

        int capturedIncome = pc.LastSessionIncome;
        PricingManager.Instance.TryChangePrice(PCTier.Basic, 1);
        Invoke(pc, "CompleteSession");
        Require(pc.LastSessionIncome == capturedIncome,
            "A tariff change altered an active session.");

        ConsumableInventoryManager inventory = ConsumableInventoryManager.Instance;
        inventory.RestoreState(1, 0, 0, 0, 0);
        SetField(inventory, "forcePurchaseDecisions", true);
        SetField(inventory, "forceEnergyDrinkPurchase", true);
        SetField(inventory, "forceSnackPurchase", false);
        int consumableIncomeBefore = EconomyManager.Instance.TotalIncome;
        inventory.TrySellToClient(ClientType.Regular);
        int firstSaleIncome = EconomyManager.Instance.TotalIncome;
        inventory.TrySellToClient(ClientType.Regular);
        Require(firstSaleIncome > consumableIncomeBefore &&
                EconomyManager.Instance.TotalIncome == firstSaleIncome,
            "An exhausted consumable stock produced duplicate revenue.");

        EconomyManager.Instance.AddBonusMoney(
            50,
            EconomyTransactionCategory.DailyGoalReward
        );
        EconomyManager.Instance.AddBonusMoney(
            75,
            EconomyTransactionCategory.TutorialReward
        );
        DailyFinancialReportData current =
            DailyFinancialReportManager.Instance.CurrentReport;
        int revenueBeforeDayEnd = current.Revenue;
        Require(current.Bonuses == 125 &&
                current.Revenue == revenueBeforeDayEnd,
            "Tutorial or daily-goal bonuses leaked into revenue.");

        int expectedElectricity = 0;
        foreach (PC item in UnityEngine.Object.FindObjectsByType<PC>())
            if (item.HasRoomAccess)
                expectedElectricity += item.DailyElectricityCost;
        expectedElectricity = Mathf.RoundToInt(
            expectedElectricity *
            ClubRandomEventManager.Instance.GetElectricityCostMultiplier() *
            ClubResearchManager.Instance.GetElectricityCostMultiplier()
        );
        int expectedInternet = InternetProviderManager.Instance.GetDailyCost();
        int telemetryCount = GameplayTelemetryManager.Instance.CompletedDays.Count;
        qa.CompleteCurrentDay();

        DailyFinancialReportData report =
            DailyFinancialReportManager.Instance.LastReport;
        Require(report.fixedOperatingExpenses == 200,
            "Fixed operating cost was not charged exactly once.");
        Require(report.electricityExpenses == expectedElectricity,
            "Electricity was duplicated or calculated from a second source.");
        Require(report.internetSubscriptionExpenses == expectedInternet,
            "Internet subscription was not charged exactly once.");
        Require(report.staffSalaryExpenses == 0,
            "Unhired staff generated a salary expense.");
        Require(GameplayTelemetryManager.Instance.CompletedDays.Count ==
                telemetryCount + 1,
            "Day completion did not create telemetry.");

        GameplayDayTelemetry telemetry =
            GameplayTelemetryManager.Instance.CompletedDays[^1];
        Require(telemetry.revenue == report.Revenue &&
                telemetry.bonuses == report.Bonuses &&
                telemetry.expenses == report.TotalExpenses &&
                telemetry.netResult == report.NetCashChange,
            "Telemetry does not match the existing financial report.");

        string diagnostics = Path.Combine(
            Application.persistentDataPath,
            "Diagnostics"
        );
        int exportCountBefore = Directory.Exists(diagnostics)
            ? Directory.GetFiles(diagnostics, "balance_*.json").Length
            : 0;
        qa.ExportTelemetry();
        int exportCountAfter = Directory.GetFiles(
            diagnostics,
            "balance_*.json"
        ).Length;
        Require(exportCountAfter == exportCountBefore + 1,
            "Telemetry export did not create a JSON file.");
    }

    private static void ValidateHUDInvariants()
    {
        ResetScenarioState();
        ClubHUDCanvas hud = ClubHUDCanvas.Instance;
        Require(hud != null, "ClubHUDCanvas singleton is missing.");
        Require(UnityEngine.Object.FindObjectsByType<ClubHUDCanvas>().Length == 1,
            "Runtime setup created duplicate HUD canvases.");

        Transform root = hud.transform.Find("GameplayHUDRoot");
        Transform compact = root?.Find("CompactSection");
        Transform warning = root?.Find("WarningSection");
        Transform expanded = root?.Find("ExpandedSection");
        Transform prompt = hud.transform.Find("InteractionPrompt");
        Transform feedback = hud.transform.Find("ClientFeedbackPanel");
        Require(root != null && compact != null && warning != null &&
                expanded != null && prompt != null && feedback != null,
            "HUD runtime hierarchy is incomplete.");
        Require(!prompt.IsChildOf(root) && !feedback.IsChildOf(root),
            "Interaction prompt or feedback was placed inside GameplayHUDRoot.");

        hud.SetMode(ClubHUDMode.Compact);
        Require(compact.gameObject.activeSelf && !expanded.gameObject.activeSelf,
            "Compact mode visibility is incorrect.");
        hud.ToggleHUDMode();
        Require(hud.CurrentMode == ClubHUDMode.Expanded &&
                compact.gameObject.activeSelf && expanded.gameObject.activeSelf,
            "First HUD toggle did not open Expanded mode.");
        Canvas.ForceUpdateCanvases();
        RectTransform rootRect = (RectTransform)root;
        LayoutRebuilder.ForceRebuildLayoutImmediate(rootRect);
        Require(rootRect.rect.width <= 1920f * 0.3f + 0.1f,
            "HUD exceeds 30 percent of the reference width.");
        Require(rootRect.rect.height <= 1040f,
            "Expanded HUD exceeds the safe vertical area.");

        hud.ToggleHUDMode();
        Require(hud.CurrentMode == ClubHUDMode.Hidden &&
                !compact.gameObject.activeSelf && !expanded.gameObject.activeSelf,
            "Second HUD toggle did not hide gameplay information.");
        Require(prompt.gameObject.activeSelf ==
                !string.IsNullOrWhiteSpace(
                    FindAnyPlayerInteraction()?.CurrentPrompt),
            "Hidden mode incorrectly controls the interaction prompt.");
        hud.ToggleHUDMode();
        Require(hud.CurrentMode == ClubHUDMode.Compact,
            "Third HUD toggle did not restore Compact mode.");

        hud.SetTemporarilyHidden(true);
        Require(!root.gameObject.activeSelf && prompt.gameObject.activeSelf ==
                !string.IsNullOrWhiteSpace(
                    FindAnyPlayerInteraction()?.CurrentPrompt),
            "Temporary hiding affected HUD-independent UI.");
        hud.SetTemporarilyHidden(false);
        Require(root.gameObject.activeSelf && hud.CurrentMode == ClubHUDMode.Compact,
            "Closing a panel did not restore the selected HUD mode.");

        Require(PricingPanel.Instance != null,
            "PricingPanel is missing for HUD visibility validation.");
        PricingPanel.Instance.Open();
        Invoke(hud, "Update");
        Require(PricingPanel.Instance.IsOpen && !root.gameObject.activeSelf,
            "An administrative panel did not hide GameplayHUDRoot.");
        PricingPanel.Instance.Close();
        Invoke(hud, "Update");
        Require(!PricingPanel.Instance.IsOpen && root.gameObject.activeSelf &&
                hud.CurrentMode == ClubHUDMode.Compact,
            "Closing an administrative panel did not restore the HUD.");

        hud.SetMode(ClubHUDMode.Expanded);
        GameSaveData data = (GameSaveData)Invoke(
            SaveManager.Instance,
            "CreateSaveData"
        );
        Require(data.hudMode == ClubHUDMode.Expanded,
            "Save data did not capture the selected HUD mode.");
        hud.SetMode(ClubHUDMode.Hidden);
        Invoke(SaveManager.Instance, "RestoreGame", data);
        Require(hud.CurrentMode == ClubHUDMode.Expanded,
            "Loading did not restore the saved HUD mode.");
        Invoke(SaveManager.Instance, "InitializeNewGameState");
        Require(hud.CurrentMode == ClubHUDMode.Compact,
            "A new game did not start in Compact mode.");

        ConsumableInventoryManager.Instance.RestoreState(0, 0, 0, 0, 0);
        ClubRandomEventManager.Instance.RestoreState(
            new ClubRandomEventState
            {
                eventType = ClubRandomEventType.InternetOutage,
                remainingSeconds = 30f
            },
            true
        );
        BankruptcyManager.Instance.RestoreState(1);
        hud.SetMode(ClubHUDMode.Compact);
        Require(hud.ActiveWarnings.Count >= 3 &&
                hud.ActiveWarnings[0].Type == HUDWarningType.BankruptcyRisk,
            "Bankruptcy warning does not have maximum priority.");

        BankruptcyManager.Instance.RestoreState(0);
        hud.SetMode(ClubHUDMode.Compact);
        Require(hud.ActiveWarnings.Count >= 2 &&
                hud.ActiveWarnings[0].Type == HUDWarningType.InternetOutage,
            "Internet outage did not override lower-priority warnings.");

        ClubRandomEventManager.Instance.RestoreState(null, false);
        ConsumableInventoryManager.Instance.RestoreState(5, 5, 0, 0, 0);
        ClientSpawner spawner = UnityEngine.Object.FindAnyObjectByType<ClientSpawner>();
        Require(spawner != null, "ClientSpawner is missing for HUD warning tests.");
        List<Client> waitingClients = GetField<List<Client>>(
            spawner,
            "waitingClients"
        );
        int originalMaxQueueSize = spawner.MaxQueueSize;
        waitingClients.Add(null);
        SetField(spawner, "maxQueueSize", waitingClients.Count);
        hud.SetMode(ClubHUDMode.Compact);
        Require(hud.ActiveWarnings.Count > 0 &&
                hud.ActiveWarnings[0].Type == HUDWarningType.QueueFull,
            "A full client queue did not create a HUD warning.");
        waitingClients.RemoveAt(waitingClients.Count - 1);
        SetField(spawner, "maxQueueSize", originalMaxQueueSize);
        hud.SetMode(ClubHUDMode.Compact);
        Require(!ContainsWarning(hud, HUDWarningType.QueueFull),
            "Queue warning remained after the problem was removed.");

        ResetScenarioState();
        hud.SetMode(ClubHUDMode.Compact);
    }

    private static PlayerInteraction FindAnyPlayerInteraction()
    {
        return UnityEngine.Object.FindAnyObjectByType<PlayerInteraction>();
    }

    private static bool ContainsWarning(ClubHUDCanvas hud, HUDWarningType type)
    {
        foreach (HUDWarningData warning in hud.ActiveWarnings)
        {
            if (warning.Type == type)
            {
                return true;
            }
        }

        return false;
    }

    private static void ValidateSaveCompatibility()
    {
        GameSaveData template = (GameSaveData)Invoke(
            SaveManager.Instance,
            "CreateSaveData"
        );
        template.money = 4850;
        template.currentDay = 6;
        template.clubLevel = 3;
        template.reputation = 67;
        template.timeRemaining = 60f;
        template.activeGoalDay = 6;
        template.dailyGoalType = 0;
        template.dailyGoalTarget = int.MaxValue;
        template.dailyGoalReward = 400;
        template.dailyGoalServedBaseline = template.servedClients;
        template.dailyGoalIncomeBaseline = template.totalIncome;
        template.dailyGoalCompleted = true;
        template.tutorialStarted = true;
        template.tutorialCompleted = true;
        template.activeInternetPlan = InternetPlanType.Gaming;
        template.clubResearch = new[]
        {
            new ClubResearchSaveData
            {
                researchType = ClubResearchType.ReliableComponents,
                level = 1
            }
        };

        for (int version = 14; version <= 19; version++)
        {
            GameSaveData data = Clone(template);
            data.version = version;
            Invoke(SaveManager.Instance, "RestoreGame", data);
            Require(EconomyManager.Instance.Money == 4850 &&
                    GameDayManager.Instance.CurrentDay == 6 &&
                    ClubProgressionManager.Instance.Level == 3,
                $"Save v{version} did not restore core state: " +
                $"money={EconomyManager.Instance.Money}, " +
                $"day={GameDayManager.Instance.CurrentDay}, " +
                $"level={ClubProgressionManager.Instance.Level}.");
            if (version >= 16)
                Require(InternetProviderManager.Instance.ActivePlan ==
                        InternetPlanType.Gaming,
                    $"Save v{version} did not restore the internet plan.");
            if (version >= 17)
                Require(ClubResearchManager.Instance.GetLevel(
                        ClubResearchType.ReliableComponents) == 1,
                    $"Save v{version} did not restore research.");
            if (version >= 18)
                Require(FirstDayTutorialManager.Instance.IsTutorialCompleted,
                    $"Save v{version} did not restore tutorial state.");
        }

        GameSaveData sparse = new GameSaveData
        {
            version = 14,
            money = 1000,
            reputation = 50,
            currentDay = 2,
            timeRemaining = 30f,
            clubLevel = 1,
            pcs = new List<PCSaveData>
            {
                new PCSaveData { objectName = "PC_DOES_NOT_EXIST", tier = 2 }
            },
            roomDoors = null,
            pcEquipment = null,
            trashItems = null,
            clubResearch = null
        };
        Invoke(SaveManager.Instance, "RestoreGame", sparse);
        Require(EconomyManager.Instance.Money == 1000,
            "Sparse legacy save did not load safely.");

        File.WriteAllText(SavePath, string.Empty);
        Require(!SaveManager.TryReadSaveSummary().isValid,
            "An empty save was accepted.");
        File.WriteAllText(SavePath, "{ invalid json");
        Require(!SaveManager.TryReadSaveSummary().isValid,
            "Corrupt JSON was accepted.");
        File.WriteAllText(
            SavePath,
            JsonUtility.ToJson(new GameSaveData { version = 999 }, true)
        );
        Require(!SaveManager.TryReadSaveSummary().isValid,
            "A future save version was accepted.");

        GameSaveData v19 = Clone(template);
        v19.version = 19;
        v19.savedAtUtc = DateTime.UtcNow.ToString("O");
        v19.savedDay = 6;
        v19.savedBalance = 4850;
        v19.savedClubLevel = 3;
        v19.savedReputation = 67;
        File.WriteAllText(SavePath, JsonUtility.ToJson(v19, true));
        GameSaveSummary summary = SaveManager.TryReadSaveSummary();
        Require(summary.isValid && summary.day == 6 &&
                summary.balance == 4850 && summary.clubLevel == 3 &&
                summary.reputation == 67,
            "Save v19 metadata summary is incorrect.");
    }

    private static void RunBalanceScenarios()
    {
        BalanceValidationExport export = new BalanceValidationExport
        {
            generatedAtUtc = DateTime.UtcNow.ToString("O")
        };
        export.scenarios.Add(RunScenario("A - cautious beginner", ScenarioKind.Cautious));
        export.scenarios.Add(RunScenario("B - aggressive expansion", ScenarioKind.Aggressive));
        export.scenarios.Add(RunScenario("C - poor management", ScenarioKind.Poor));

        BalanceScenarioResult cautious = export.scenarios[0];
        Require(cautious.endingBalances[0] >= 1200 &&
                cautious.endingBalances[0] <= 2000,
            "Cautious day-one balance is outside 1200-2000 RUB.");
        Require(cautious.endingBalances[4] >= 2000 &&
                cautious.endingBalances[4] <= 6000,
            "Cautious five-day balance is outside 2000-6000 RUB.");
        Require(cautious.finalLevel >= 2 && cautious.finalLevel <= 3,
            "Cautious progression is outside level 2-3.");
        Require(cautious.researchLevels >= 1,
            "Cautious scenario did not reach its first research.");

        BalanceScenarioResult aggressive = export.scenarios[1];
        Require(aggressive.purchasedPCCount >= 1,
            "Aggressive scenario could not buy PC_06.");
        Require(aggressive.endingBalances[1] < aggressive.endingBalances[0],
            "Aggressive investment had no short-term cash impact.");
        Require(aggressive.endingBalances[4] < 7000,
            "Aggressive scenario bought progression too quickly.");

        BalanceScenarioResult poor = export.scenarios[2];
        Require(poor.lostClients[4] > 0 && poor.finalReputation < 50,
            "Poor management did not create visible demand consequences.");
        Require(poor.endingBalances[0] > -500,
            "One poor day caused immediate bankruptcy.");
        Require(poor.bankruptcyRisk,
            "Poor management did not create a bankruptcy risk.");

        string directory = Path.Combine(
            Application.persistentDataPath,
            "Diagnostics"
        );
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "prerelease_balance_validation.json");
        File.WriteAllText(path, JsonUtility.ToJson(export, true));
        Debug.Log($"[BALANCE] Prerelease scenario report: {path}");
    }

    private static BalanceScenarioResult RunScenario(
        string name,
        ScenarioKind kind)
    {
        ResetScenarioState();
        UnityEngine.Random.InitState(12345 + (int)kind);
        BalanceScenarioResult result = new BalanceScenarioResult
        {
            scenario = name
        };

        int servedBaseline = 0;
        int lostBaseline = 0;
        for (int day = 1; day <= 5; day++)
        {
            ClubRandomEventManager.Instance.RestoreState(null, true);
            DailyGoalManager.Instance.RestoreState(
                day, 0, int.MaxValue, 1,
                ClubReputationManager.Instance.ServedClients,
                EconomyManager.Instance.TotalIncome,
                true
            );

            if (day == 1)
            {
                EconomyManager.Instance.AddBonusMoney(
                    500,
                    EconomyTransactionCategory.TutorialReward
                );
            }

            ApplyScenarioDecision(kind, day);
            int served = kind switch
            {
                ScenarioKind.Cautious => day switch
                {
                    1 => 5,
                    2 => 6,
                    3 => 8,
                    4 => 9,
                    _ => 10
                },
                ScenarioKind.Aggressive => day == 1 ? 5 : 8,
                _ => 3
            };
            int lost = kind == ScenarioKind.Poor ? 4 : 0;
            ClientSatisfaction satisfaction = kind == ScenarioKind.Poor
                ? ClientSatisfaction.Poor
                : day <= 2
                    ? ClientSatisfaction.Normal
                    : ClientSatisfaction.Excellent;

            for (int index = 0; index < served; index++)
            {
                ClientType type = day >= 3 && index % 3 == 1
                    ? ClientType.Gamer
                    : ClientType.Regular;
                CompleteControlledSession(type);
                ClubReputationManager.Instance.RegisterServedClient(
                    type,
                    satisfaction,
                    kind == ScenarioKind.Poor ? 20f : 2f,
                    kind == ScenarioKind.Poor ? 25f : 90f,
                    kind == ScenarioKind.Poor ? 35f : 90f
                );
            }
            for (int index = 0; index < lost; index++)
                ClubReputationManager.Instance.RegisterLostClient(
                    ClientType.Regular,
                    30f
                );

            GameDayManager.Instance.QACompleteCurrentDay();
            result.endingBalances[day - 1] = EconomyManager.Instance.Money;
            result.servedClients[day - 1] =
                ClubReputationManager.Instance.ServedClients - servedBaseline;
            result.lostClients[day - 1] =
                ClubReputationManager.Instance.LostClients - lostBaseline;
            servedBaseline = ClubReputationManager.Instance.ServedClients;
            lostBaseline = ClubReputationManager.Instance.LostClients;
        }

        result.finalLevel = ClubProgressionManager.Instance.Level;
        result.finalReputation = ClubReputationManager.Instance.Reputation;
        result.purchasedPCCount = PCExpansionManager.Instance.PurchasedPCCount;
        result.researchLevels = ClubResearchManager.Instance.TotalPurchasedLevels;
        result.technicianHired = TechnicianManager.Instance.TechnicianHired;
        result.cleanerHired = CleanerManager.Instance.CleanerHired;
        result.bankruptcyRisk = EconomyManager.Instance.Money <= 500 ||
            BankruptcyManager.Instance.ConsecutiveDebtDays > 0;
        return result;
    }

    private static void ApplyScenarioDecision(ScenarioKind kind, int day)
    {
        switch (kind)
        {
            case ScenarioKind.Cautious:
                PricingManager.Instance.RestoreState(110, 110, 110);
                if (day == 2) Invoke(FindPC("PC_01"), "TryUpgrade");
                if (day == 3)
                {
                    ClubResearchManager.Instance.TryPurchaseResearch(
                        ClubResearchType.EnergyEfficiency
                    );
                }
                break;

            case ScenarioKind.Aggressive:
                PricingManager.Instance.RestoreState(120, 130, 140);
                if (day == 2)
                {
                    Require(PCExpansionManager.Instance.TryPurchaseNextPC(),
                        "Aggressive scenario could not purchase PC_06.");
                    Invoke(FindPC("PC_01"), "TryUpgrade");
                    MarketingManager.Instance.TryStartCampaign(
                        MarketingCampaignType.SocialMedia
                    );
                }
                break;

            case ScenarioKind.Poor:
                PricingManager.Instance.RestoreState(160, 160, 160);
                if (day == 1)
                {
                    CleanerManager.Instance.TryHireCleaner();
                    ConsumableInventoryManager.Instance.TryRestock(
                        ConsumableType.EnergyDrink
                    );
                    ConsumableInventoryManager.Instance.TryRestock(
                        ConsumableType.Snack
                    );
                }
                if (day >= 2)
                    PrereleaseQAPanel.Instance.CreateTrash();
                break;
        }
    }

    private static void CompleteControlledSession(ClientType clientType)
    {
        PC selected = null;
        foreach (PC pc in UnityEngine.Object.FindObjectsByType<PC>())
        {
            if (pc != null && pc.IsAvailable && pc.HasRoomAccess &&
                !pc.HasBrokenEquipment)
            {
                selected = pc;
                break;
            }
        }
        Require(selected != null, "No PC was available for a controlled session.");
        int moneyBefore = EconomyManager.Instance.Money;
        Require(selected.TryReserve() && selected.TryOccupyReserved(clientType),
            "Controlled client could not occupy its reserved PC.");
        Require(EconomyManager.Instance.Money ==
                moneyBefore + selected.LastSessionIncome,
            $"Controlled session credited an unexpected amount: " +
            $"pc={selected.name}, before={moneyBefore}, " +
            $"after={EconomyManager.Instance.Money}, " +
            $"session={selected.LastSessionIncome}.");
        Invoke(selected, "CompleteSession");
    }

    private static void ResetScenarioState()
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
        BankruptcyManager.Instance.RestoreState(0);
        SetField(BankruptcyManager.Instance, "isGameOver", false);
        SetField(BankruptcyManager.Instance, "gameOverDay", 0);
        SetField(BankruptcyManager.Instance, "finalBalance", 0);
        FirstDayTutorialManager.Instance.RestoreState(true, true, 0, 0, false);
        DailyFinancialReportManager.Instance.RestoreState(null, null, 1);
        DemandAnalyticsManager.Instance.RestoreState(null, null, 1);
        GameDayManager.Instance.RestoreState(1, 120f, 0, 0);

        foreach (RoomDoor door in UnityEngine.Object.FindObjectsByType<RoomDoor>())
            door.RestoreState(false);
        foreach (PC pc in UnityEngine.Object.FindObjectsByType<PC>())
        {
            pc.RestoreTier(PCTier.Basic);
            pc.RestoreEquipmentCondition(100f, 100f, 100f);
            pc.SetState(PCState.Free);
            SetField(pc, "breakdownChance", 0f);
        }
    }

    private static PC FindPC(string objectName)
    {
        foreach (PC pc in UnityEngine.Object.FindObjectsByType<PC>())
            if (pc != null && pc.name == objectName) return pc;
        throw new InvalidOperationException($"{objectName} was not found.");
    }

    private static float[] CaptureRandomSequence()
    {
        float[] values = new float[8];
        for (int index = 0; index < values.Length; index++)
            values[index] = UnityEngine.Random.value;
        return values;
    }

    private static GameSaveData Clone(GameSaveData data)
    {
        return JsonUtility.FromJson<GameSaveData>(
            JsonUtility.ToJson(data)
        );
    }

    private static object Invoke(object target, string methodName, params object[] args)
    {
        MethodInfo method = target.GetType().GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
        );
        if (method == null)
            throw new MissingMethodException(target.GetType().Name, methodName);
        try
        {
            return method.Invoke(target, args);
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

    private static T GetField<T>(object target, string fieldName)
    {
        FieldInfo field = target.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic
        );
        if (field == null)
            throw new MissingFieldException(target.GetType().Name, fieldName);
        return (T)field.GetValue(target);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void BackupSave()
    {
        if (File.Exists(SaveBackupPath)) File.Delete(SaveBackupPath);
        if (File.Exists(SavePath)) File.Copy(SavePath, SaveBackupPath, true);
        if (File.Exists(SettingsBackupPath)) File.Delete(SettingsBackupPath);
        if (File.Exists(SettingsPath))
            File.Copy(SettingsPath, SettingsBackupPath, true);
    }

    private static void RestoreSave()
    {
        if (File.Exists(SavePath)) File.Delete(SavePath);
        if (File.Exists(SaveBackupPath))
        {
            File.Copy(SaveBackupPath, SavePath, true);
            File.Delete(SaveBackupPath);
        }

        if (File.Exists(SettingsPath)) File.Delete(SettingsPath);
        if (File.Exists(SettingsBackupPath))
        {
            File.Copy(SettingsBackupPath, SettingsPath, true);
            File.Delete(SettingsBackupPath);
        }
    }

    private static void Fail(Exception exception)
    {
        testFailed = true;
        Debug.LogException(exception);
        Debug.LogError("PRERELEASE_VALIDATION_SMOKE_TEST: FAIL");
        if (EditorApplication.isPlaying)
        {
            EditorApplication.isPlaying = false;
            return;
        }

        RestoreSave();
        EditorPrefs.DeleteKey(PendingKey);
        EditorApplication.Exit(1);
    }
}
