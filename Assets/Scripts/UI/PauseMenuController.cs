using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor;
#endif

public sealed class PauseMenuController : MonoBehaviour
{
    public static PauseMenuController Instance { get; private set; }

    [Header("Canvas Settings")]
    [SerializeField] private Vector2 referenceResolution =
        new Vector2(1920f, 1080f);

    [SerializeField, Range(0f, 1f)]
    private float widthHeightMatch = 0.5f;

    [Header("Menu Settings")]
    [SerializeField] private Vector2 menuSize = new Vector2(580f, 620f);
    [SerializeField] private int titleFontSize = 32;
    [SerializeField] private int textFontSize = 22;
    [SerializeField] private int buttonFontSize = 22;

    private bool isMenuOpen;
    private bool isGameOverMode;
    private bool confirmNewGame;

    private bool cursorStateCaptured;
    private bool previousCursorVisible;
    private CursorLockMode previousCursorLockMode;
    private float statusMessageUntil;

    private GameObject pauseCanvasObject;
    private GameObject menuRoot;
    private GameObject confirmationPanel;

    private Text titleText;
    private Text gameOverInformationText;
    private Text statusText;

    private Button continueButton;
    private Button saveButton;
    private Button newGameButton;
    private Button quitButton;
    private Button confirmNewGameButton;
    private Button cancelNewGameButton;

    private Font runtimeFont;

    public bool IsMenuOpen => isMenuOpen;
    public bool BlocksGameplayInput => isMenuOpen || isGameOverMode;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        Instance = null;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
        EnsureEventSystem();
        BuildCanvasMenu();
        SetCanvasVisible(false);
    }

    private void Start()
    {
        if (BankruptcyManager.Instance == null)
        {
            return;
        }

        BankruptcyManager.Instance.GameOverTriggered += OnGameOverTriggered;

        if (BankruptcyManager.Instance.IsGameOver)
        {
            OnGameOverTriggered();
        }
    }

    private void Update()
    {
        if (statusText == null || string.IsNullOrWhiteSpace(statusText.text) ||
            Time.unscaledTime < statusMessageUntil)
        {
            return;
        }

        statusText.text = string.Empty;
    }

    private void OnDestroy()
    {
        if (BankruptcyManager.Instance != null)
        {
            BankruptcyManager.Instance.GameOverTriggered -= OnGameOverTriggered;
        }

        RestoreCursorState();
        Time.timeScale = 1f;

        if (Instance == this)
        {
            Instance = null;
        }
    }

    public void OnPause(InputValue inputValue)
    {
        if (!inputValue.isPressed || isGameOverMode)
        {
            return;
        }

        if (ClubRandomEventPanel.Instance != null &&
            ClubRandomEventPanel.Instance.IsOpen)
        {
            ClubRandomEventPanel.Instance.Close();
            return;
        }

        if (InternetProviderPanel.Instance != null &&
            InternetProviderPanel.Instance.IsOpen)
        {
            InternetProviderPanel.Instance.Close();
            return;
        }

        if (ClubResearchPanel.Instance != null &&
            ClubResearchPanel.Instance.IsOpen)
        {
            ClubResearchPanel.Instance.Close();
            return;
        }

        if (PCMaintenancePanel.Instance != null &&
            PCMaintenancePanel.Instance.IsOpen)
        {
            PCMaintenancePanel.Instance.Close();
            return;
        }

        if (PricingPanel.Instance != null && PricingPanel.Instance.IsOpen)
        {
            PricingPanel.Instance.Close();
            return;
        }

        if (ConsumableStockPanel.Instance != null &&
            ConsumableStockPanel.Instance.IsOpen)
        {
            ConsumableStockPanel.Instance.Close();
            return;
        }

        if (DailyFinancialReportPanel.Instance != null &&
            DailyFinancialReportPanel.Instance.IsOpen)
        {
            DailyFinancialReportPanel.Instance.Close();
            return;
        }

        if (MarketingPanel.Instance != null && MarketingPanel.Instance.IsOpen)
        {
            MarketingPanel.Instance.Close();
            return;
        }

        if (DemandAnalyticsPanel.Instance != null &&
            DemandAnalyticsPanel.Instance.IsOpen)
        {
            DemandAnalyticsPanel.Instance.Close();
            return;
        }

        if (confirmNewGame)
        {
            HideNewGameConfirmation();
            return;
        }

        SetMenuOpen(!isMenuOpen);
    }

    private void OnGameOverTriggered()
    {
        isGameOverMode = true;
        confirmNewGame = false;

        if (!isMenuOpen)
        {
            SetMenuOpen(true);
            return;
        }

        UpdateMenuMode();
        StartCoroutine(SelectDefaultButtonNextFrame());
    }

    private void SetMenuOpen(bool shouldOpen)
    {
        if (isGameOverMode && !shouldOpen)
        {
            return;
        }

        if (isMenuOpen == shouldOpen)
        {
            return;
        }

        isMenuOpen = shouldOpen;
        confirmNewGame = false;

        if (isMenuOpen)
        {
            CaptureCursorState();
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            Time.timeScale = 0f;
            SetCanvasVisible(true);
            UpdateMenuMode();
            StartCoroutine(SelectDefaultButtonNextFrame());
            return;
        }

        SetCanvasVisible(false);

        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
        }

        Time.timeScale = 1f;
        RestoreCursorState();
    }

    private void UpdateMenuMode()
    {
        if (titleText == null)
        {
            return;
        }

        titleText.text = isGameOverMode ? "КЛУБ ОБАНКРОТИЛСЯ" : "ПАУЗА";
        continueButton.gameObject.SetActive(!isGameOverMode);
        saveButton.gameObject.SetActive(!isGameOverMode);
        gameOverInformationText.gameObject.SetActive(isGameOverMode);

        if (isGameOverMode)
        {
            RefreshGameOverInformation();
        }

        statusText.text = string.Empty;
        statusMessageUntil = 0f;
        SetNewGameConfirmationVisible(false);
    }

    private void RefreshGameOverInformation()
    {
        BankruptcyManager bankruptcy = BankruptcyManager.Instance;

        if (bankruptcy == null)
        {
            gameOverInformationText.text =
                "Игра завершена.\nСохранение этой попытки удалено.";
            return;
        }

        gameOverInformationText.text =
            $"Пройдено дней: {bankruptcy.GameOverDay}\n" +
            $"Итоговый баланс: {bankruptcy.FinalBalance} ₽\n" +
            "Сохранение этой попытки удалено.";
    }

    private void ContinueGame()
    {
        if (!isGameOverMode)
        {
            SetMenuOpen(false);
        }
    }

    private void SaveGame()
    {
        if (isGameOverMode)
        {
            return;
        }

        if (SaveManager.Instance == null)
        {
            ShowStatusMessage("SaveManager не найден.");
            return;
        }

        bool saveSucceeded = SaveManager.Instance.TrySaveGame();
        ShowStatusMessage(
            saveSucceeded ? "Игра сохранена." : "Не удалось сохранить игру."
        );
    }

    private void ShowNewGameConfirmation()
    {
        confirmNewGame = true;
        SetNewGameConfirmationVisible(true);
        StartCoroutine(SelectDefaultButtonNextFrame());
    }

    private void HideNewGameConfirmation()
    {
        confirmNewGame = false;
        SetNewGameConfirmationVisible(false);
        StartCoroutine(SelectDefaultButtonNextFrame());
    }

    private void SetNewGameConfirmationVisible(bool shouldShow)
    {
        confirmNewGame = shouldShow;

        if (confirmationPanel != null)
        {
            confirmationPanel.SetActive(shouldShow);
        }

        if (newGameButton != null)
        {
            newGameButton.gameObject.SetActive(!shouldShow);
        }
    }

    private void StartNewGame()
    {
        if (SaveManager.Instance == null)
        {
            ShowStatusMessage("SaveManager не найден.");
            return;
        }

        SaveManager.Instance.StartNewGame();
    }

    private void QuitGame()
    {
        if (!isGameOverMode && SaveManager.Instance != null)
        {
            SaveManager.Instance.TrySaveGame();
        }

        Time.timeScale = 1f;

#if UNITY_EDITOR
        EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void ShowStatusMessage(string message)
    {
        if (statusText == null)
        {
            return;
        }

        statusText.text = message;
        statusMessageUntil = Time.unscaledTime + 2.5f;
    }

    private IEnumerator SelectDefaultButtonNextFrame()
    {
        yield return null;

        if (!isMenuOpen || EventSystem.current == null)
        {
            yield break;
        }

        Button targetButton = confirmNewGame
            ? cancelNewGameButton
            : isGameOverMode
                ? newGameButton
                : continueButton;

        if (targetButton == null || !targetButton.gameObject.activeInHierarchy)
        {
            yield break;
        }

        EventSystem.current.SetSelectedGameObject(null);
        targetButton.Select();
    }

    private void BuildCanvasMenu()
    {
        runtimeFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        pauseCanvasObject = new GameObject(
            "PauseMenuCanvas",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster)
        );

        pauseCanvasObject.transform.SetParent(transform, false);

        Canvas canvas = pauseCanvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000;

        CanvasScaler scaler = pauseCanvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = referenceResolution;
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = widthHeightMatch;

        CreateMenuRoot();
    }

    private void CreateMenuRoot()
    {
        menuRoot = new GameObject(
            "MenuRoot",
            typeof(RectTransform),
            typeof(Image)
        );

        menuRoot.transform.SetParent(pauseCanvasObject.transform, false);

        RectTransform rootRect = menuRoot.GetComponent<RectTransform>();
        StretchToParent(rootRect);

        Image background = menuRoot.GetComponent<Image>();
        background.color = new Color(0f, 0f, 0f, 0.78f);

        CreateMenuPanel(menuRoot.transform);
    }

    private void CreateMenuPanel(Transform parent)
    {
        GameObject panelObject = new GameObject(
            "MenuPanel",
            typeof(RectTransform),
            typeof(Image),
            typeof(VerticalLayoutGroup)
        );

        panelObject.transform.SetParent(parent, false);

        RectTransform panelRect = panelObject.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.anchoredPosition = Vector2.zero;
        panelRect.sizeDelta = menuSize;

        Image panelImage = panelObject.GetComponent<Image>();
        panelImage.color = new Color(0.035f, 0.045f, 0.065f, 0.98f);

        VerticalLayoutGroup layout = panelObject.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(28, 28, 24, 24);
        layout.spacing = 10f;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        titleText = CreateLabel(
            "TitleText", panelObject.transform, string.Empty,
            titleFontSize, 62f, FontStyle.Bold
        );

        gameOverInformationText = CreateLabel(
            "GameOverInformationText", panelObject.transform, string.Empty,
            textFontSize, 112f, FontStyle.Normal
        );

        continueButton = CreateButton(
            "ContinueButton", "Продолжить", panelObject.transform
        );
        continueButton.onClick.AddListener(ContinueGame);

        saveButton = CreateButton(
            "SaveButton", "Сохранить игру", panelObject.transform
        );
        saveButton.onClick.AddListener(SaveGame);

        newGameButton = CreateButton(
            "NewGameButton", "Новая игра", panelObject.transform
        );
        newGameButton.onClick.AddListener(ShowNewGameConfirmation);

        confirmationPanel = CreateConfirmationPanel(panelObject.transform);

        quitButton = CreateButton(
            "QuitButton", "Выйти из игры", panelObject.transform
        );
        quitButton.onClick.AddListener(QuitGame);

        statusText = CreateLabel(
            "StatusText", panelObject.transform, string.Empty,
            20, 42f, FontStyle.Normal
        );
    }

    private GameObject CreateConfirmationPanel(Transform parent)
    {
        GameObject panelObject = new GameObject(
            "NewGameConfirmationPanel",
            typeof(RectTransform),
            typeof(VerticalLayoutGroup),
            typeof(LayoutElement)
        );

        panelObject.transform.SetParent(parent, false);

        LayoutElement panelLayout = panelObject.GetComponent<LayoutElement>();
        panelLayout.preferredHeight = 130f;

        VerticalLayoutGroup verticalLayout =
            panelObject.GetComponent<VerticalLayoutGroup>();
        verticalLayout.spacing = 8f;
        verticalLayout.childAlignment = TextAnchor.MiddleCenter;
        verticalLayout.childControlWidth = true;
        verticalLayout.childControlHeight = true;
        verticalLayout.childForceExpandWidth = true;
        verticalLayout.childForceExpandHeight = false;

        CreateLabel(
            "ConfirmationText", panelObject.transform,
            "Удалить сохранение и начать заново?", 20, 46f, FontStyle.Bold
        );

        GameObject buttonsRow = new GameObject(
            "ConfirmationButtons",
            typeof(RectTransform),
            typeof(HorizontalLayoutGroup),
            typeof(LayoutElement)
        );

        buttonsRow.transform.SetParent(panelObject.transform, false);

        LayoutElement rowLayout = buttonsRow.GetComponent<LayoutElement>();
        rowLayout.preferredHeight = 56f;

        HorizontalLayoutGroup horizontalLayout =
            buttonsRow.GetComponent<HorizontalLayoutGroup>();
        horizontalLayout.spacing = 10f;
        horizontalLayout.childAlignment = TextAnchor.MiddleCenter;
        horizontalLayout.childControlWidth = true;
        horizontalLayout.childControlHeight = true;
        horizontalLayout.childForceExpandWidth = true;
        horizontalLayout.childForceExpandHeight = true;

        confirmNewGameButton = CreateButton(
            "ConfirmNewGameButton", "Да, начать заново", buttonsRow.transform
        );
        confirmNewGameButton.onClick.AddListener(StartNewGame);

        cancelNewGameButton = CreateButton(
            "CancelNewGameButton", "Отмена", buttonsRow.transform
        );
        cancelNewGameButton.onClick.AddListener(HideNewGameConfirmation);

        panelObject.SetActive(false);
        return panelObject;
    }

    private Text CreateLabel(
        string objectName,
        Transform parent,
        string content,
        int fontSize,
        float preferredHeight,
        FontStyle fontStyle)
    {
        GameObject textObject = new GameObject(
            objectName,
            typeof(RectTransform),
            typeof(Text),
            typeof(LayoutElement)
        );

        textObject.transform.SetParent(parent, false);

        Text text = textObject.GetComponent<Text>();
        text.font = runtimeFont;
        text.text = content;
        text.fontSize = fontSize;
        text.fontStyle = fontStyle;
        text.color = Color.white;
        text.alignment = TextAnchor.MiddleCenter;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;

        LayoutElement layoutElement = textObject.GetComponent<LayoutElement>();
        layoutElement.preferredHeight = preferredHeight;

        return text;
    }

    private Button CreateButton(
        string objectName,
        string caption,
        Transform parent)
    {
        GameObject buttonObject = new GameObject(
            objectName,
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

        Navigation navigation = button.navigation;
        navigation.mode = Navigation.Mode.Automatic;
        button.navigation = navigation;

        ColorBlock colors = button.colors;
        colors.normalColor = new Color(0.12f, 0.16f, 0.23f, 1f);
        colors.highlightedColor = new Color(0.20f, 0.29f, 0.42f, 1f);
        colors.selectedColor = new Color(0.20f, 0.29f, 0.42f, 1f);
        colors.pressedColor = new Color(0.08f, 0.11f, 0.17f, 1f);
        colors.disabledColor = new Color(0.10f, 0.10f, 0.10f, 0.55f);
        colors.colorMultiplier = 1f;
        button.colors = colors;

        LayoutElement layoutElement = buttonObject.GetComponent<LayoutElement>();
        layoutElement.preferredHeight = 54f;
        layoutElement.flexibleWidth = 1f;

        GameObject textObject = new GameObject(
            "Text",
            typeof(RectTransform),
            typeof(Text)
        );

        textObject.transform.SetParent(buttonObject.transform, false);

        RectTransform textRect = textObject.GetComponent<RectTransform>();
        StretchToParent(textRect);
        textRect.offsetMin = new Vector2(12f, 4f);
        textRect.offsetMax = new Vector2(-12f, -4f);

        Text buttonText = textObject.GetComponent<Text>();
        buttonText.font = runtimeFont;
        buttonText.text = caption;
        buttonText.fontSize = buttonFontSize;
        buttonText.color = Color.white;
        buttonText.alignment = TextAnchor.MiddleCenter;
        buttonText.horizontalOverflow = HorizontalWrapMode.Wrap;
        buttonText.verticalOverflow = VerticalWrapMode.Overflow;
        buttonText.raycastTarget = false;

        return button;
    }

    private void EnsureEventSystem()
    {
        EventSystem eventSystem = EventSystem.current;

        if (eventSystem == null)
        {
            eventSystem = FindAnyObjectByType<EventSystem>();
        }

        if (eventSystem == null)
        {
            GameObject eventSystemObject = new GameObject(
                "EventSystem",
                typeof(EventSystem)
            );
            eventSystem = eventSystemObject.GetComponent<EventSystem>();
        }

        if (eventSystem.GetComponent<InputSystemUIInputModule>() != null)
        {
            return;
        }

        StandaloneInputModule legacyModule =
            eventSystem.GetComponent<StandaloneInputModule>();

        if (legacyModule != null)
        {
            legacyModule.enabled = false;
        }

        eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
    }

    private void SetCanvasVisible(bool shouldShow)
    {
        if (menuRoot != null && menuRoot.activeSelf != shouldShow)
        {
            menuRoot.SetActive(shouldShow);
        }
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

    private void RestoreCursorState()
    {
        if (!cursorStateCaptured)
        {
            return;
        }

        Cursor.visible = previousCursorVisible;
        Cursor.lockState = previousCursorLockMode;
        cursorStateCaptured = false;
    }

    private static void StretchToParent(RectTransform rectTransform)
    {
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.anchoredPosition = Vector2.zero;
        rectTransform.sizeDelta = Vector2.zero;
    }

    private void OnValidate()
    {
        referenceResolution.x = Mathf.Max(640f, referenceResolution.x);
        referenceResolution.y = Mathf.Max(360f, referenceResolution.y);
        menuSize.x = Mathf.Max(400f, menuSize.x);
        menuSize.y = Mathf.Max(480f, menuSize.y);
        titleFontSize = Mathf.Max(18, titleFontSize);
        textFontSize = Mathf.Max(14, textFontSize);
        buttonFontSize = Mathf.Max(14, buttonFontSize);
    }
}
