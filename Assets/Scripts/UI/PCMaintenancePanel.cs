using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

public sealed class PCMaintenancePanel : MonoBehaviour
{
    public static PCMaintenancePanel Instance { get; private set; }

    private readonly List<PC> pcs = new();

    private GameObject rootObject;
    private Text pcInformationText;
    private Text stateText;
    private Text balanceText;
    private Text technicianText;
    private Text cleanerText;
    private Text statusText;
    private Button previousButton;
    private Button nextButton;
    private Button keyboardButton;
    private Button mouseButton;
    private Button chairButton;
    private Button repairAllButton;
    private Button hireTechnicianButton;
    private Button hireCleanerButton;
    private Button closeButton;
    private int selectedIndex;
    private bool isOpen;
    private float previousTimeScale = 1f;
    private bool cursorStateCaptured;
    private bool previousCursorVisible;
    private CursorLockMode previousCursorLockMode;
    private Font runtimeFont;

    public bool IsOpen => isOpen;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
        BuildInterface();
        rootObject.SetActive(false);
    }

    private void Start()
    {
        PC.PCRegistered += RegisterPC;
        PC.PCUnregistered += UnregisterPC;

        if (EconomyManager.Instance != null)
        {
            EconomyManager.Instance.MoneyChanged += OnMoneyChanged;
        }

        if (RoomUnlockManager.Instance != null)
        {
            RoomUnlockManager.Instance.StatusChanged += RefreshView;
        }

        if (TechnicianManager.Instance != null)
        {
            TechnicianManager.Instance.StatusChanged += RefreshView;
        }

        if (CleanerManager.Instance != null)
        {
            CleanerManager.Instance.StatusChanged += RefreshView;
        }

        foreach (PC pc in FindObjectsByType<PC>())
        {
            RegisterPC(pc);
        }

        SortPCs();
    }

    private void OnDestroy()
    {
        PC.PCRegistered -= RegisterPC;
        PC.PCUnregistered -= UnregisterPC;

        if (EconomyManager.Instance != null)
        {
            EconomyManager.Instance.MoneyChanged -= OnMoneyChanged;
        }

        if (RoomUnlockManager.Instance != null)
        {
            RoomUnlockManager.Instance.StatusChanged -= RefreshView;
        }

        if (TechnicianManager.Instance != null)
        {
            TechnicianManager.Instance.StatusChanged -= RefreshView;
        }

        if (CleanerManager.Instance != null)
        {
            CleanerManager.Instance.StatusChanged -= RefreshView;
        }

        foreach (PC pc in pcs)
        {
            UnsubscribeFromPC(pc);
        }

        if (isOpen)
        {
            RestoreGameplayState();
        }

        if (Instance == this)
        {
            Instance = null;
        }
    }

    public void Open()
    {
        if (isOpen || (PauseMenuController.Instance != null &&
            PauseMenuController.Instance.IsMenuOpen))
        {
            return;
        }

        RefreshPCList();
        isOpen = true;
        previousTimeScale = Time.timeScale;
        Time.timeScale = 0f;
        CaptureCursorState();
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        EnsureEventSystem();
        rootObject.SetActive(true);
        rootObject.transform.SetAsLastSibling();
        statusText.text = string.Empty;
        RefreshView();
        StartCoroutine(SelectDefaultButtonNextFrame());
    }

    public void Close()
    {
        if (!isOpen)
        {
            return;
        }

        isOpen = false;
        rootObject.SetActive(false);

        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
        }

        RestoreGameplayState();
    }

    private void RegisterPC(PC pc)
    {
        if (pc == null || pcs.Contains(pc))
        {
            return;
        }

        pcs.Add(pc);
        pc.StateChanged += OnPCStateChanged;
        pc.TierChanged += OnPCTierChanged;
        pc.EquipmentChanged += RefreshView;
        SortPCs();
    }

    private void UnregisterPC(PC pc)
    {
        if (pc == null)
        {
            return;
        }

        UnsubscribeFromPC(pc);
        pcs.Remove(pc);
        selectedIndex = Mathf.Clamp(selectedIndex, 0, Mathf.Max(0, pcs.Count - 1));
        RefreshView();
    }

    private void UnsubscribeFromPC(PC pc)
    {
        if (pc == null)
        {
            return;
        }

        pc.StateChanged -= OnPCStateChanged;
        pc.TierChanged -= OnPCTierChanged;
        pc.EquipmentChanged -= RefreshView;
    }

    private void RefreshPCList()
    {
        pcs.RemoveAll(pc => pc == null);

        foreach (PC pc in FindObjectsByType<PC>())
        {
            RegisterPC(pc);
        }

        SortPCs();
        selectedIndex = Mathf.Clamp(selectedIndex, 0, Mathf.Max(0, pcs.Count - 1));
    }

    private void SortPCs()
    {
        pcs.Sort((left, right) => string.CompareOrdinal(left.name, right.name));
    }

    private void OnMoneyChanged(int _) => RefreshView();
    private void OnPCStateChanged(PCState _) => RefreshView();
    private void OnPCTierChanged(PCTier _) => RefreshView();

    private PC GetSelectedPC()
    {
        pcs.RemoveAll(pc => pc == null);

        if (pcs.Count == 0)
        {
            return null;
        }

        selectedIndex = Mathf.Clamp(selectedIndex, 0, pcs.Count - 1);
        return pcs[selectedIndex];
    }

    private void SelectPreviousPC()
    {
        if (pcs.Count == 0)
        {
            return;
        }

        selectedIndex = (selectedIndex - 1 + pcs.Count) % pcs.Count;
        statusText.text = string.Empty;
        RefreshView();
    }

    private void SelectNextPC()
    {
        if (pcs.Count == 0)
        {
            return;
        }

        selectedIndex = (selectedIndex + 1) % pcs.Count;
        statusText.text = string.Empty;
        RefreshView();
    }

    private void RepairKeyboard() => RepairEquipment(PCEquipmentType.Keyboard);
    private void RepairMouse() => RepairEquipment(PCEquipmentType.Mouse);
    private void RepairChair() => RepairEquipment(PCEquipmentType.Chair);

    private void RepairEquipment(PCEquipmentType equipmentType)
    {
        PC pc = GetSelectedPC();
        bool repaired = pc != null && pc.TryRepairEquipment(equipmentType);
        statusText.text = repaired
            ? "Оборудование отремонтировано."
            : GetFailureMessage(pc);
        RefreshView();
    }

    private void RepairAll()
    {
        PC pc = GetSelectedPC();
        int totalCost = pc != null ? pc.GetTotalEquipmentRepairCost() : 0;
        bool repaired = pc != null && pc.TryRepairAllEquipment();
        statusText.text = repaired
            ? $"Полный ремонт выполнен: {totalCost} ₽."
            : GetFailureMessage(pc);
        RefreshView();
    }

    private void HireTechnician()
    {
        TechnicianManager technicianManager = TechnicianManager.Instance;
        if (technicianManager == null)
        {
            statusText.text = "Менеджер техника не найден.";
            return;
        }

        bool hired = technicianManager.TryHireTechnician();
        statusText.text = hired
            ? "Техник успешно нанят."
            : technicianManager.LastServiceMessage;
        RefreshView();
    }

    private void HireCleaner()
    {
        CleanerManager cleanerManager = CleanerManager.Instance;
        if (cleanerManager == null)
        {
            statusText.text = "Менеджер уборщика не найден.";
            return;
        }

        bool hired = cleanerManager.TryHireCleaner();
        statusText.text = hired
            ? "Уборщик успешно нанят."
            : cleanerManager.LastWorkMessage;
        RefreshView();
    }

    private static string GetFailureMessage(PC pc)
    {
        if (pc == null)
        {
            return "Компьютер не найден.";
        }

        if (!pc.HasRoomAccess)
        {
            return "ПК находится в закрытой комнате.";
        }

        if (pc.IsOccupied)
        {
            return "Нельзя ремонтировать занятый ПК.";
        }

        if (pc.IsReserved)
        {
            return "ПК уже зарезервирован клиентом.";
        }

        if (pc.GetTotalEquipmentRepairCost() <= 0)
        {
            return "Оборудование полностью исправно.";
        }

        return "Недостаточно денег для ремонта.";
    }

    private void RefreshView()
    {
        if (rootObject == null)
        {
            return;
        }

        PC pc = GetSelectedPC();
        int balance = EconomyManager.Instance != null
            ? EconomyManager.Instance.Money
            : 0;
        balanceText.text = $"Баланс: {balance} ₽";
        RefreshTechnicianSection(balance);
        RefreshCleanerSection(balance);

        if (pc == null)
        {
            pcInformationText.text = "Компьютеры не найдены.";
            stateText.text = string.Empty;
            SetRepairButtons(false);
            return;
        }

        pcInformationText.text =
            $"{pc.name} ({selectedIndex + 1}/{pcs.Count})\n" +
            $"Класс: {pc.GetTierDisplayName()}";
        stateText.text = $"Состояние: {GetStateName(pc)}";

        RefreshEquipmentButton(keyboardButton, pc.Keyboard, "Клавиатура", pc, balance);
        RefreshEquipmentButton(mouseButton, pc.Mouse, "Мышь", pc, balance);
        RefreshEquipmentButton(chairButton, pc.Chair, "Кресло", pc, balance);

        int totalCost = pc.GetTotalEquipmentRepairCost();
        SetButtonText(repairAllButton, $"Починить всё - {totalCost} ₽");
        repairAllButton.interactable =
            totalCost > 0 && pc.CanServiceEquipment && balance >= totalCost;
        previousButton.interactable = pcs.Count > 1;
        nextButton.interactable = pcs.Count > 1;
    }

    private void RefreshTechnicianSection(int balance)
    {
        TechnicianManager technicianManager = TechnicianManager.Instance;
        if (technicianManager == null)
        {
            technicianText.text = "Техник: менеджер не найден";
            SetButtonText(hireTechnicianButton, "Техник недоступен");
            hireTechnicianButton.interactable = false;
            return;
        }

        if (technicianManager.TechnicianHired)
        {
            technicianText.text =
                $"Техник: нанят | Зарплата: {technicianManager.DailySalary} ₽/день\n" +
                technicianManager.LastServiceMessage;
            SetButtonText(hireTechnicianButton, "Техник уже нанят");
            hireTechnicianButton.interactable = false;
            return;
        }

        technicianText.text =
            $"Техник: не нанят | Ремонт при износе до " +
            $"{technicianManager.ServiceThreshold:F0}%";
        SetButtonText(
            hireTechnicianButton,
            $"Нанять техника - {technicianManager.HireCost} ₽"
        );
        hireTechnicianButton.interactable = balance >= technicianManager.HireCost;
    }

    private void RefreshCleanerSection(int balance)
    {
        CleanerManager cleanerManager = CleanerManager.Instance;
        if (cleanerManager == null)
        {
            cleanerText.text = "Уборщик: менеджер не найден";
            SetButtonText(hireCleanerButton, "Уборщик недоступен");
            hireCleanerButton.interactable = false;
            return;
        }

        if (cleanerManager.CleanerHired)
        {
            cleanerText.text =
                $"Уборщик: работает | Зарплата: {cleanerManager.DailySalary} ₽/день\n" +
                cleanerManager.LastWorkMessage;
            SetButtonText(hireCleanerButton, "Уборщик уже нанят");
            hireCleanerButton.interactable = false;
            return;
        }

        cleanerText.text =
            "Уборщик: не нанят | Автоматически убирает мусор";
        SetButtonText(
            hireCleanerButton,
            $"Нанять уборщика - {cleanerManager.HireCost} ₽"
        );
        hireCleanerButton.interactable = balance >= cleanerManager.HireCost;
    }

    private static string GetStateName(PC pc)
    {
        if (!pc.HasRoomAccess) return "закрытая комната";
        if (pc.IsOccupied) return "занят";
        if (pc.IsReserved) return "зарезервирован";
        if (pc.IsBroken) return "сломанный системный блок";
        if (pc.HasBrokenEquipment) return "сломан элемент оборудования";
        return "свободен";
    }

    private static void RefreshEquipmentButton(
        Button button,
        PCEquipmentCondition equipment,
        string displayName,
        PC pc,
        int balance)
    {
        SetButtonText(
            button,
            $"{displayName}: {equipment.Condition:F0}% - {equipment.RepairCost} ₽"
        );
        button.interactable = equipment.Condition < 100f &&
            pc.CanServiceEquipment && balance >= equipment.RepairCost;
    }

    private static void SetButtonText(Button button, string content)
    {
        Text text = button.GetComponentInChildren<Text>();
        if (text != null)
        {
            text.text = content;
        }
    }

    private void SetRepairButtons(bool active)
    {
        keyboardButton.interactable = active;
        mouseButton.interactable = active;
        chairButton.interactable = active;
        repairAllButton.interactable = active;
    }

    private void RestoreGameplayState()
    {
        Time.timeScale = previousTimeScale;

        if (!cursorStateCaptured)
        {
            return;
        }

        Cursor.visible = previousCursorVisible;
        Cursor.lockState = previousCursorLockMode;
        cursorStateCaptured = false;
    }

    private void CaptureCursorState()
    {
        if (cursorStateCaptured)
        {
            return;
        }

        previousCursorVisible = Cursor.visible;
        previousCursorLockMode = Cursor.lockState;
        cursorStateCaptured = true;
    }

    private IEnumerator SelectDefaultButtonNextFrame()
    {
        yield return null;

        if (isOpen && EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
            closeButton.Select();
        }
    }

    private void BuildInterface()
    {
        runtimeFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        rootObject = new GameObject(
            "MaintenancePanelRoot",
            typeof(RectTransform),
            typeof(Image)
        );
        rootObject.transform.SetParent(transform, false);
        Stretch(rootObject.GetComponent<RectTransform>());
        rootObject.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.76f);

        GameObject panel = new GameObject(
            "MaintenancePanel",
            typeof(RectTransform),
            typeof(Image),
            typeof(VerticalLayoutGroup)
        );
        panel.transform.SetParent(rootObject.transform, false);
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(720f, 960f);
        panel.GetComponent<Image>().color = new Color(0.035f, 0.045f, 0.065f, 0.99f);

        VerticalLayoutGroup layout = panel.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(28, 28, 24, 24);
        layout.spacing = 10f;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        CreateLabel(panel.transform, "ТЕРМИНАЛ ОБСЛУЖИВАНИЯ", 30, 54f, FontStyle.Bold);
        GameObject navigation = CreateHorizontalRow(panel.transform, 54f);
        previousButton = CreateButton(navigation.transform, "Предыдущий ПК", SelectPreviousPC);
        nextButton = CreateButton(navigation.transform, "Следующий ПК", SelectNextPC);
        pcInformationText = CreateLabel(panel.transform, string.Empty, 21, 64f, FontStyle.Bold);
        stateText = CreateLabel(panel.transform, string.Empty, 21, 34f, FontStyle.Normal);
        balanceText = CreateLabel(panel.transform, string.Empty, 21, 34f, FontStyle.Normal);
        technicianText = CreateLabel(panel.transform, string.Empty, 18, 54f, FontStyle.Normal);
        hireTechnicianButton = CreateButton(panel.transform, "Нанять техника", HireTechnician);
        cleanerText = CreateLabel(panel.transform, string.Empty, 18, 54f, FontStyle.Normal);
        hireCleanerButton = CreateButton(panel.transform, "Нанять уборщика", HireCleaner);
        keyboardButton = CreateButton(panel.transform, "Клавиатура", RepairKeyboard);
        mouseButton = CreateButton(panel.transform, "Мышь", RepairMouse);
        chairButton = CreateButton(panel.transform, "Кресло", RepairChair);
        repairAllButton = CreateButton(panel.transform, "Починить всё", RepairAll);
        statusText = CreateLabel(panel.transform, string.Empty, 19, 40f, FontStyle.Normal);
        closeButton = CreateButton(panel.transform, "Закрыть", Close);
    }

    private Text CreateLabel(
        Transform parent,
        string content,
        int fontSize,
        float height,
        FontStyle fontStyle)
    {
        GameObject label = new GameObject("Text", typeof(RectTransform), typeof(Text), typeof(LayoutElement));
        label.transform.SetParent(parent, false);
        Text text = label.GetComponent<Text>();
        text.font = runtimeFont;
        text.text = content;
        text.fontSize = fontSize;
        text.fontStyle = fontStyle;
        text.color = Color.white;
        text.alignment = TextAnchor.MiddleCenter;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;
        label.GetComponent<LayoutElement>().preferredHeight = height;
        return text;
    }

    private Button CreateButton(Transform parent, string caption, UnityEngine.Events.UnityAction action)
    {
        GameObject buttonObject = new GameObject(
            caption,
            typeof(RectTransform),
            typeof(Image),
            typeof(Button),
            typeof(LayoutElement)
        );
        buttonObject.transform.SetParent(parent, false);
        Image image = buttonObject.GetComponent<Image>();
        image.color = new Color(0.12f, 0.16f, 0.23f, 1f);
        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(action);
        ColorBlock colors = button.colors;
        colors.normalColor = new Color(0.12f, 0.16f, 0.23f, 1f);
        colors.highlightedColor = new Color(0.20f, 0.29f, 0.42f, 1f);
        colors.selectedColor = colors.highlightedColor;
        colors.pressedColor = new Color(0.08f, 0.11f, 0.17f, 1f);
        colors.disabledColor = new Color(0.10f, 0.10f, 0.10f, 0.55f);
        colors.colorMultiplier = 1f;
        button.colors = colors;
        buttonObject.GetComponent<LayoutElement>().preferredHeight = 52f;
        Text text = CreateLabel(buttonObject.transform, caption, 20, 52f, FontStyle.Normal);
        RectTransform textRect = text.GetComponent<RectTransform>();
        Stretch(textRect);
        textRect.offsetMin = new Vector2(12f, 4f);
        textRect.offsetMax = new Vector2(-12f, -4f);
        return button;
    }

    private static GameObject CreateHorizontalRow(Transform parent, float height)
    {
        GameObject row = new GameObject(
            "NavigationRow",
            typeof(RectTransform),
            typeof(HorizontalLayoutGroup),
            typeof(LayoutElement)
        );
        row.transform.SetParent(parent, false);
        HorizontalLayoutGroup layout = row.GetComponent<HorizontalLayoutGroup>();
        layout.spacing = 10f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = true;
        row.GetComponent<LayoutElement>().preferredHeight = height;
        return row;
    }

    private static void Stretch(RectTransform rectTransform)
    {
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.anchoredPosition = Vector2.zero;
        rectTransform.sizeDelta = Vector2.zero;
    }

    private static void EnsureEventSystem()
    {
        EventSystem system = EventSystem.current ?? FindAnyObjectByType<EventSystem>();
        if (system == null)
        {
            GameObject systemObject = new GameObject("EventSystem", typeof(EventSystem));
            system = systemObject.GetComponent<EventSystem>();
        }

        if (system.GetComponent<InputSystemUIInputModule>() == null)
        {
            system.gameObject.AddComponent<InputSystemUIInputModule>();
        }
    }
}
