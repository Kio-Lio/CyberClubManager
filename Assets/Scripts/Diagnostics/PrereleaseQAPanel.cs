#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public sealed class PrereleaseQAPanel : MonoBehaviour
{
    public static PrereleaseQAPanel Instance { get; private set; }

    private Rect windowRect = new Rect(20f, 20f, 520f, 820f);
    private Vector2 scrollPosition;
    private bool isOpen;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private float previousTimeScale = 1f;
    private float resumeTimeScale = 1f;
    private string balanceText = "1200";
    private string clubLevelText = "1";
    private string randomSeedText = "12345";
#endif
    private PC[] pcs = Array.Empty<PC>();
    private int selectedPCIndex;
    private ClubRandomEventType[] eventTypes = Array.Empty<ClubRandomEventType>();
    private int selectedEventIndex;

    public bool IsOpen => isOpen;
    public bool IsTaintedByDebugActions { get; private set; }
    public PC SelectedPC => pcs.Length == 0
        ? null
        : pcs[Mathf.Clamp(selectedPCIndex, 0, pcs.Length - 1)];
    public ClubRandomEventType SelectedEvent => eventTypes.Length == 0
        ? ClubRandomEventType.None
        : eventTypes[Mathf.Clamp(selectedEventIndex, 0, eventTypes.Length - 1)];

    private void Awake()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        eventTypes = (ClubRandomEventType[])Enum.GetValues(
            typeof(ClubRandomEventType)
        );
#else
        enabled = false;
        Destroy(this);
#endif
    }

    private void Update()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (Keyboard.current != null &&
            Keyboard.current.f10Key.wasPressedThisFrame)
        {
            if (isOpen) ClosePanel();
            else OpenPanel();
        }

        if (isOpen && Time.timeScale != 0f)
        {
            Time.timeScale = 0f;
        }
#endif
    }

    private void OnDestroy()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (isOpen) Time.timeScale = resumeTimeScale;
        if (Instance == this) Instance = null;
#endif
    }

    public void OpenPanel()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (isOpen) return;
        RefreshPCs();
        previousTimeScale = Time.timeScale;
        resumeTimeScale = previousTimeScale <= 0f ? 1f : previousTimeScale;
        Time.timeScale = 0f;
        isOpen = true;
#endif
    }

    public void ClosePanel()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (!isOpen) return;
        isOpen = false;
        Time.timeScale = resumeTimeScale;
#endif
    }

    public void AddMoney(int amount = 5000)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (EconomyManager.Instance == null || amount <= 0) return;
        MarkTainted($"Добавлено {amount} ₽");
        EconomyManager.Instance.RestoreState(
            EconomyManager.Instance.Money + amount,
            EconomyManager.Instance.TotalIncome,
            EconomyManager.Instance.TotalExpenses
        );
#endif
    }

    public void SetBalance(int balance)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (EconomyManager.Instance == null) return;
        MarkTainted($"Баланс установлен: {balance} ₽");
        EconomyManager.Instance.RestoreState(
            balance,
            EconomyManager.Instance.TotalIncome,
            EconomyManager.Instance.TotalExpenses
        );
#endif
    }

    public void SetTimeMultiplier(float multiplier)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        resumeTimeScale = Mathf.Clamp(multiplier, 1f, 5f);
        MarkTainted($"Скорость времени после закрытия: x{resumeTimeScale:0}");
        if (!isOpen) Time.timeScale = resumeTimeScale;
#endif
    }

    public void CompleteCurrentDay()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (GameDayManager.Instance == null) return;
        MarkTainted("Текущий день завершен принудительно");
        GameDayManager.Instance.QACompleteCurrentDay();
#endif
    }

    public void SpawnClient(ClientType clientType)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        ClientSpawner spawner = FindAnyObjectByType<ClientSpawner>();
        if (spawner == null) return;
        MarkTainted($"Создан клиент: {clientType}");
        spawner.QASpawnClient(clientType);
#endif
    }

    public void CreateTrash()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        RefreshPCs();
        if (ClubCleanlinessManager.Instance == null || SelectedPC == null) return;
        MarkTainted($"Создан мусор возле {SelectedPC.name}");
        ClubCleanlinessManager.Instance.EnsureTutorialTrash(SelectedPC);
#endif
    }

    public void WearSelectedPCEquipment(float condition = 10f)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        RefreshPCs();
        PC pc = SelectedPC;
        if (pc == null) return;
        MarkTainted($"Периферия {pc.name} изношена до {condition:0}%");
        pc.SetEquipmentCondition(PCEquipmentType.Keyboard, condition);
        pc.SetEquipmentCondition(PCEquipmentType.Mouse, condition);
        pc.SetEquipmentCondition(PCEquipmentType.Chair, condition);
#endif
    }

    public void BreakRandomPC()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        List<PC> candidates = new List<PC>();
        foreach (PC pc in FindObjectsByType<PC>())
        {
            if (pc != null && pc.HasRoomAccess && pc.IsFree)
                candidates.Add(pc);
        }

        if (candidates.Count == 0) return;
        PC selected = candidates[UnityEngine.Random.Range(0, candidates.Count)];
        MarkTainted($"Сломан случайный ПК: {selected.name}");
        selected.SetState(PCState.Broken);
#endif
    }

    public void TriggerSelectedEvent()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (ClubRandomEventManager.Instance == null ||
            SelectedEvent == ClubRandomEventType.None) return;
        MarkTainted($"Запущено событие: {SelectedEvent}");
        ClubRandomEventManager.Instance.TriggerEvent(SelectedEvent);
        if (ClubRandomEventPanel.Instance != null &&
            ClubRandomEventPanel.Instance.IsOpen)
            ClubRandomEventPanel.Instance.Close();
#endif
    }

    public void SetClubLevel(int level)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (ClubProgressionManager.Instance == null) return;
        int clamped = Mathf.Clamp(level, 1, ClubProgressionManager.Instance.MaxLevel);
        MarkTainted($"Уровень клуба установлен: {clamped}");
        ClubProgressionManager.Instance.RestoreState(
            clamped,
            ClubProgressionManager.Instance.Experience
        );
#endif
    }

    public void ApplyRandomSeed(int seed)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        MarkTainted($"Random seed установлен: {seed}");
        UnityEngine.Random.InitState(seed);
#endif
    }

    public void ExportTelemetry()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        GameplayTelemetryManager.Instance?.ExportTelemetry();
        Debug.Log("[QA] Запрошен экспорт телеметрии");
#endif
    }

    private void MarkTainted(string message)
    {
        IsTaintedByDebugActions = true;
        Debug.Log($"[QA] {message}");
    }

    private void RefreshPCs()
    {
        string selectedName = SelectedPC != null ? SelectedPC.name : string.Empty;
        pcs = FindObjectsByType<PC>();
        Array.Sort(pcs, (left, right) => string.CompareOrdinal(left.name, right.name));
        selectedPCIndex = Mathf.Clamp(selectedPCIndex, 0, Mathf.Max(0, pcs.Length - 1));
        if (!string.IsNullOrEmpty(selectedName))
        {
            int index = Array.FindIndex(pcs, pc => pc.name == selectedName);
            if (index >= 0) selectedPCIndex = index;
        }
    }

    private void OnGUI()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (!isOpen) return;
        windowRect = GUI.Window(99241, windowRect, DrawWindow, "ПРЕДРЕЛИЗНАЯ QA");
#endif
    }

    private void DrawWindow(int windowId)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        scrollPosition = GUILayout.BeginScrollView(scrollPosition);
        GUILayout.Label(IsTaintedByDebugActions
            ? "СЕССИЯ ПОМЕЧЕНА: СОХРАНЕНИЕ ОТКЛЮЧЕНО"
            : "До первого QA-действия сохранение доступно");

        if (GUILayout.Button("Добавить 5 000 ₽")) AddMoney();
        GUILayout.BeginHorizontal();
        balanceText = GUILayout.TextField(balanceText);
        if (GUILayout.Button("Установить баланс") && int.TryParse(balanceText, out int balance))
            SetBalance(balance);
        GUILayout.EndHorizontal();

        GUILayout.Label("Скорость после закрытия панели");
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("x1")) SetTimeMultiplier(1f);
        if (GUILayout.Button("x2")) SetTimeMultiplier(2f);
        if (GUILayout.Button("x5")) SetTimeMultiplier(5f);
        GUILayout.EndHorizontal();
        if (GUILayout.Button("Завершить текущий день")) CompleteCurrentDay();

        GUILayout.Label("Клиенты");
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Regular")) SpawnClient(ClientType.Regular);
        if (GUILayout.Button("Gamer")) SpawnClient(ClientType.Gamer);
        if (GUILayout.Button("VIP")) SpawnClient(ClientType.VIP);
        GUILayout.EndHorizontal();

        RefreshPCs();
        GUILayout.Label(SelectedPC != null ? $"Выбран: {SelectedPC.name}" : "ПК не найдены");
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("< ПК") && pcs.Length > 0)
            selectedPCIndex = (selectedPCIndex - 1 + pcs.Length) % pcs.Length;
        if (GUILayout.Button("ПК >") && pcs.Length > 0)
            selectedPCIndex = (selectedPCIndex + 1) % pcs.Length;
        GUILayout.EndHorizontal();
        if (GUILayout.Button("Создать мусор")) CreateTrash();
        if (GUILayout.Button("Износить периферию выбранного ПК")) WearSelectedPCEquipment();
        if (GUILayout.Button("Сломать случайный ПК")) BreakRandomPC();

        GUILayout.Label($"Событие: {SelectedEvent}");
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("< Событие"))
            selectedEventIndex = (selectedEventIndex - 1 + eventTypes.Length) % eventTypes.Length;
        if (GUILayout.Button("Событие >"))
            selectedEventIndex = (selectedEventIndex + 1) % eventTypes.Length;
        GUILayout.EndHorizontal();
        if (GUILayout.Button("Запустить выбранное событие")) TriggerSelectedEvent();

        GUILayout.BeginHorizontal();
        clubLevelText = GUILayout.TextField(clubLevelText);
        if (GUILayout.Button("Установить Club Level") && int.TryParse(clubLevelText, out int level))
            SetClubLevel(level);
        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
        randomSeedText = GUILayout.TextField(randomSeedText);
        if (GUILayout.Button("Применить Random Seed") && int.TryParse(randomSeedText, out int seed))
            ApplyRandomSeed(seed);
        GUILayout.EndHorizontal();

        if (GUILayout.Button("Экспортировать телеметрию")) ExportTelemetry();
        if (GUILayout.Button("ЗАКРЫТЬ F10")) ClosePanel();
        GUILayout.EndScrollView();
        GUI.DragWindow(new Rect(0f, 0f, 520f, 28f));
#endif
    }
}
#endif
