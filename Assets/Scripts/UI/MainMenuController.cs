using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor;
#endif

public sealed class MainMenuController : MonoBehaviour
{
    public static MainMenuController Instance { get; private set; }

    private static readonly Vector2 ReferenceResolution =
        new Vector2(1920f, 1080f);

    private Font runtimeFont;
    private CanvasScaler canvasScaler;
    private Button continueButton;
    private Button newGameButton;
    private Button deleteCorruptedSaveButton;
    private Text saveSummaryText;
    private Text statusText;
    private GameObject settingsOverlay;
    private GameObject newGameConfirmationOverlay;

    private Toggle fullscreenToggle;
    private Toggle screenEffectsToggle;
    private Slider volumeSlider;
    private Slider uiScaleSlider;
    private Text volumeValueText;
    private Text uiScaleValueText;
    private Text resolutionValueText;
    private List<Vector2Int> resolutions;
    private int selectedResolutionIndex;
    private bool isLoading;
    private bool hasSave;
    private bool saveIsCorrupted;
    private GameSaveSummary saveSummary;

    public bool ContinueAvailable =>
        continueButton != null && continueButton.interactable;

    public bool SaveIsCorrupted => saveIsCorrupted;
    public bool IsLoading => isLoading;
    public GameSaveSummary SaveSummary => saveSummary;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        Instance = null;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        GameUserSettings.ApplyDisplayAndAudio();
        EnsureEventSystem();
        BuildInterface();
        RefreshSaveState();
        Debug.Log("Main menu initialized.");
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public void RefreshSaveState()
    {
        hasSave = SaveManager.HasSaveFile();
        saveSummary = SaveManager.TryReadSaveSummary();
        saveIsCorrupted = hasSave && !saveSummary.isValid;

        if (continueButton != null)
        {
            continueButton.interactable = hasSave && saveSummary.isValid;
        }

        if (deleteCorruptedSaveButton != null)
        {
            deleteCorruptedSaveButton.gameObject.SetActive(saveIsCorrupted);
        }

        if (saveSummaryText == null)
        {
            return;
        }

        if (!hasSave)
        {
            saveSummaryText.text =
                "Сохранение не найдено\n\n" +
                "Начните новую игру, чтобы открыть клуб.";
            return;
        }

        if (saveIsCorrupted)
        {
            saveSummaryText.text =
                "СОХРАНЕНИЕ ПОВРЕЖДЕНО\n\n" +
                "Продолжение невозможно. Удалите файл сохранения и " +
                "начните новую игру.";
            return;
        }

        saveSummaryText.text =
            $"День: {saveSummary.day}\n" +
            $"Баланс: {saveSummary.balance:N0} ₽\n" +
            $"Уровень клуба: {saveSummary.clubLevel}\n" +
            $"Репутация: {saveSummary.reputation}\n\n" +
            "Последнее сохранение:\n" +
            saveSummary.savedAt.ToString("dd.MM.yyyy HH:mm");
    }

    private void ContinueGame()
    {
        if (isLoading || !ContinueAvailable)
        {
            return;
        }

        BeginSceneLoad(SceneTransitionLoader.GameSceneName);
    }

    private void RequestNewGame()
    {
        if (isLoading)
        {
            return;
        }

        if (hasSave)
        {
            newGameConfirmationOverlay.SetActive(true);
            SelectButton("CancelNewGameButton");
            return;
        }

        StartNewGame();
    }

    private void StartNewGame()
    {
        if (!SaveManager.DeleteSaveFile())
        {
            ShowStatus("Не удалось удалить сохранение.");
            return;
        }

        newGameConfirmationOverlay.SetActive(false);
        SceneTransitionLoader.CloseGameplayPanels();
        BeginSceneLoad(SceneTransitionLoader.GameSceneName);
    }

    private void CancelNewGame()
    {
        newGameConfirmationOverlay.SetActive(false);
        SelectButton("NewGameButton");
    }

    private void DeleteCorruptedSave()
    {
        if (SaveManager.DeleteSaveFile())
        {
            ShowStatus("Поврежденное сохранение удалено.");
            RefreshSaveState();
            return;
        }

        ShowStatus("Не удалось удалить сохранение.");
    }

    private void OpenSettings()
    {
        PopulateSettingsControls();
        settingsOverlay.SetActive(true);
        SelectButton("ApplySettingsButton");
    }

    private void CloseSettings()
    {
        settingsOverlay.SetActive(false);
        SelectButton("SettingsButton");
    }

    private void ApplySettings()
    {
        Vector2Int resolution = resolutions[selectedResolutionIndex];
        GameUserSettings.Save(
            volumeSlider.value,
            fullscreenToggle.isOn,
            resolution.x,
            resolution.y,
            uiScaleSlider.value,
            screenEffectsToggle.isOn
        );
        GameUserSettings.ApplyCanvasScale(canvasScaler, ReferenceResolution);
        ShowStatus("Настройки сохранены.");
        CloseSettings();
    }

    private void SelectPreviousResolution()
    {
        selectedResolutionIndex =
            (selectedResolutionIndex - 1 + resolutions.Count) %
            resolutions.Count;
        RefreshResolutionText();
    }

    private void SelectNextResolution()
    {
        selectedResolutionIndex =
            (selectedResolutionIndex + 1) % resolutions.Count;
        RefreshResolutionText();
    }

    private void ExitGame()
    {
        Time.timeScale = 1f;

#if UNITY_EDITOR
        Debug.Log("Exit requested from MainMenu. Play Mode will stop.");
        EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void BeginSceneLoad(string sceneName)
    {
        isLoading = true;
        SetMenuInteractable(false);
        StartCoroutine(SceneTransitionLoader.LoadSceneAsync(sceneName));
    }

    private void SetMenuInteractable(bool interactable)
    {
        foreach (Button button in GetComponentsInChildren<Button>(true))
        {
            button.interactable = interactable;
        }
    }

    private void PopulateSettingsControls()
    {
        resolutions = GameUserSettings.GetSupportedResolutions();
        Vector2Int savedResolution = new Vector2Int(
            GameUserSettings.ResolutionWidth,
            GameUserSettings.ResolutionHeight
        );
        selectedResolutionIndex = Mathf.Max(
            0,
            resolutions.FindIndex(item => item == savedResolution)
        );

        fullscreenToggle.isOn = GameUserSettings.Fullscreen;
        screenEffectsToggle.isOn = GameUserSettings.ScreenEffectsEnabled;
        volumeSlider.value = GameUserSettings.MasterVolume;
        uiScaleSlider.value = GameUserSettings.UIScale;
        RefreshResolutionText();
        RefreshSliderLabels();
    }

    private void RefreshResolutionText()
    {
        if (resolutionValueText == null || resolutions == null ||
            resolutions.Count == 0)
        {
            return;
        }

        Vector2Int resolution = resolutions[selectedResolutionIndex];
        resolutionValueText.text = $"{resolution.x} × {resolution.y}";
    }

    private void RefreshSliderLabels()
    {
        if (volumeValueText != null)
        {
            volumeValueText.text = $"{Mathf.RoundToInt(volumeSlider.value * 100f)}%";
        }

        if (uiScaleValueText != null)
        {
            uiScaleValueText.text = $"{Mathf.RoundToInt(uiScaleSlider.value * 100f)}%";
        }
    }

    private void ShowStatus(string message)
    {
        if (statusText != null)
        {
            statusText.text = message;
        }
    }

    private void BuildInterface()
    {
        runtimeFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        GameObject canvasObject = new GameObject(
            "MainMenuCanvas",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster)
        );
        canvasObject.transform.SetParent(transform, false);

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        canvasScaler = canvasObject.GetComponent<CanvasScaler>();
        canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasScaler.referenceResolution = ReferenceResolution;
        canvasScaler.screenMatchMode =
            CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        canvasScaler.matchWidthOrHeight = 0.5f;
        GameUserSettings.ApplyCanvasScale(canvasScaler, ReferenceResolution);

        GameObject root = CreateImage(
            "MainMenuRoot",
            canvasObject.transform,
            new Color(0.018f, 0.028f, 0.042f, 1f)
        );
        Stretch(root.GetComponent<RectTransform>());
        CreateAccentBands(root.transform);
        CreateMainPanel(root.transform);
        CreateSavePanel(root.transform);
        settingsOverlay = CreateSettingsOverlay(root.transform);
        newGameConfirmationOverlay =
            CreateNewGameConfirmation(root.transform);
    }

    private void CreateAccentBands(Transform parent)
    {
        GameObject topBand = CreateImage(
            "TopAccent",
            parent,
            new Color(0.15f, 0.85f, 0.68f, 1f)
        );
        RectTransform topRect = topBand.GetComponent<RectTransform>();
        topRect.anchorMin = new Vector2(0f, 1f);
        topRect.anchorMax = new Vector2(0.58f, 1f);
        topRect.pivot = new Vector2(0f, 1f);
        topRect.sizeDelta = new Vector2(0f, 7f);

        GameObject lowerBand = CreateImage(
            "LowerAccent",
            parent,
            new Color(0.92f, 0.25f, 0.48f, 1f)
        );
        RectTransform lowerRect = lowerBand.GetComponent<RectTransform>();
        lowerRect.anchorMin = new Vector2(0.72f, 0f);
        lowerRect.anchorMax = new Vector2(1f, 0f);
        lowerRect.pivot = new Vector2(1f, 0f);
        lowerRect.sizeDelta = new Vector2(0f, 7f);
    }

    private void CreateMainPanel(Transform parent)
    {
        GameObject panel = CreateImage(
            "MainActions",
            parent,
            new Color(0.025f, 0.045f, 0.06f, 0.94f)
        );
        RectTransform rect = panel.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.07f, 0.12f);
        rect.anchorMax = new Vector2(0.49f, 0.88f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        VerticalLayoutGroup layout = panel.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(44, 44, 38, 38);
        layout.spacing = 14f;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        CreateLabel(
            "GameTitle",
            panel.transform,
            "CYBER CLUB MANAGER",
            48,
            122f,
            FontStyle.Bold,
            TextAnchor.MiddleLeft,
            new Color(0.35f, 0.95f, 0.75f, 1f)
        );
        CreateLabel(
            "Subtitle",
            panel.transform,
            "Управление компьютерным клубом",
            23,
            48f,
            FontStyle.Normal,
            TextAnchor.MiddleLeft,
            new Color(0.82f, 0.88f, 0.92f, 1f)
        );
        CreateSpacer(panel.transform, 28f);

        continueButton = CreateButton(
            "ContinueButton", "ПРОДОЛЖИТЬ", panel.transform, ContinueGame
        );
        newGameButton = CreateButton(
            "NewGameButton", "НОВАЯ ИГРА", panel.transform, RequestNewGame
        );
        CreateButton(
            "SettingsButton", "НАСТРОЙКИ", panel.transform, OpenSettings
        );
        CreateButton("ExitButton", "ВЫХОД", panel.transform, ExitGame);

        statusText = CreateLabel(
            "StatusText",
            panel.transform,
            string.Empty,
            19,
            46f,
            FontStyle.Normal,
            TextAnchor.MiddleLeft,
            new Color(0.96f, 0.78f, 0.30f, 1f)
        );
    }

    private void CreateSavePanel(Transform parent)
    {
        GameObject panel = CreateImage(
            "SaveCard",
            parent,
            new Color(0.07f, 0.075f, 0.095f, 0.96f)
        );
        RectTransform rect = panel.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.55f, 0.24f);
        rect.anchorMax = new Vector2(0.91f, 0.76f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        VerticalLayoutGroup layout = panel.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(38, 38, 34, 34);
        layout.spacing = 16f;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        CreateLabel(
            "SaveTitle",
            panel.transform,
            "ТЕКУЩИЙ КЛУБ",
            27,
            54f,
            FontStyle.Bold,
            TextAnchor.MiddleLeft,
            Color.white
        );
        saveSummaryText = CreateLabel(
            "SaveSummary",
            panel.transform,
            string.Empty,
            23,
            250f,
            FontStyle.Normal,
            TextAnchor.UpperLeft,
            new Color(0.84f, 0.89f, 0.93f, 1f)
        );
        deleteCorruptedSaveButton = CreateButton(
            "DeleteCorruptedSaveButton",
            "УДАЛИТЬ СОХРАНЕНИЕ",
            panel.transform,
            DeleteCorruptedSave
        );
    }

    private GameObject CreateSettingsOverlay(Transform parent)
    {
        GameObject overlay = CreateOverlay("SettingsOverlay", parent);
        GameObject panel = CreateModalPanel(
            "SettingsPanel",
            overlay.transform,
            new Vector2(760f, 720f)
        );
        VerticalLayoutGroup layout = panel.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(38, 38, 30, 30);
        layout.spacing = 14f;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        CreateLabel(
            "SettingsTitle", panel.transform, "НАСТРОЙКИ", 32, 58f,
            FontStyle.Bold, TextAnchor.MiddleCenter, Color.white
        );
        fullscreenToggle = CreateToggleRow(
            "FullscreenToggle", "Полноэкранный режим", panel.transform
        );
        CreateResolutionRow(panel.transform);
        volumeSlider = CreateSliderRow(
            "MasterVolume", "Общая громкость", panel.transform,
            0f, 1f, out volumeValueText
        );
        uiScaleSlider = CreateSliderRow(
            "UIScale", "Масштаб интерфейса", panel.transform,
            0.75f, 1.5f, out uiScaleValueText
        );
        volumeSlider.onValueChanged.AddListener(_ => RefreshSliderLabels());
        uiScaleSlider.onValueChanged.AddListener(_ => RefreshSliderLabels());
        screenEffectsToggle = CreateToggleRow(
            "ScreenEffectsToggle", "Экранные эффекты", panel.transform
        );
        CreateSpacer(panel.transform, 12f);
        CreateButton(
            "ApplySettingsButton", "ПРИМЕНИТЬ", panel.transform, ApplySettings
        );
        CreateButton(
            "CancelSettingsButton", "НАЗАД", panel.transform, CloseSettings
        );

        overlay.SetActive(false);
        return overlay;
    }

    private GameObject CreateNewGameConfirmation(Transform parent)
    {
        GameObject overlay = CreateOverlay(
            "NewGameConfirmationOverlay",
            parent
        );
        GameObject panel = CreateModalPanel(
            "NewGameConfirmationPanel",
            overlay.transform,
            new Vector2(660f, 330f)
        );
        VerticalLayoutGroup layout = panel.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(36, 36, 30, 30);
        layout.spacing = 16f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        CreateLabel(
            "ConfirmationTitle", panel.transform, "НАЧАТЬ НОВУЮ ИГРУ?",
            29, 58f, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white
        );
        CreateLabel(
            "ConfirmationText",
            panel.transform,
            "Текущий прогресс будет удален.",
            22,
            62f,
            FontStyle.Normal,
            TextAnchor.MiddleCenter,
            new Color(0.92f, 0.76f, 0.34f, 1f)
        );
        CreateButton(
            "ConfirmNewGameButton", "НАЧАТЬ ЗАНОВО", panel.transform,
            StartNewGame
        );
        CreateButton(
            "CancelNewGameButton", "ОТМЕНА", panel.transform, CancelNewGame
        );

        overlay.SetActive(false);
        return overlay;
    }

    private void CreateResolutionRow(Transform parent)
    {
        GameObject row = CreateRow("ResolutionRow", parent, 70f);
        CreateLabel(
            "ResolutionLabel", row.transform, "Разрешение", 21, 60f,
            FontStyle.Normal, TextAnchor.MiddleLeft, Color.white
        );
        CreateCompactButton(
            "PreviousResolutionButton", "<", row.transform,
            SelectPreviousResolution
        );
        resolutionValueText = CreateLabel(
            "ResolutionValue", row.transform, string.Empty, 21, 60f,
            FontStyle.Bold, TextAnchor.MiddleCenter, Color.white
        );
        CreateCompactButton(
            "NextResolutionButton", ">", row.transform,
            SelectNextResolution
        );
    }

    private Toggle CreateToggleRow(
        string objectName,
        string label,
        Transform parent)
    {
        GameObject row = CreateRow(objectName + "Row", parent, 66f);
        CreateLabel(
            objectName + "Label", row.transform, label, 21, 58f,
            FontStyle.Normal, TextAnchor.MiddleLeft, Color.white
        );

        GameObject toggleObject = new GameObject(
            objectName,
            typeof(RectTransform),
            typeof(Image),
            typeof(Toggle),
            typeof(LayoutElement)
        );
        toggleObject.transform.SetParent(row.transform, false);
        LayoutElement layout = toggleObject.GetComponent<LayoutElement>();
        layout.preferredWidth = 58f;
        layout.preferredHeight = 34f;
        layout.flexibleWidth = 0f;
        Image background = toggleObject.GetComponent<Image>();
        background.color = new Color(0.12f, 0.15f, 0.19f, 1f);

        GameObject checkmarkObject = CreateImage(
            "Checkmark",
            toggleObject.transform,
            new Color(0.28f, 0.92f, 0.68f, 1f)
        );
        RectTransform checkmarkRect =
            checkmarkObject.GetComponent<RectTransform>();
        checkmarkRect.anchorMin = new Vector2(0.5f, 0.5f);
        checkmarkRect.anchorMax = new Vector2(0.5f, 0.5f);
        checkmarkRect.sizeDelta = new Vector2(34f, 20f);
        checkmarkRect.anchoredPosition = Vector2.zero;

        Toggle toggle = toggleObject.GetComponent<Toggle>();
        toggle.targetGraphic = background;
        toggle.graphic = checkmarkObject.GetComponent<Image>();
        return toggle;
    }

    private Slider CreateSliderRow(
        string objectName,
        string label,
        Transform parent,
        float minimum,
        float maximum,
        out Text valueText)
    {
        GameObject container = new GameObject(
            objectName + "Container",
            typeof(RectTransform),
            typeof(VerticalLayoutGroup),
            typeof(LayoutElement)
        );
        container.transform.SetParent(parent, false);
        container.GetComponent<LayoutElement>().preferredHeight = 98f;
        VerticalLayoutGroup vertical =
            container.GetComponent<VerticalLayoutGroup>();
        vertical.spacing = 4f;
        vertical.childControlHeight = true;
        vertical.childControlWidth = true;
        vertical.childForceExpandHeight = false;

        GameObject header = CreateRow(objectName + "Header", container.transform, 38f);
        CreateLabel(
            objectName + "Label", header.transform, label, 21, 36f,
            FontStyle.Normal, TextAnchor.MiddleLeft, Color.white
        );
        valueText = CreateLabel(
            objectName + "Value", header.transform, string.Empty, 20, 36f,
            FontStyle.Bold, TextAnchor.MiddleRight,
            new Color(0.35f, 0.95f, 0.75f, 1f)
        );

        GameObject sliderObject = new GameObject(
            objectName + "Slider",
            typeof(RectTransform),
            typeof(Slider),
            typeof(LayoutElement)
        );
        sliderObject.transform.SetParent(container.transform, false);
        sliderObject.GetComponent<LayoutElement>().preferredHeight = 42f;

        GameObject backgroundObject = CreateImage(
            "Background",
            sliderObject.transform,
            new Color(0.12f, 0.15f, 0.19f, 1f)
        );
        Stretch(backgroundObject.GetComponent<RectTransform>());
        backgroundObject.GetComponent<RectTransform>().offsetMin =
            new Vector2(0f, 15f);
        backgroundObject.GetComponent<RectTransform>().offsetMax =
            new Vector2(0f, -15f);

        GameObject fillArea = new GameObject("FillArea", typeof(RectTransform));
        fillArea.transform.SetParent(sliderObject.transform, false);
        RectTransform fillAreaRect = fillArea.GetComponent<RectTransform>();
        Stretch(fillAreaRect);
        fillAreaRect.offsetMin = new Vector2(4f, 15f);
        fillAreaRect.offsetMax = new Vector2(-10f, -15f);

        GameObject fillObject = CreateImage(
            "Fill",
            fillArea.transform,
            new Color(0.28f, 0.92f, 0.68f, 1f)
        );
        Stretch(fillObject.GetComponent<RectTransform>());

        GameObject handleArea = new GameObject(
            "HandleSlideArea",
            typeof(RectTransform)
        );
        handleArea.transform.SetParent(sliderObject.transform, false);
        RectTransform handleAreaRect = handleArea.GetComponent<RectTransform>();
        Stretch(handleAreaRect);
        handleAreaRect.offsetMin = new Vector2(10f, 0f);
        handleAreaRect.offsetMax = new Vector2(-10f, 0f);

        GameObject handleObject = CreateImage(
            "Handle",
            handleArea.transform,
            new Color(0.96f, 0.96f, 0.98f, 1f)
        );
        RectTransform handleRect = handleObject.GetComponent<RectTransform>();
        handleRect.sizeDelta = new Vector2(20f, 36f);

        Slider slider = sliderObject.GetComponent<Slider>();
        slider.minValue = minimum;
        slider.maxValue = maximum;
        slider.fillRect = fillObject.GetComponent<RectTransform>();
        slider.handleRect = handleRect;
        slider.targetGraphic = handleObject.GetComponent<Image>();
        return slider;
    }

    private GameObject CreateOverlay(string objectName, Transform parent)
    {
        GameObject overlay = CreateImage(
            objectName,
            parent,
            new Color(0f, 0f, 0f, 0.84f)
        );
        Stretch(overlay.GetComponent<RectTransform>());
        return overlay;
    }

    private GameObject CreateModalPanel(
        string objectName,
        Transform parent,
        Vector2 size)
    {
        GameObject panel = CreateImage(
            objectName,
            parent,
            new Color(0.035f, 0.05f, 0.065f, 1f)
        );
        RectTransform rect = panel.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = Vector2.zero;
        return panel;
    }

    private GameObject CreateRow(
        string objectName,
        Transform parent,
        float height)
    {
        GameObject row = new GameObject(
            objectName,
            typeof(RectTransform),
            typeof(HorizontalLayoutGroup),
            typeof(LayoutElement)
        );
        row.transform.SetParent(parent, false);
        row.GetComponent<LayoutElement>().preferredHeight = height;
        HorizontalLayoutGroup layout = row.GetComponent<HorizontalLayoutGroup>();
        layout.spacing = 12f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        return row;
    }

    private Button CreateButton(
        string objectName,
        string caption,
        Transform parent,
        UnityEngine.Events.UnityAction action)
    {
        GameObject buttonObject = CreateImage(
            objectName,
            parent,
            new Color(0.11f, 0.16f, 0.20f, 1f)
        );
        buttonObject.AddComponent<LayoutElement>().preferredHeight = 60f;
        Button button = buttonObject.AddComponent<Button>();
        button.targetGraphic = buttonObject.GetComponent<Image>();
        button.onClick.AddListener(action);

        ColorBlock colors = button.colors;
        colors.normalColor = new Color(0.11f, 0.16f, 0.20f, 1f);
        colors.highlightedColor = new Color(0.18f, 0.34f, 0.34f, 1f);
        colors.selectedColor = colors.highlightedColor;
        colors.pressedColor = new Color(0.08f, 0.12f, 0.15f, 1f);
        colors.disabledColor = new Color(0.07f, 0.08f, 0.09f, 0.6f);
        button.colors = colors;

        Text text = CreateLabel(
            "Text", buttonObject.transform, caption, 22, 56f,
            FontStyle.Bold, TextAnchor.MiddleCenter, Color.white
        );
        Stretch(text.rectTransform);
        return button;
    }

    private void CreateCompactButton(
        string objectName,
        string caption,
        Transform parent,
        UnityEngine.Events.UnityAction action)
    {
        Button button = CreateButton(objectName, caption, parent, action);
        LayoutElement layout = button.GetComponent<LayoutElement>();
        layout.preferredWidth = 56f;
        layout.flexibleWidth = 0f;
    }

    private Text CreateLabel(
        string objectName,
        Transform parent,
        string content,
        int fontSize,
        float preferredHeight,
        FontStyle fontStyle,
        TextAnchor alignment,
        Color color)
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
        text.alignment = alignment;
        text.color = color;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        text.raycastTarget = false;
        LayoutElement layout = textObject.GetComponent<LayoutElement>();
        layout.preferredHeight = preferredHeight;
        layout.flexibleWidth = 1f;
        return text;
    }

    private static GameObject CreateImage(
        string objectName,
        Transform parent,
        Color color)
    {
        GameObject imageObject = new GameObject(
            objectName,
            typeof(RectTransform),
            typeof(Image)
        );
        imageObject.transform.SetParent(parent, false);
        imageObject.GetComponent<Image>().color = color;
        return imageObject;
    }

    private static void CreateSpacer(Transform parent, float height)
    {
        GameObject spacer = new GameObject(
            "Spacer",
            typeof(RectTransform),
            typeof(LayoutElement)
        );
        spacer.transform.SetParent(parent, false);
        spacer.GetComponent<LayoutElement>().preferredHeight = height;
    }

    private static void Stretch(RectTransform rectTransform)
    {
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.anchoredPosition = Vector2.zero;
        rectTransform.sizeDelta = Vector2.zero;
    }

    private void EnsureEventSystem()
    {
        EventSystem eventSystem = EventSystem.current ??
            FindAnyObjectByType<EventSystem>();

        if (eventSystem == null)
        {
            GameObject eventSystemObject = new GameObject(
                "EventSystem",
                typeof(EventSystem)
            );
            eventSystem = eventSystemObject.GetComponent<EventSystem>();
        }

        if (eventSystem.GetComponent<InputSystemUIInputModule>() == null)
        {
            StandaloneInputModule legacy =
                eventSystem.GetComponent<StandaloneInputModule>();
            if (legacy != null)
            {
                legacy.enabled = false;
            }

            eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
        }
    }

    private void SelectButton(string objectName)
    {
        if (EventSystem.current == null)
        {
            return;
        }

        foreach (Button button in GetComponentsInChildren<Button>(true))
        {
            if (button.name == objectName && button.gameObject.activeInHierarchy)
            {
                EventSystem.current.SetSelectedGameObject(button.gameObject);
                return;
            }
        }
    }
}
