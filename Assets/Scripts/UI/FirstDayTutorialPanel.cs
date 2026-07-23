using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public sealed class FirstDayTutorialPanel : MonoBehaviour
{
    public static FirstDayTutorialPanel Instance { get; private set; }

    private GameObject rootObject;
    private GameObject bodyObject;
    private RectTransform panelRect;
    private Image panelImage;
    private Text titleText;
    private Text descriptionText;
    private Text objectiveText;
    private Button collapseButton;
    private Text collapseButtonText;
    private Button skipButton;
    private Text skipButtonText;
    private Font runtimeFont;
    private bool collapsed;
    private bool skipConfirmationPending;
    private Coroutine highlightCoroutine;

    public bool IsCollapsed => collapsed;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;
        BuildInterface();
    }

    private void Start()
    {
        if (FirstDayTutorialManager.Instance != null)
        {
            FirstDayTutorialManager.Instance.StepChanged += OnStepChanged;
            FirstDayTutorialManager.Instance.TutorialCompleted += RefreshView;
        }
        RefreshView();
    }

    private void OnDestroy()
    {
        if (FirstDayTutorialManager.Instance != null)
        {
            FirstDayTutorialManager.Instance.StepChanged -= OnStepChanged;
            FirstDayTutorialManager.Instance.TutorialCompleted -= RefreshView;
        }
        if (Instance == this) Instance = null;
    }

    private void OnStepChanged()
    {
        skipConfirmationPending = false;
        RefreshView();
        if (highlightCoroutine != null) StopCoroutine(highlightCoroutine);
        if (rootObject.activeSelf) highlightCoroutine = StartCoroutine(HighlightPanel());
    }

    private void RefreshView()
    {
        FirstDayTutorialManager manager = FirstDayTutorialManager.Instance;
        bool visible = manager != null && manager.IsTutorialActive && manager.CurrentStep != null;
        rootObject.SetActive(visible);
        if (!visible) return;

        TutorialStepDefinition step = manager.CurrentStep;
        titleText.text = step.Title;
        descriptionText.text = step.Description;
        string progress = step.RequiredProgress > 1
            ? $" ({manager.CurrentProgress}/{step.RequiredProgress})" : string.Empty;
        objectiveText.text = $"Цель: {step.ObjectiveText}{progress}";
        skipButtonText.text = skipConfirmationPending
            ? "Подтвердить пропуск" : "Пропустить";
    }

    private void ToggleCollapsed()
    {
        collapsed = !collapsed;
        bodyObject.SetActive(!collapsed);
        panelRect.sizeDelta = new Vector2(440f, collapsed ? 56f : 238f);
        collapseButtonText.text = collapsed ? "+" : "−";
    }

    private void RequestSkip()
    {
        if (!skipConfirmationPending)
        {
            skipConfirmationPending = true;
            skipButtonText.text = "Подтвердить пропуск";
            return;
        }
        FirstDayTutorialManager.Instance?.SkipTutorial();
    }

    private IEnumerator HighlightPanel()
    {
        Color normal = new Color(0.035f, 0.08f, 0.09f, 0.96f);
        Color highlight = new Color(0.12f, 0.32f, 0.22f, 0.98f);
        float elapsed = 0f;
        while (elapsed < 0.8f)
        {
            elapsed += Time.unscaledDeltaTime;
            float pulse = Mathf.Sin(elapsed / 0.8f * Mathf.PI);
            panelImage.color = Color.Lerp(normal, highlight, pulse);
            yield return null;
        }
        panelImage.color = normal;
        highlightCoroutine = null;
    }

    private void BuildInterface()
    {
        runtimeFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        rootObject = new GameObject("FirstDayTutorialPanelRoot", typeof(RectTransform),
            typeof(Image), typeof(VerticalLayoutGroup));
        rootObject.transform.SetParent(transform, false);
        panelRect = rootObject.GetComponent<RectTransform>();
        panelRect.anchorMin = panelRect.anchorMax = panelRect.pivot = new Vector2(0f, 0f);
        panelRect.anchoredPosition = new Vector2(20f, 92f);
        panelRect.sizeDelta = new Vector2(440f, 238f);
        panelImage = rootObject.GetComponent<Image>();
        panelImage.color = new Color(0.035f, 0.08f, 0.09f, 0.96f);

        VerticalLayoutGroup rootLayout = rootObject.GetComponent<VerticalLayoutGroup>();
        rootLayout.padding = new RectOffset(16, 16, 10, 10);
        rootLayout.spacing = 6f;
        rootLayout.childControlWidth = true;
        rootLayout.childControlHeight = true;
        rootLayout.childForceExpandWidth = true;
        rootLayout.childForceExpandHeight = false;

        GameObject header = new GameObject("Header", typeof(RectTransform),
            typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        header.transform.SetParent(rootObject.transform, false);
        header.GetComponent<LayoutElement>().preferredHeight = 36f;
        HorizontalLayoutGroup headerLayout = header.GetComponent<HorizontalLayoutGroup>();
        headerLayout.spacing = 8f;
        headerLayout.childControlWidth = true;
        headerLayout.childControlHeight = true;
        headerLayout.childForceExpandWidth = false;
        headerLayout.childForceExpandHeight = true;

        Text heading = CreateLabel(header.transform, "ОБУЧЕНИЕ", 19, 36f,
            FontStyle.Bold, TextAnchor.MiddleLeft);
        LayoutElement headingLayout = heading.GetComponent<LayoutElement>();
        headingLayout.flexibleWidth = 1f;
        collapseButton = CreateButton(header.transform, "−", ToggleCollapsed, 36f, 44f);
        collapseButtonText = collapseButton.GetComponentInChildren<Text>();

        bodyObject = new GameObject("Body", typeof(RectTransform),
            typeof(VerticalLayoutGroup), typeof(LayoutElement));
        bodyObject.transform.SetParent(rootObject.transform, false);
        bodyObject.GetComponent<LayoutElement>().preferredHeight = 174f;
        VerticalLayoutGroup bodyLayout = bodyObject.GetComponent<VerticalLayoutGroup>();
        bodyLayout.spacing = 8f;
        bodyLayout.childControlWidth = true;
        bodyLayout.childControlHeight = true;
        bodyLayout.childForceExpandWidth = true;
        bodyLayout.childForceExpandHeight = false;

        titleText = CreateLabel(bodyObject.transform, string.Empty, 18, 30f,
            FontStyle.Bold, TextAnchor.MiddleLeft);
        descriptionText = CreateLabel(bodyObject.transform, string.Empty, 15, 52f,
            FontStyle.Normal, TextAnchor.UpperLeft);
        objectiveText = CreateLabel(bodyObject.transform, string.Empty, 16, 32f,
            FontStyle.Bold, TextAnchor.MiddleLeft);
        skipButton = CreateButton(bodyObject.transform, "Пропустить", RequestSkip, 40f, 180f);
        skipButtonText = skipButton.GetComponentInChildren<Text>();
        rootObject.SetActive(false);
    }

    private Text CreateLabel(Transform parent, string content, int size, float height,
        FontStyle style, TextAnchor alignment)
    {
        GameObject label = new GameObject("Text", typeof(RectTransform), typeof(Text),
            typeof(LayoutElement));
        label.transform.SetParent(parent, false);
        Text text = label.GetComponent<Text>();
        text.font = runtimeFont;
        text.text = content;
        text.fontSize = size;
        text.fontStyle = style;
        text.color = Color.white;
        text.alignment = alignment;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;
        label.GetComponent<LayoutElement>().preferredHeight = height;
        return text;
    }

    private Button CreateButton(Transform parent, string caption,
        UnityEngine.Events.UnityAction action, float height, float width)
    {
        GameObject buttonObject = new GameObject(caption, typeof(RectTransform),
            typeof(Image), typeof(Button), typeof(LayoutElement));
        buttonObject.transform.SetParent(parent, false);
        Image image = buttonObject.GetComponent<Image>();
        image.color = new Color(0.12f, 0.42f, 0.30f, 1f);
        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(action);
        LayoutElement element = buttonObject.GetComponent<LayoutElement>();
        element.preferredHeight = height;
        element.preferredWidth = width;
        element.flexibleWidth = 0f;
        Text text = CreateLabel(buttonObject.transform, caption, 17, height,
            FontStyle.Bold, TextAnchor.MiddleCenter);
        RectTransform textRect = text.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(8f, 2f);
        textRect.offsetMax = new Vector2(-8f, -2f);
        return button;
    }
}
