using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

public sealed class PricingPanel : MonoBehaviour
{
    private const int BasicBasePrice = 100;
    private const int GamingBasePrice = 160;
    private const int PremiumBasePrice = 250;

    public static PricingPanel Instance { get; private set; }

    private GameObject rootObject;
    private Text basicText;
    private Text gamingText;
    private Text premiumText;
    private Text rangeText;
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
        if (PricingManager.Instance != null)
        {
            PricingManager.Instance.StatusChanged += RefreshView;
        }
    }

    private void OnDestroy()
    {
        if (PricingManager.Instance != null)
        {
            PricingManager.Instance.StatusChanged -= RefreshView;
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
        if (isOpen || PricingManager.Instance == null ||
            (PauseMenuController.Instance != null && PauseMenuController.Instance.IsMenuOpen) ||
            (PCMaintenancePanel.Instance != null && PCMaintenancePanel.Instance.IsOpen))
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

    private void ChangeBasicPrice(int direction)
    {
        PricingManager.Instance?.TryChangePrice(PCTier.Basic, direction);
        RefreshView();
    }

    private void ChangeGamingPrice(int direction)
    {
        PricingManager.Instance?.TryChangePrice(PCTier.Gaming, direction);
        RefreshView();
    }

    private void ChangePremiumPrice(int direction)
    {
        PricingManager.Instance?.TryChangePrice(PCTier.Premium, direction);
        RefreshView();
    }

    private void RefreshView()
    {
        PricingManager manager = PricingManager.Instance;
        if (manager == null || basicText == null)
        {
            return;
        }

        basicText.text = FormatTier(manager, "Basic", PCTier.Basic, BasicBasePrice);
        gamingText.text = FormatTier(manager, "Gaming", PCTier.Gaming, GamingBasePrice);
        premiumText.text = FormatTier(manager, "Premium", PCTier.Premium, PremiumBasePrice);
        rangeText.text = $"Range: {manager.MinimumPricePercent}-{manager.MaximumPricePercent}% | Step: {manager.PriceStepPercent}%";
    }

    private static string FormatTier(PricingManager manager, string title, PCTier tier, int basePrice)
    {
        return $"{title}: {manager.GetPricePercent(tier)}% - {manager.GetSessionPrice(tier, basePrice)} RUB";
    }

    private void BuildInterface()
    {
        runtimeFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        rootObject = new GameObject("PricingPanelRoot", typeof(RectTransform), typeof(Image));
        rootObject.transform.SetParent(transform, false);
        Stretch(rootObject.GetComponent<RectTransform>());
        rootObject.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.76f);

        GameObject panel = new GameObject("PricingPanel", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
        panel.transform.SetParent(rootObject.transform, false);
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(680f, 500f);
        panel.GetComponent<Image>().color = new Color(0.055f, 0.035f, 0.09f, 0.99f);

        VerticalLayoutGroup layout = panel.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(28, 28, 24, 24);
        layout.spacing = 12f;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        CreateLabel(panel.transform, "PRICING MANAGEMENT", 30, 54f, FontStyle.Bold);
        basicText = CreatePriceRow(panel.transform, "Basic", ChangeBasicPrice);
        gamingText = CreatePriceRow(panel.transform, "Gaming", ChangeGamingPrice);
        premiumText = CreatePriceRow(panel.transform, "Premium", ChangePremiumPrice);
        rangeText = CreateLabel(panel.transform, string.Empty, 18, 40f, FontStyle.Normal);
        closeButton = CreateButton(panel.transform, "Close", Close);
    }

    private Text CreatePriceRow(Transform parent, string label, System.Action<int> onChange)
    {
        GameObject row = new GameObject(label + "Row", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        row.transform.SetParent(parent, false);
        row.GetComponent<LayoutElement>().preferredHeight = 60f;
        HorizontalLayoutGroup layout = row.GetComponent<HorizontalLayoutGroup>();
        layout.spacing = 12f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = true;
        CreateButton(row.transform, "-", () => onChange(-1), 70f);
        Text text = CreateLabel(row.transform, string.Empty, 22, 56f, FontStyle.Bold);
        text.GetComponent<LayoutElement>().flexibleWidth = 1f;
        CreateButton(row.transform, "+", () => onChange(1), 70f);
        return text;
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

    private Button CreateButton(Transform parent, string caption, UnityEngine.Events.UnityAction action, float preferredWidth = -1f)
    {
        GameObject buttonObject = new GameObject(caption, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        buttonObject.transform.SetParent(parent, false);
        Image image = buttonObject.GetComponent<Image>();
        image.color = new Color(0.27f, 0.16f, 0.39f, 1f);
        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(action);
        ColorBlock colors = button.colors;
        colors.normalColor = image.color;
        colors.highlightedColor = new Color(0.42f, 0.25f, 0.58f, 1f);
        colors.selectedColor = colors.highlightedColor;
        colors.pressedColor = new Color(0.16f, 0.09f, 0.25f, 1f);
        colors.colorMultiplier = 1f;
        button.colors = colors;
        LayoutElement layout = buttonObject.GetComponent<LayoutElement>();
        layout.preferredHeight = 54f;
        if (preferredWidth > 0f)
        {
            layout.preferredWidth = preferredWidth;
            layout.flexibleWidth = 0f;
        }

        Text text = CreateLabel(buttonObject.transform, caption, 21, 54f, FontStyle.Bold);
        RectTransform textRect = text.GetComponent<RectTransform>();
        Stretch(textRect);
        textRect.offsetMin = new Vector2(8f, 2f);
        textRect.offsetMax = new Vector2(-8f, -2f);
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
