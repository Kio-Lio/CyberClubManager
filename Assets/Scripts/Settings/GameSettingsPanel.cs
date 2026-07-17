using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

public sealed class GameSettingsPanel : MonoBehaviour
{
    public static GameSettingsPanel Instance { get; private set; }

    private static readonly Vector2 ReferenceResolution = new(1920f, 1080f);
    private const float ConfirmationDuration = 10f;

    private GameObject canvasObject;
    private GameObject overlayRoot;
    private GameObject settingsPanel;
    private GameObject controlsPanel;
    private GameObject confirmationPanel;
    private Text resolutionValueText;
    private Text masterValueText;
    private Text musicValueText;
    private Text effectsValueText;
    private Text interfaceScaleValueText;
    private Text hudModeValueText;
    private Text confirmationText;
    private Text statusText;
    private Toggle fullscreenToggle;
    private Toggle verticalSyncToggle;
    private Slider masterSlider;
    private Slider musicSlider;
    private Slider effectsSlider;
    private Slider interfaceScaleSlider;
    private Button applyButton;
    private Button backButton;
    private Button controlsBackButton;
    private Button keepDisplayButton;
    private Font runtimeFont;
    private List<Vector2Int> resolutions = new();
    private int selectedResolutionIndex;
    private ClubHUDMode selectedDefaultHUDMode;
    private bool isOpen;
    private bool isPopulating;
    private float confirmationRemaining;
    private Action closedCallback;
    private int lastBackFrame = -1;

    public bool IsOpen => isOpen;
    public bool IsControlsOpen => controlsPanel != null && controlsPanel.activeSelf;
    public bool IsDisplayConfirmationOpen =>
        confirmationPanel != null && confirmationPanel.activeSelf;
    public float ConfirmationSecondsRemaining => confirmationRemaining;

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
        BuildInterface();
        SetVisible(false);
    }

    private void Update()
    {
        if (!isOpen)
        {
            return;
        }

        if (IsDisplayConfirmationOpen)
        {
            confirmationRemaining -= Time.unscaledDeltaTime;
            RefreshConfirmationText();
            if (confirmationRemaining <= 0f)
            {
                RevertDisplaySettings();
            }
        }

        bool backPressed = Keyboard.current?.escapeKey.wasPressedThisFrame == true ||
            Gamepad.current?.buttonEast.wasPressedThisFrame == true;
        if (backPressed && lastBackFrame != Time.frameCount)
        {
            HandleBack();
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public void Open(Action onClosed = null)
    {
        if (isOpen || GameSettingsManager.Instance == null)
        {
            return;
        }

        closedCallback = onClosed;
        isOpen = true;
        PopulateControls();
        settingsPanel.SetActive(true);
        controlsPanel.SetActive(false);
        confirmationPanel.SetActive(false);
        SetVisible(true);
        EnsureEventSystem();
        SelectButton(applyButton);
    }

    public void Close()
    {
        if (!isOpen)
        {
            return;
        }

        if (IsDisplayConfirmationOpen)
        {
            RevertDisplaySettings();
        }

        isOpen = false;
        SetVisible(false);
        EventSystem.current?.SetSelectedGameObject(null);
        Action callback = closedCallback;
        closedCallback = null;
        callback?.Invoke();
    }

    public void HandleBack()
    {
        if (!isOpen)
        {
            return;
        }

        lastBackFrame = Time.frameCount;
        if (IsDisplayConfirmationOpen)
        {
            RevertDisplaySettings();
            return;
        }

        if (IsControlsOpen)
        {
            ShowSettingsScreen();
            return;
        }

        Close();
    }

    public void ApplyPendingDisplaySettings()
    {
        if (resolutions.Count == 0)
        {
            return;
        }

        Vector2Int resolution = resolutions[selectedResolutionIndex];
        RefreshRate refreshRate = GetSelectedRefreshRate(resolution);
        GameSettingsManager.Instance.PreviewDisplayMode(
            resolution.x,
            resolution.y,
            refreshRate,
            fullscreenToggle.isOn,
            verticalSyncToggle.isOn
        );
        confirmationRemaining = ConfirmationDuration;
        confirmationPanel.SetActive(true);
        confirmationPanel.transform.SetAsLastSibling();
        RefreshConfirmationText();
        SelectButton(keepDisplayButton);
    }

    public void ConfirmDisplaySettings()
    {
        if (!IsDisplayConfirmationOpen || resolutions.Count == 0)
        {
            return;
        }

        Vector2Int resolution = resolutions[selectedResolutionIndex];
        GameSettingsManager.Instance.ConfirmDisplaySettings(
            resolution.x,
            resolution.y,
            GetSelectedRefreshRate(resolution),
            fullscreenToggle.isOn,
            verticalSyncToggle.isOn
        );
        confirmationPanel.SetActive(false);
        statusText.text = "Параметры экрана сохранены.";
        SelectButton(applyButton);
    }

    public void RevertDisplaySettings()
    {
        if (!IsDisplayConfirmationOpen)
        {
            return;
        }

        GameSettingsManager.Instance.RestoreDisplayPreview();
        confirmationPanel.SetActive(false);
        PopulateDisplayControls();
        statusText.text = "Предыдущие параметры экрана восстановлены.";
        SelectButton(applyButton);
    }

    public void RestoreDefaults()
    {
        GameSettingsManager.Instance.RestoreDefaults();
        PopulateControls();
        statusText.text = "Настройки по умолчанию восстановлены.";
    }

    public void ShowControlsScreen()
    {
        settingsPanel.SetActive(false);
        controlsPanel.SetActive(true);
        SelectButton(controlsBackButton);
    }

    public void ShowSettingsScreen()
    {
        controlsPanel.SetActive(false);
        settingsPanel.SetActive(true);
        SelectButton(applyButton);
    }

    public void ResetInputBindings()
    {
        GameSettingsManager.Instance.ResetInputBindings();
        statusText.text = "Стандартные назначения восстановлены.";
    }

    private void PopulateControls()
    {
        isPopulating = true;
        PopulateDisplayControls();

        GameSettingsData data = GameSettingsManager.Instance.Settings;
        masterSlider.value = data.masterVolume;
        musicSlider.value = data.musicVolume;
        effectsSlider.value = data.effectsVolume;
        interfaceScaleSlider.value = data.interfaceScale;
        selectedDefaultHUDMode = data.defaultHUDMode;
        RefreshImmediateValueLabels();
        statusText.text = string.Empty;
        isPopulating = false;
    }

    private void PopulateDisplayControls()
    {
        GameSettingsData data = GameSettingsManager.Instance.Settings;
        resolutions = GameSettingsManager.Instance.GetSupportedResolutions();
        Vector2Int savedResolution = new(
            data.resolutionWidth,
            data.resolutionHeight
        );
        selectedResolutionIndex = Mathf.Max(
            0,
            resolutions.FindIndex(item => item == savedResolution)
        );
        fullscreenToggle.isOn = data.fullscreen;
        verticalSyncToggle.isOn = data.verticalSync;
        RefreshResolutionText();
    }

    private void SelectPreviousResolution()
    {
        selectedResolutionIndex =
            (selectedResolutionIndex - 1 + resolutions.Count) % resolutions.Count;
        RefreshResolutionText();
    }

    private void SelectNextResolution()
    {
        selectedResolutionIndex = (selectedResolutionIndex + 1) % resolutions.Count;
        RefreshResolutionText();
    }

    private void SelectPreviousHUDMode()
    {
        selectedDefaultHUDMode = selectedDefaultHUDMode switch
        {
            ClubHUDMode.Compact => ClubHUDMode.Hidden,
            ClubHUDMode.Expanded => ClubHUDMode.Compact,
            _ => ClubHUDMode.Expanded
        };
        GameSettingsManager.Instance.SetDefaultHUDMode(selectedDefaultHUDMode);
        RefreshImmediateValueLabels();
    }

    private void SelectNextHUDMode()
    {
        selectedDefaultHUDMode = selectedDefaultHUDMode switch
        {
            ClubHUDMode.Compact => ClubHUDMode.Expanded,
            ClubHUDMode.Expanded => ClubHUDMode.Hidden,
            _ => ClubHUDMode.Compact
        };
        GameSettingsManager.Instance.SetDefaultHUDMode(selectedDefaultHUDMode);
        RefreshImmediateValueLabels();
    }

    private void OnMasterVolumeChanged(float value)
    {
        if (!isPopulating)
            GameSettingsManager.Instance.SetMasterVolume(value);
        RefreshImmediateValueLabels();
    }

    private void OnMusicVolumeChanged(float value)
    {
        if (!isPopulating)
            GameSettingsManager.Instance.SetMusicVolume(value);
        RefreshImmediateValueLabels();
    }

    private void OnEffectsVolumeChanged(float value)
    {
        if (!isPopulating)
            GameSettingsManager.Instance.SetEffectsVolume(value);
        RefreshImmediateValueLabels();
    }

    private void OnInterfaceScaleChanged(float value)
    {
        float normalized = Mathf.Round(value * 10f) / 10f;
        if (!isPopulating)
            GameSettingsManager.Instance.SetInterfaceScale(normalized);
        RefreshImmediateValueLabels();
    }

    private void RefreshResolutionText()
    {
        if (resolutions.Count == 0)
        {
            resolutionValueText.text = "—";
            return;
        }

        Vector2Int resolution = resolutions[selectedResolutionIndex];
        resolutionValueText.text = $"{resolution.x} × {resolution.y}";
    }

    private void RefreshImmediateValueLabels()
    {
        if (masterValueText == null)
        {
            return;
        }

        masterValueText.text = $"{Mathf.RoundToInt(masterSlider.value * 100f)}%";
        musicValueText.text = $"{Mathf.RoundToInt(musicSlider.value * 100f)}%";
        effectsValueText.text = $"{Mathf.RoundToInt(effectsSlider.value * 100f)}%";
        interfaceScaleValueText.text =
            $"{Mathf.RoundToInt(interfaceScaleSlider.value * 100f)}%";
        hudModeValueText.text = selectedDefaultHUDMode.ToString();
    }

    private void RefreshConfirmationText()
    {
        confirmationText.text =
            "Сохранить новые параметры экрана?\n\n" +
            $"Автоматический возврат через {Mathf.CeilToInt(confirmationRemaining)} сек.";
    }

    private RefreshRate GetSelectedRefreshRate(Vector2Int resolution)
    {
        GameSettingsData data = GameSettingsManager.Instance.Settings;
        foreach (Resolution available in Screen.resolutions)
        {
            if (available.width == resolution.x && available.height == resolution.y)
            {
                return available.refreshRateRatio;
            }
        }

        return new RefreshRate
        {
            numerator = (uint)Mathf.Max(1, data.refreshRateNumerator),
            denominator = (uint)Mathf.Max(1, data.refreshRateDenominator)
        };
    }

    private void BuildInterface()
    {
        runtimeFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        canvasObject = new GameObject(
            "GameSettingsCanvas",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster)
        );
        canvasObject.transform.SetParent(transform, false);

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 2100;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = ReferenceResolution;
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        overlayRoot = CreateImage(
            "GameSettingsOverlay",
            canvasObject.transform,
            new Color(0f, 0f, 0f, 0.86f)
        );
        Stretch(overlayRoot.GetComponent<RectTransform>());
        settingsPanel = CreateSettingsScreen(overlayRoot.transform);
        controlsPanel = CreateControlsScreen(overlayRoot.transform);
        confirmationPanel = CreateDisplayConfirmation(overlayRoot.transform);
    }

    private GameObject CreateSettingsScreen(Transform parent)
    {
        GameObject panel = CreatePanel("SettingsPanel", parent, new Vector2(860f, 820f));
        panel.AddComponent<ScalableUIRoot>();
        VerticalLayoutGroup layout = panel.AddComponent<VerticalLayoutGroup>();
        ConfigureVertical(layout, new RectOffset(34, 34, 24, 24), 5f);

        CreateLabel("Title", panel.transform, "НАСТРОЙКИ", 30, 48f, FontStyle.Bold);
        CreateSectionLabel(panel.transform, "ЭКРАН");
        CreateResolutionRow(panel.transform);
        fullscreenToggle = CreateToggleRow(panel.transform, "Полный экран");
        verticalSyncToggle = CreateToggleRow(
            panel.transform,
            "Вертикальная синхронизация"
        );

        CreateSectionLabel(panel.transform, "ЗВУК");
        masterSlider = CreateSliderRow(
            panel.transform,
            "Общая громкость",
            out masterValueText
        );
        musicSlider = CreateSliderRow(panel.transform, "Музыка", out musicValueText);
        effectsSlider = CreateSliderRow(panel.transform, "Эффекты", out effectsValueText);
        masterSlider.onValueChanged.AddListener(OnMasterVolumeChanged);
        musicSlider.onValueChanged.AddListener(OnMusicVolumeChanged);
        effectsSlider.onValueChanged.AddListener(OnEffectsVolumeChanged);

        CreateSectionLabel(panel.transform, "ИНТЕРФЕЙС");
        interfaceScaleSlider = CreateSliderRow(
            panel.transform,
            "Масштаб",
            out interfaceScaleValueText,
            0.8f,
            1.2f
        );
        interfaceScaleSlider.onValueChanged.AddListener(OnInterfaceScaleChanged);
        CreateHUDModeRow(panel.transform);

        GameObject utilityRow = CreateRow("UtilityButtons", panel.transform, 48f);
        CreateButton("ControlsButton", "УПРАВЛЕНИЕ", utilityRow.transform, ShowControlsScreen);
        CreateButton("DefaultsButton", "ПО УМОЛЧАНИЮ", utilityRow.transform, RestoreDefaults);

        GameObject actionRow = CreateRow("ActionButtons", panel.transform, 48f);
        applyButton = CreateButton(
            "ApplySettingsButton",
            "ПРИМЕНИТЬ",
            actionRow.transform,
            ApplyPendingDisplaySettings
        );
        backButton = CreateButton("BackButton", "НАЗАД", actionRow.transform, Close);
        statusText = CreateLabel(
            "StatusText",
            panel.transform,
            string.Empty,
            17,
            30f,
            FontStyle.Normal,
            new Color(1f, 0.78f, 0.34f)
        );
        return panel;
    }

    private GameObject CreateControlsScreen(Transform parent)
    {
        GameObject panel = CreatePanel("ControlsPanel", parent, new Vector2(760f, 650f));
        panel.AddComponent<ScalableUIRoot>();
        VerticalLayoutGroup layout = panel.AddComponent<VerticalLayoutGroup>();
        ConfigureVertical(layout, new RectOffset(38, 38, 30, 30), 10f);
        CreateLabel("Title", panel.transform, "УПРАВЛЕНИЕ", 30, 58f, FontStyle.Bold);
        CreateControlLine(panel.transform, "Движение", "WASD / левый стик");
        CreateControlLine(panel.transform, "Взаимодействие", "E / South Button");
        CreateControlLine(panel.transform, "Пауза", "Esc / Start");
        CreateControlLine(panel.transform, "HUD", "Tab / Select");
        CreateControlLine(panel.transform, "Масштаб камеры", "колесо / правый стик");
        CreateSpacer(panel.transform, 18f);
        CreateButton(
            "ResetBindingsButton",
            "ВОССТАНОВИТЬ СТАНДАРТНЫЕ НАЗНАЧЕНИЯ",
            panel.transform,
            ResetInputBindings
        );
        controlsBackButton = CreateButton(
            "ControlsBackButton",
            "НАЗАД",
            panel.transform,
            ShowSettingsScreen
        );
        panel.SetActive(false);
        return panel;
    }

    private GameObject CreateDisplayConfirmation(Transform parent)
    {
        GameObject panel = CreatePanel(
            "DisplayConfirmationPanel",
            parent,
            new Vector2(650f, 330f)
        );
        panel.AddComponent<ScalableUIRoot>();
        VerticalLayoutGroup layout = panel.AddComponent<VerticalLayoutGroup>();
        ConfigureVertical(layout, new RectOffset(36, 36, 30, 30), 16f);
        confirmationText = CreateLabel(
            "ConfirmationText",
            panel.transform,
            string.Empty,
            23,
            150f,
            FontStyle.Bold
        );
        GameObject row = CreateRow("ConfirmationButtons", panel.transform, 58f);
        keepDisplayButton = CreateButton(
            "KeepDisplayButton",
            "СОХРАНИТЬ",
            row.transform,
            ConfirmDisplaySettings
        );
        CreateButton("RevertDisplayButton", "ВЕРНУТЬ", row.transform, RevertDisplaySettings);
        panel.SetActive(false);
        return panel;
    }

    private void CreateResolutionRow(Transform parent)
    {
        GameObject row = CreateRow("ResolutionRow", parent, 46f);
        CreateLabel("Label", row.transform, "Разрешение", 19, 42f, FontStyle.Normal);
        CreateCompactButton("Previous", "<", row.transform, SelectPreviousResolution);
        resolutionValueText = CreateLabel(
            "Value",
            row.transform,
            string.Empty,
            19,
            42f,
            FontStyle.Bold
        );
        CreateCompactButton("Next", ">", row.transform, SelectNextResolution);
    }

    private void CreateHUDModeRow(Transform parent)
    {
        GameObject row = CreateRow("HUDModeRow", parent, 46f);
        CreateLabel("Label", row.transform, "HUD новой игры", 19, 42f, FontStyle.Normal);
        CreateCompactButton("Previous", "<", row.transform, SelectPreviousHUDMode);
        hudModeValueText = CreateLabel(
            "Value",
            row.transform,
            string.Empty,
            19,
            42f,
            FontStyle.Bold
        );
        CreateCompactButton("Next", ">", row.transform, SelectNextHUDMode);
    }

    private Toggle CreateToggleRow(Transform parent, string label)
    {
        GameObject row = CreateRow(label + "Row", parent, 46f);
        CreateLabel("Label", row.transform, label, 19, 42f, FontStyle.Normal);
        GameObject toggleObject = CreateImage(
            "Toggle",
            row.transform,
            new Color(0.12f, 0.16f, 0.20f, 1f)
        );
        toggleObject.AddComponent<LayoutElement>().preferredWidth = 100f;
        Toggle toggle = toggleObject.AddComponent<Toggle>();
        toggle.targetGraphic = toggleObject.GetComponent<Image>();
        GameObject checkmarkObject = CreateImage(
            "Checkmark",
            toggleObject.transform,
            new Color(0.22f, 0.82f, 0.65f, 1f)
        );
        RectTransform checkmark = checkmarkObject.GetComponent<RectTransform>();
        checkmark.anchorMin = new Vector2(0.06f, 0.2f);
        checkmark.anchorMax = new Vector2(0.94f, 0.8f);
        checkmark.offsetMin = Vector2.zero;
        checkmark.offsetMax = Vector2.zero;
        toggle.graphic = checkmarkObject.GetComponent<Image>();
        return toggle;
    }

    private Slider CreateSliderRow(
        Transform parent,
        string label,
        out Text valueText,
        float minimum = 0f,
        float maximum = 1f)
    {
        GameObject row = CreateRow(label + "Row", parent, 58f);
        CreateLabel("Label", row.transform, label, 19, 52f, FontStyle.Normal);
        Slider slider = CreateSlider(row.transform, minimum, maximum);
        valueText = CreateLabel(
            "Value",
            row.transform,
            string.Empty,
            18,
            52f,
            FontStyle.Bold
        );
        valueText.GetComponent<LayoutElement>().preferredWidth = 64f;
        valueText.GetComponent<LayoutElement>().flexibleWidth = 0f;
        return slider;
    }

    private Slider CreateSlider(Transform parent, float minimum, float maximum)
    {
        GameObject sliderObject = new(
            "Slider",
            typeof(RectTransform),
            typeof(Slider),
            typeof(LayoutElement)
        );
        sliderObject.transform.SetParent(parent, false);
        LayoutElement element = sliderObject.GetComponent<LayoutElement>();
        element.preferredWidth = 330f;
        element.preferredHeight = 44f;

        GameObject background = CreateImage(
            "Background",
            sliderObject.transform,
            new Color(0.11f, 0.15f, 0.18f, 1f)
        );
        Stretch(background.GetComponent<RectTransform>());
        background.GetComponent<RectTransform>().offsetMin = new Vector2(0f, 16f);
        background.GetComponent<RectTransform>().offsetMax = new Vector2(0f, -16f);

        GameObject fill = CreateImage(
            "Fill",
            sliderObject.transform,
            new Color(0.24f, 0.86f, 0.67f, 1f)
        );
        Stretch(fill.GetComponent<RectTransform>());
        fill.GetComponent<RectTransform>().offsetMin = new Vector2(2f, 16f);
        fill.GetComponent<RectTransform>().offsetMax = new Vector2(-8f, -16f);

        GameObject handleArea = new("HandleArea", typeof(RectTransform));
        handleArea.transform.SetParent(sliderObject.transform, false);
        Stretch(handleArea.GetComponent<RectTransform>());
        handleArea.GetComponent<RectTransform>().offsetMin = new Vector2(8f, 0f);
        handleArea.GetComponent<RectTransform>().offsetMax = new Vector2(-8f, 0f);
        GameObject handle = CreateImage("Handle", handleArea.transform, Color.white);
        handle.GetComponent<RectTransform>().sizeDelta = new Vector2(18f, 34f);

        Slider slider = sliderObject.GetComponent<Slider>();
        slider.minValue = minimum;
        slider.maxValue = maximum;
        slider.fillRect = fill.GetComponent<RectTransform>();
        slider.handleRect = handle.GetComponent<RectTransform>();
        slider.targetGraphic = handle.GetComponent<Image>();
        return slider;
    }

    private void CreateControlLine(Transform parent, string action, string binding)
    {
        GameObject row = CreateRow(action + "Row", parent, 54f);
        CreateLabel("Action", row.transform, action, 20, 50f, FontStyle.Bold);
        CreateLabel("Binding", row.transform, binding, 20, 50f, FontStyle.Normal);
    }

    private void CreateSectionLabel(Transform parent, string text)
    {
        CreateLabel(
            text + "Header",
            parent,
            text,
            18,
            28f,
            FontStyle.Bold,
            new Color(0.34f, 0.94f, 0.74f)
        );
    }

    private GameObject CreatePanel(string name, Transform parent, Vector2 size)
    {
        GameObject panel = CreateImage(
            name,
            parent,
            new Color(0.03f, 0.045f, 0.06f, 1f)
        );
        RectTransform rect = panel.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = size;
        return panel;
    }

    private GameObject CreateRow(string name, Transform parent, float height)
    {
        GameObject row = new(
            name,
            typeof(RectTransform),
            typeof(HorizontalLayoutGroup),
            typeof(LayoutElement)
        );
        row.transform.SetParent(parent, false);
        row.GetComponent<LayoutElement>().preferredHeight = height;
        HorizontalLayoutGroup layout = row.GetComponent<HorizontalLayoutGroup>();
        layout.spacing = 10f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        return row;
    }

    private Button CreateButton(
        string name,
        string caption,
        Transform parent,
        UnityEngine.Events.UnityAction action)
    {
        GameObject buttonObject = CreateImage(
            name,
            parent,
            new Color(0.10f, 0.16f, 0.20f, 1f)
        );
        buttonObject.AddComponent<LayoutElement>().preferredHeight = 46f;
        Button button = buttonObject.AddComponent<Button>();
        button.targetGraphic = buttonObject.GetComponent<Image>();
        button.onClick.AddListener(action);
        Text text = CreateLabel(
            "Text",
            buttonObject.transform,
            caption,
            18,
            42f,
            FontStyle.Bold
        );
        Stretch(text.rectTransform);
        return button;
    }

    private void CreateCompactButton(
        string name,
        string caption,
        Transform parent,
        UnityEngine.Events.UnityAction action)
    {
        Button button = CreateButton(name, caption, parent, action);
        LayoutElement element = button.GetComponent<LayoutElement>();
        element.preferredWidth = 48f;
        element.flexibleWidth = 0f;
    }

    private Text CreateLabel(
        string name,
        Transform parent,
        string content,
        int fontSize,
        float height,
        FontStyle style,
        Color? color = null)
    {
        GameObject textObject = new(
            name,
            typeof(RectTransform),
            typeof(Text),
            typeof(LayoutElement)
        );
        textObject.transform.SetParent(parent, false);
        Text text = textObject.GetComponent<Text>();
        text.font = runtimeFont;
        text.text = content;
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.alignment = TextAnchor.MiddleLeft;
        text.color = color ?? Color.white;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        text.raycastTarget = false;
        LayoutElement element = textObject.GetComponent<LayoutElement>();
        element.preferredHeight = height;
        element.flexibleWidth = 1f;
        return text;
    }

    private static GameObject CreateImage(string name, Transform parent, Color color)
    {
        GameObject image = new(name, typeof(RectTransform), typeof(Image));
        image.transform.SetParent(parent, false);
        image.GetComponent<Image>().color = color;
        return image;
    }

    private static void CreateSpacer(Transform parent, float height)
    {
        GameObject spacer = new("Spacer", typeof(RectTransform), typeof(LayoutElement));
        spacer.transform.SetParent(parent, false);
        spacer.GetComponent<LayoutElement>().preferredHeight = height;
    }

    private static void ConfigureVertical(
        VerticalLayoutGroup layout,
        RectOffset padding,
        float spacing)
    {
        layout.padding = padding;
        layout.spacing = spacing;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
    }

    private void SetVisible(bool visible)
    {
        if (overlayRoot != null)
        {
            overlayRoot.SetActive(visible);
        }
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = Vector2.zero;
    }

    private static void SelectButton(Button button)
    {
        if (EventSystem.current == null || button == null ||
            !button.gameObject.activeInHierarchy)
        {
            return;
        }

        EventSystem.current.SetSelectedGameObject(button.gameObject);
    }

    private static void EnsureEventSystem()
    {
        EventSystem eventSystem = EventSystem.current ??
            FindAnyObjectByType<EventSystem>();
        if (eventSystem == null)
        {
            eventSystem = new GameObject("EventSystem", typeof(EventSystem))
                .GetComponent<EventSystem>();
        }

        if (eventSystem.GetComponent<InputSystemUIInputModule>() == null)
        {
            eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
        }
    }
}
