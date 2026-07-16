using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

public sealed class MarketingPanel : MonoBehaviour
{
    private static readonly MarketingCampaignType[] CampaignTypes =
    {
        MarketingCampaignType.SocialMedia,
        MarketingCampaignType.GamerAdvertising,
        MarketingCampaignType.VIPPromotion,
        MarketingCampaignType.Tournament
    };

    public static MarketingPanel Instance { get; private set; }

    private GameObject rootObject;
    private Text currentCampaignText;
    private Text statusText;
    private readonly Button[] campaignButtons = new Button[4];
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
        if (MarketingManager.Instance != null)
        {
            MarketingManager.Instance.StatusChanged += RefreshView;
        }

        if (EconomyManager.Instance != null)
        {
            EconomyManager.Instance.MoneyChanged += OnMoneyChanged;
        }
    }

    private void OnDestroy()
    {
        if (MarketingManager.Instance != null)
        {
            MarketingManager.Instance.StatusChanged -= RefreshView;
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
        if (isOpen || MarketingManager.Instance == null ||
            (PauseMenuController.Instance != null && PauseMenuController.Instance.IsMenuOpen) ||
            (PCMaintenancePanel.Instance != null && PCMaintenancePanel.Instance.IsOpen) ||
            (PricingPanel.Instance != null && PricingPanel.Instance.IsOpen) ||
            (ConsumableStockPanel.Instance != null && ConsumableStockPanel.Instance.IsOpen) ||
            (DailyFinancialReportPanel.Instance != null && DailyFinancialReportPanel.Instance.IsOpen))
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

    private void StartCampaign(int index)
    {
        if (index < 0 || index >= CampaignTypes.Length)
        {
            return;
        }

        MarketingManager.Instance?.TryStartCampaign(CampaignTypes[index]);
        RefreshView();
    }

    private void OnMoneyChanged(int _) => RefreshView();

    private void RefreshView()
    {
        MarketingManager manager = MarketingManager.Instance;
        if (manager == null || currentCampaignText == null)
        {
            return;
        }

        currentCampaignText.text = manager.HasActiveCampaign
            ? $"Current campaign:\n{manager.GetDefinition(manager.ActiveCampaign)?.DisplayName}\n{manager.RemainingDays} day(s) remaining"
            : "Current campaign:\nNone";
        statusText.text = manager.LastStatusMessage;
        int balance = EconomyManager.Instance != null ? EconomyManager.Instance.Money : 0;

        for (int i = 0; i < CampaignTypes.Length; i++)
        {
            MarketingCampaignDefinition definition = manager.GetDefinition(CampaignTypes[i]);
            Button button = campaignButtons[i];
            if (definition == null || button == null)
            {
                continue;
            }

            Text text = button.GetComponentInChildren<Text>();
            int activationCost = manager.GetEffectiveActivationCost(CampaignTypes[i]);
            if (text != null)
            {
                text.text = $"{definition.DisplayName}\n{activationCost} RUB | {definition.DurationDays} day(s) | demand +{(definition.DemandMultiplier - 1f) * 100f:F0}%";
            }

            button.interactable = !manager.HasActiveCampaign && balance >= activationCost;
        }
    }

    private void BuildInterface()
    {
        runtimeFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        rootObject = new GameObject("MarketingPanelRoot", typeof(RectTransform), typeof(Image));
        rootObject.transform.SetParent(transform, false);
        Stretch(rootObject.GetComponent<RectTransform>());
        rootObject.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.76f);

        GameObject panel = new GameObject("MarketingPanel", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
        panel.transform.SetParent(rootObject.transform, false);
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(700f, 780f);
        panel.GetComponent<Image>().color = new Color(0.13f, 0.11f, 0.02f, 0.99f);

        VerticalLayoutGroup layout = panel.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(28, 28, 24, 24);
        layout.spacing = 10f;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        CreateLabel(panel.transform, "MARKETING", 30, 54f, FontStyle.Bold);
        currentCampaignText = CreateLabel(panel.transform, string.Empty, 21, 78f, FontStyle.Normal);
        for (int i = 0; i < CampaignTypes.Length; i++)
        {
            int campaignIndex = i;
            campaignButtons[i] = CreateButton(panel.transform, CampaignTypes[i].ToString(), () => StartCampaign(campaignIndex), 76f);
        }

        statusText = CreateLabel(panel.transform, string.Empty, 17, 36f, FontStyle.Normal);
        closeButton = CreateButton(panel.transform, "Close", Close, 54f);
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

    private Button CreateButton(Transform parent, string caption, UnityEngine.Events.UnityAction action, float height)
    {
        GameObject buttonObject = new GameObject(caption, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        buttonObject.transform.SetParent(parent, false);
        Image image = buttonObject.GetComponent<Image>();
        image.color = new Color(0.50f, 0.43f, 0.05f, 1f);
        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(action);
        ColorBlock colors = button.colors;
        colors.normalColor = image.color;
        colors.highlightedColor = new Color(0.72f, 0.62f, 0.08f, 1f);
        colors.selectedColor = colors.highlightedColor;
        colors.pressedColor = new Color(0.28f, 0.23f, 0.02f, 1f);
        colors.colorMultiplier = 1f;
        button.colors = colors;
        buttonObject.GetComponent<LayoutElement>().preferredHeight = height;
        Text text = CreateLabel(buttonObject.transform, caption, 18, height, FontStyle.Bold);
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
        if (!cursorStateCaptured)
        {
            previousCursorVisible = Cursor.visible;
            previousCursorLockMode = Cursor.lockState;
            cursorStateCaptured = true;
        }
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
