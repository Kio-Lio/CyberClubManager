using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

public sealed class DailyFinancialReportPanel : MonoBehaviour
{
    public static DailyFinancialReportPanel Instance { get; private set; }

    private GameObject rootObject;
    private Text reportText;
    private Button continueButton;
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
        if (DailyFinancialReportManager.Instance != null)
        {
            DailyFinancialReportManager.Instance.ReportCompleted += OnReportCompleted;
        }
    }

    private void OnDestroy()
    {
        if (DailyFinancialReportManager.Instance != null)
        {
            DailyFinancialReportManager.Instance.ReportCompleted -= OnReportCompleted;
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

    public void Open(DailyFinancialReportData report)
    {
        if (isOpen || report == null ||
            (BankruptcyManager.Instance != null && BankruptcyManager.Instance.IsGameOver))
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
        reportText.text = FormatReport(report);
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

    private void OnReportCompleted(DailyFinancialReportData report)
    {
        StartCoroutine(OpenAfterDayEvents(report));
    }

    private IEnumerator OpenAfterDayEvents(DailyFinancialReportData report)
    {
        yield return null;

        if (BankruptcyManager.Instance == null || !BankruptcyManager.Instance.IsGameOver)
        {
            Open(report);
        }
    }

    private static string FormatReport(DailyFinancialReportData report)
    {
        string prefix = report.NetCashChange >= 0 ? "+" : string.Empty;
        return
            $"DAILY FINANCIAL REPORT - DAY {report.day}\n\n" +
            "INCOME\n" +
            $"Sessions: {report.sessionRevenue} RUB\n" +
            $"Drinks and snacks: {report.consumableRevenue} RUB\n" +
            $"Other revenue: {report.otherIncome} RUB\n" +
            $"Rewards: {report.Bonuses} RUB\n\n" +
            "OPERATING EXPENSES\n" +
            $"Club upkeep: {report.fixedOperatingExpenses} RUB\n" +
            $"Electricity: {report.electricityExpenses} RUB\n" +
            $"Salaries: {report.staffSalaryExpenses} RUB\n" +
            $"PC repair: {report.pcRepairExpenses} RUB\n" +
            $"Equipment repair: {report.equipmentRepairExpenses} RUB\n" +
            $"Restocking: {report.consumableRestockExpenses} RUB\n\n" +
            "INVESTMENTS\n" +
            $"Upgrades: {report.pcUpgradeExpenses} RUB\n" +
            $"Expansion: {report.expansionExpenses} RUB\n" +
            $"Rooms: {report.roomUnlockExpenses} RUB\n" +
            $"Staff hiring: {report.staffHireExpenses} RUB\n\n" +
            $"Balance change: {prefix}{report.NetCashChange} RUB";
    }

    private void BuildInterface()
    {
        runtimeFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        rootObject = new GameObject("DailyFinancialReportPanelRoot", typeof(RectTransform), typeof(Image));
        rootObject.transform.SetParent(transform, false);
        Stretch(rootObject.GetComponent<RectTransform>());
        rootObject.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.78f);

        GameObject panel = new GameObject("DailyFinancialReportPanel", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
        panel.transform.SetParent(rootObject.transform, false);
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(720f, 900f);
        panel.GetComponent<Image>().color = new Color(0.025f, 0.07f, 0.06f, 0.99f);

        VerticalLayoutGroup layout = panel.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(30, 30, 26, 26);
        layout.spacing = 12f;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        reportText = CreateLabel(panel.transform, string.Empty, 20, 760f, TextAnchor.UpperLeft, FontStyle.Normal);
        continueButton = CreateButton(panel.transform, "Continue", Close);
    }

    private Text CreateLabel(Transform parent, string content, int fontSize, float height, TextAnchor alignment, FontStyle fontStyle)
    {
        GameObject label = new GameObject("Text", typeof(RectTransform), typeof(Text), typeof(LayoutElement));
        label.transform.SetParent(parent, false);
        Text text = label.GetComponent<Text>();
        text.font = runtimeFont;
        text.text = content;
        text.fontSize = fontSize;
        text.fontStyle = fontStyle;
        text.color = Color.white;
        text.alignment = alignment;
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
        image.color = new Color(0.08f, 0.34f, 0.28f, 1f);
        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(action);
        ColorBlock colors = button.colors;
        colors.normalColor = image.color;
        colors.highlightedColor = new Color(0.12f, 0.5f, 0.4f, 1f);
        colors.selectedColor = colors.highlightedColor;
        colors.pressedColor = new Color(0.04f, 0.18f, 0.15f, 1f);
        colors.colorMultiplier = 1f;
        button.colors = colors;
        buttonObject.GetComponent<LayoutElement>().preferredHeight = 56f;
        Text text = CreateLabel(buttonObject.transform, caption, 21, 56f, TextAnchor.MiddleCenter, FontStyle.Bold);
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
            continueButton.Select();
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
