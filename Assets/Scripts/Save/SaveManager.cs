using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SaveManager : MonoBehaviour
{
    public static SaveManager Instance { get; private set; }

    private const int CurrentSaveVersion = 10;
    private const string SaveFileName = "cyber_club_save.json";

    private bool suppressSaving;
    private Coroutine pendingSaveCoroutine;

    private string SavePath =>
        Path.Combine(Application.persistentDataPath, SaveFileName);

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
        LoadGame();

        if (GameDayManager.Instance != null)
        {
            GameDayManager.Instance.DayEnded += OnDayEnded;
        }

        if (BankruptcyManager.Instance != null)
        {
            BankruptcyManager.Instance.GameOverTriggered += OnGameOverTriggered;
        }
    }

    private void OnDestroy()
    {
        if (GameDayManager.Instance != null)
        {
            GameDayManager.Instance.DayEnded -= OnDayEnded;
        }

        if (BankruptcyManager.Instance != null)
        {
            BankruptcyManager.Instance.GameOverTriggered -= OnGameOverTriggered;
        }

        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void OnApplicationQuit()
    {
        SaveGame();
    }

    private void OnApplicationPause(bool isPaused)
    {
        if (isPaused)
        {
            SaveGame();
        }
    }

    private void OnDayEnded(int completedDay, int income, int expenses, int profit)
    {
        if (pendingSaveCoroutine != null)
        {
            StopCoroutine(pendingSaveCoroutine);
        }

        pendingSaveCoroutine = StartCoroutine(SaveAfterDayEnd());
    }

    private IEnumerator SaveAfterDayEnd()
    {
        // Let every DayEnded handler, including bankruptcy tracking, finish first.
        yield return null;

        pendingSaveCoroutine = null;
        SaveGame();
    }

    private void OnGameOverTriggered()
    {
        suppressSaving = true;
        DeleteSave();

        Debug.Log(
            "Сохранение удалено после банкротства. " +
            "Следующий запуск начнется с новой игры."
        );
    }

    [ContextMenu("Save Game")]
    public void SaveGame()
    {
        TrySaveGame();
    }

    public bool TrySaveGame()
    {
        if (suppressSaving)
        {
            Debug.LogWarning(
                "Сохранение заблокировано после завершения игры."
            );
            return false;
        }

        if (!CanSave())
        {
            Debug.LogWarning(
                "Сохранение невозможно: не все игровые менеджеры доступны."
            );
            return false;
        }

        GameSaveData data = CreateSaveData();

        try
        {
            string json = JsonUtility.ToJson(data, true);
            File.WriteAllText(SavePath, json);
            Debug.Log($"Игра сохранена: {SavePath}");
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogError($"Ошибка сохранения игры: {exception.Message}");
            return false;
        }
    }

    public void StartNewGame()
    {
        Scene activeScene = SceneManager.GetActiveScene();

        if (activeScene.buildIndex < 0)
        {
            Debug.LogError(
                "Невозможно начать новую игру: " +
                "текущая сцена не добавлена в Build Settings."
            );
            return;
        }

        suppressSaving = true;
        DeleteSave();

        Time.timeScale = 1f;

        SceneManager.LoadScene(
            activeScene.buildIndex,
            LoadSceneMode.Single
        );
    }

    private bool CanSave()
    {
        return EconomyManager.Instance != null &&
               ClubReputationManager.Instance != null &&
               GameDayManager.Instance != null &&
               BankruptcyManager.Instance != null &&
               PCExpansionManager.Instance != null &&
               DailyGoalManager.Instance != null &&
               ClubProgressionManager.Instance != null &&
               TechnicianManager.Instance != null &&
               ClubCleanlinessManager.Instance != null &&
               CleanerManager.Instance != null &&
               PricingManager.Instance != null;
    }

    private GameSaveData CreateSaveData()
    {
        EconomyManager economy = EconomyManager.Instance;
        ClubReputationManager reputation = ClubReputationManager.Instance;
        GameDayManager gameDay = GameDayManager.Instance;
        BankruptcyManager bankruptcy = BankruptcyManager.Instance;
        PCExpansionManager expansion = PCExpansionManager.Instance;
        DailyGoalManager dailyGoal = DailyGoalManager.Instance;
        ClubProgressionManager progression = ClubProgressionManager.Instance;

        GameSaveData data = new GameSaveData
        {
            version = CurrentSaveVersion,
            money = economy.Money,
            totalIncome = economy.TotalIncome,
            totalExpenses = economy.TotalExpenses,
            reputation = reputation.Reputation,
            servedClients = reputation.ServedClients,
            lostClients = reputation.LostClients,
            excellentClients = reputation.ExcellentClients,
            normalClients = reputation.NormalClients,
            poorClients = reputation.PoorClients,
            currentDay = gameDay.CurrentDay,
            timeRemaining = gameDay.TimeRemaining,
            incomeAtDayStart = gameDay.IncomeAtDayStart,
            expensesAtDayStart = gameDay.ExpensesAtDayStart,
            activeGoalDay = dailyGoal.ActiveGoalDay,
            dailyGoalType = (int)dailyGoal.GoalType,
            dailyGoalTarget = dailyGoal.TargetValue,
            dailyGoalReward = dailyGoal.RewardMoney,
            dailyGoalServedBaseline = dailyGoal.ServedClientsBaseline,
            dailyGoalIncomeBaseline = dailyGoal.IncomeBaseline,
            dailyGoalCompleted = dailyGoal.GoalCompleted,
            clubLevel = progression.Level,
            clubExperience = progression.Experience,
            consecutiveDebtDays = bankruptcy.ConsecutiveDebtDays,
            purchasedPCCount = expansion.PurchasedPCCount,
            technicianHired = TechnicianManager.Instance.TechnicianHired,
            cleanerHired = CleanerManager.Instance.CleanerHired,
            trashItems = ClubCleanlinessManager.Instance.CreateSaveData(),
            basicPricePercent = PricingManager.Instance.GetPricePercent(PCTier.Basic),
            gamingPricePercent = PricingManager.Instance.GetPricePercent(PCTier.Gaming),
            premiumPricePercent = PricingManager.Instance.GetPricePercent(PCTier.Premium)
        };

        RoomDoor[] roomDoors = FindObjectsByType<RoomDoor>();
        data.roomDoors = new RoomDoorSaveData[roomDoors.Length];

        for (int index = 0; index < roomDoors.Length; index++)
        {
            data.roomDoors[index] = new RoomDoorSaveData
            {
                doorId = roomDoors[index].DoorId,
                isUnlocked = roomDoors[index].IsUnlocked
            };
        }

        PC[] pcs = FindObjectsByType<PC>();
        data.pcEquipment = new PCEquipmentSaveData[pcs.Length];

        for (int index = 0; index < pcs.Length; index++)
        {
            PC pc = pcs[index];
            if (pc == null)
            {
                continue;
            }

            data.pcEquipment[index] = new PCEquipmentSaveData
            {
                pcName = pc.name,
                keyboardCondition = pc.Keyboard.Condition,
                mouseCondition = pc.Mouse.Condition,
                chairCondition = pc.Chair.Condition
            };

            data.pcs.Add(
                new PCSaveData
                {
                    objectName = pc.name,
                    tier = (int)pc.Tier
                }
            );
        }

        return data;
    }

    private void LoadGame()
    {
        if (!File.Exists(SavePath))
        {
            Debug.Log("Сохранение не найдено. Начата новая игра.");
            return;
        }

        try
        {
            string json = File.ReadAllText(SavePath);
            GameSaveData data = JsonUtility.FromJson<GameSaveData>(json);

            if (data == null)
            {
                Debug.LogError("Файл сохранения не содержит данных.");
                return;
            }

            if (data.version > CurrentSaveVersion)
            {
                Debug.LogError(
                    "Сохранение создано более новой версией игры и не может быть загружено."
                );
                return;
            }

            RestoreGame(data);
            Debug.Log($"Игра загружена: {SavePath}");
        }
        catch (Exception exception)
        {
            Debug.LogError($"Ошибка загрузки игры: {exception.Message}");
        }
    }

    private void RestoreGame(GameSaveData data)
    {
        if (!CanSave())
        {
            Debug.LogWarning(
                "Загрузка невозможна: не все игровые менеджеры доступны."
            );
            return;
        }

        EconomyManager.Instance.RestoreState(
            data.money,
            data.totalIncome,
            data.totalExpenses
        );

        ClubReputationManager.Instance.RestoreState(
            data.reputation,
            data.servedClients,
            data.lostClients,
            data.excellentClients,
            data.normalClients,
            data.poorClients
        );

        GameDayManager.Instance.RestoreState(
            data.currentDay,
            data.timeRemaining,
            data.incomeAtDayStart,
            data.expensesAtDayStart
        );

        DailyGoalManager.Instance.RestoreState(
            data.activeGoalDay,
            data.dailyGoalType,
            data.dailyGoalTarget,
            data.dailyGoalReward,
            data.dailyGoalServedBaseline,
            data.dailyGoalIncomeBaseline,
            data.dailyGoalCompleted
        );

        BankruptcyManager.Instance.RestoreState(data.consecutiveDebtDays);

        ClubProgressionManager.Instance.RestoreState(
            data.clubLevel <= 0 ? 1 : data.clubLevel,
            data.clubExperience
        );

        TechnicianManager.Instance.RestoreState(data.technicianHired);
        PricingManager.Instance.RestoreState(
            data.basicPricePercent,
            data.gamingPricePercent,
            data.premiumPricePercent
        );

        RestoreRoomDoors(data);
        PCExpansionManager.Instance.RestorePurchasedPCs(data.purchasedPCCount);
        RestorePCTiers(data);
        RestorePCEquipment(data);
        ClubCleanlinessManager.Instance.RestoreState(data.trashItems);
        CleanerManager.Instance.RestoreState(data.cleanerHired);
    }

    private static void RestoreRoomDoors(GameSaveData data)
    {
        if (data.roomDoors == null)
        {
            return;
        }

        RoomUnlockManager manager = RoomUnlockManager.Instance;

        foreach (RoomDoorSaveData savedDoor in data.roomDoors)
        {
            if (savedDoor == null ||
                string.IsNullOrWhiteSpace(savedDoor.doorId))
            {
                continue;
            }

            RoomDoor door = manager != null
                ? manager.FindDoor(savedDoor.doorId)
                : null;

            if (door != null)
            {
                door.RestoreState(savedDoor.isUnlocked);
            }
        }
    }

    private void RestorePCTiers(GameSaveData data)
    {
        PC[] existingPCs = FindObjectsByType<PC>();

        foreach (PC pc in existingPCs)
        {
            if (pc == null)
            {
                continue;
            }

            pc.CancelReservation();
            pc.SetState(PCState.Free);
        }

        if (data.pcs == null)
        {
            return;
        }

        foreach (PCSaveData pcData in data.pcs)
        {
            if (pcData == null || string.IsNullOrWhiteSpace(pcData.objectName))
            {
                continue;
            }

            PC targetPC = null;
            foreach (PC pc in existingPCs)
            {
                if (pc != null && pc.name == pcData.objectName)
                {
                    targetPC = pc;
                    break;
                }
            }

            if (targetPC == null)
            {
                Debug.LogWarning($"ПК из сохранения не найден: {pcData.objectName}.");
                continue;
            }

            int tierValue = Mathf.Clamp(
                pcData.tier,
                (int)PCTier.Basic,
                (int)PCTier.Premium
            );

            targetPC.RestoreTier((PCTier)tierValue);
        }
    }

    private static void RestorePCEquipment(GameSaveData data)
    {
        if (data.pcEquipment == null)
        {
            return;
        }

        foreach (PCEquipmentSaveData savedEquipment in data.pcEquipment)
        {
            if (savedEquipment == null ||
                string.IsNullOrWhiteSpace(savedEquipment.pcName))
            {
                continue;
            }

            GameObject pcObject = GameObject.Find(savedEquipment.pcName);
            PC pc = pcObject != null ? pcObject.GetComponent<PC>() : null;

            if (pc == null)
            {
                continue;
            }

            pc.RestoreEquipmentCondition(
                savedEquipment.keyboardCondition,
                savedEquipment.mouseCondition,
                savedEquipment.chairCondition
            );
        }
    }

    [ContextMenu("Delete Save")]
    public void DeleteSave()
    {
        try
        {
            if (!File.Exists(SavePath))
            {
                Debug.Log("Файл сохранения отсутствует.");
                return;
            }

            File.Delete(SavePath);
            Debug.Log("Файл сохранения удален.");
        }
        catch (Exception exception)
        {
            Debug.LogError($"Ошибка удаления сохранения: {exception.Message}");
        }
    }
}
