using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

public sealed class ConsumableStockPanel : MonoBehaviour
{
    public static ConsumableStockPanel Instance { get; private set; }

    private GameObject rootObject;
    private Text energyDrinkText;
    private Text snackText;
    private Text statisticsText;
    private Text statusText;
    private Button restockEnergyDrinkButton;
    private Button restockSnackButton;
    private Button closeButton;
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
        if (ConsumableInventoryManager.Instance != null)
        {
            ConsumableInventoryManager.Instance.StatusChanged += RefreshView;
        }

        if (EconomyManager.Instance != null)
        {
            EconomyManager.Instance.MoneyChanged += OnMoneyChanged;
        }
    }

    private void OnDestroy()
    {
        if (ConsumableInventoryManager.Instance != null)
        {
            ConsumableInventoryManager.Instance.StatusChanged -= RefreshView;
        }

        if (EconomyManager.Instance != null)
        {
            EconomyManager.Instance.MoneyChanged -= OnMoneyChanged;
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
        if (isOpen || ConsumableInventoryManager.Instance == null ||
            (PauseMenuController.Instance != null && PauseMenuController.Instance.IsMenuOpen) ||
            (PCMaintenancePanel.Instance != null && PCMaintenancePanel.Instance.IsOpen) ||
            (PricingPanel.Instance != null && PricingPanel.Instance.IsOpen))
        {
            return;
        }

        isOpen = true;
        previousTimeScale = Time.timeScale;
        Time.timeScale = 0f;
        CaptureCursorState();
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        EnsureEventSystem();
        rootObject.SetActive(true);
        rootObject.transform.SetAsLastSibling();
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

    private void RestockEnergyDrinks()
    {
        ConsumableInventoryManager.Instance?.TryRestock(ConsumableType.EnergyDrink);
        RefreshView();
    }

    private void RestockSnacks()
    {
        ConsumableInventoryManager.Instance?.TryRestock(ConsumableType.Snack);
        RefreshView();
    }

    private void OnMoneyChanged(int _) => RefreshView();

    private void RefreshView()
    {
        ConsumableInventoryManager manager = ConsumableInventoryManager.Instance;
        if (manager == null || energyDrinkText == null)
        {
            return;
        }

        int balance = EconomyManager.Instance != null ? EconomyManager.Instance.Money : 0;
        energyDrinkText.text = FormatStock(manager, ConsumableType.EnergyDrink, "Energy drinks");
        snackText.text = FormatStock(manager, ConsumableType.Snack, "Snacks");
        statisticsText.text =
            $"Sold: {manager.TotalItemsSold}\n" +
            $"Revenue: {manager.TotalConsumableRevenue} RUB\n" +
            $"Missed sales: {manager.MissedSales}";
        statusText.text = manager.LastStatusMessage;
        RefreshRestockButton(restockEnergyDrinkButton, manager, ConsumableType.EnergyDrink, balance, "Buy energy drinks");
        RefreshRestockButton(restockSnackButton, manager, ConsumableType.Snack, balance, "Buy snacks");
    }

    private static string FormatStock(ConsumableInventoryManager manager, ConsumableType type, string name)
    {
        return $"{name}: {manager.GetStock(type)}/{manager.GetMaximumStock(type)}\n" +
            $"Sale price: {manager.GetSalePrice(type)} RUB";
    }

    private static void RefreshRestockButton(
        Button button,
        ConsumableInventoryManager manager,
        ConsumableType type,
        int balance,
        string label)
    {
        int amount = manager.GetRestockAmount(type);
        int cost = manager.GetRestockCost(type);
        Text text = button.GetComponentInChildren<Text>();
        if (text != null)
        {
            text.text = amount > 0 ? $"{label}: {amount} for {cost} RUB" : "Stock is full";
        }

        button.interactable = amount > 0 && balance >= cost;
    }

    private void BuildInterface()
    {
        runtimeFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        rootObject = new GameObject("ConsumableStockPanelRoot", typeof(RectTransform), typeof(Image));
        rootObject.transform.SetParent(transform, false);
        Stretch(rootObject.GetComponent<RectTransform>());
        rootObject.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.76f);

        GameObject panel = new GameObject("ConsumableStockPanel", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
        panel.transform.SetParent(rootObject.transform, false);
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(650f, 610f);
        panel.GetComponent<Image>().color = new Color(0.12f, 0.06f, 0.02f, 0.99f);

        VerticalLayoutGroup layout = panel.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(28, 28, 24, 24);
        layout.spacing = 10f;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        CreateLabel(panel.transform, "SNACKS AND DRINKS STOCK", 28, 54f, FontStyle.Bold);
        energyDrinkText = CreateLabel(panel.transform, string.Empty, 21, 62f, FontStyle.Normal);
        restockEnergyDrinkButton = CreateButton(panel.transform, "Buy energy drinks", RestockEnergyDrinks);
        snackText = CreateLabel(panel.transform, string.Empty, 21, 62f, FontStyle.Normal);
        restockSnackButton = CreateButton(panel.transform, "Buy snacks", RestockSnacks);
        statisticsText = CreateLabel(panel.transform, string.Empty, 19, 82f, FontStyle.Normal);
        statusText = CreateLabel(panel.transform, string.Empty, 17, 42f, FontStyle.Normal);
        closeButton = CreateButton(panel.transform, "Close", Close);
    }

    private Text CreateLabel(Transform parent, string content, int fontSize, float height, FontStyle fontStyle)
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
        GameObject buttonObject = new GameObject(caption, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        buttonObject.transform.SetParent(parent, false);
        Image image = buttonObject.GetComponent<Image>();
        image.color = new Color(0.48f, 0.24f, 0.06f, 1f);
        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(action);
        ColorBlock colors = button.colors;
        colors.normalColor = image.color;
        colors.highlightedColor = new Color(0.68f, 0.36f, 0.09f, 1f);
        colors.selectedColor = colors.highlightedColor;
        colors.pressedColor = new Color(0.25f, 0.11f, 0.02f, 1f);
        colors.colorMultiplier = 1f;
        button.colors = colors;
        buttonObject.GetComponent<LayoutElement>().preferredHeight = 52f;
        Text text = CreateLabel(buttonObject.transform, caption, 19, 52f, FontStyle.Bold);
        RectTransform textRect = text.GetComponent<RectTransform>();
        Stretch(textRect);
        textRect.offsetMin = new Vector2(12f, 4f);
        textRect.offsetMax = new Vector2(-12f, -4f);
        return button;
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

    private static void Stretch(RectTransform rectTransform)
    {
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.anchoredPosition = Vector2.zero;
        rectTransform.sizeDelta = Vector2.zero;
    }
}
