using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

public sealed class ClubRandomEventPanel : MonoBehaviour
{
    public static ClubRandomEventPanel Instance { get; private set; }

    private GameObject rootObject;
    private Text titleText;
    private Text messageText;
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
        if (ClubRandomEventManager.Instance != null)
        {
            ClubRandomEventManager.Instance.EventTriggered += OnEventTriggered;
        }
    }

    private void OnDestroy()
    {
        if (ClubRandomEventManager.Instance != null)
        {
            ClubRandomEventManager.Instance.EventTriggered -= OnEventTriggered;
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

    private void OnEventTriggered(ClubRandomEventType eventType, string message)
    {
        if (BankruptcyManager.Instance != null && BankruptcyManager.Instance.IsGameOver)
        {
            return;
        }

        Open(eventType, message);
    }

    private void Open(ClubRandomEventType eventType, string message)
    {
        if (isOpen)
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

        titleText.text = ClubRandomEventManager.GetEventDisplayName(eventType).ToUpperInvariant();
        messageText.text = BuildMessage(eventType, message);
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

    private static string BuildMessage(ClubRandomEventType eventType, string message)
    {
        string effect = eventType switch
        {
            ClubRandomEventType.VisitorRush =>
                "До конца дня клиенты будут приходить на 40% чаще.",
            ClubRandomEventType.InternetOutage =>
                "Новые игровые сессии временно недоступны. Клиенты продолжат ждать.",
            ClubRandomEventType.PowerSurge =>
                "Проверьте состояние компьютеров и выполните ремонт.",
            ClubRandomEventType.EquipmentInspection =>
                "Критически изношенная периферия учитывается в расходах дня.",
            ClubRandomEventType.ViralPost =>
                "Репутация повышена, а поток клиентов усилен до конца дня.",
            ClubRandomEventType.ElectricityPriceIncrease =>
                "Расходы на электричество в конце дня увеличатся в 1,5 раза.",
            _ => string.Empty
        };

        return string.IsNullOrWhiteSpace(effect)
            ? message
            : $"{message}\n\n{effect}";
    }

    private void BuildInterface()
    {
        runtimeFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        rootObject = new GameObject("ClubRandomEventPanelRoot", typeof(RectTransform), typeof(Image));
        rootObject.transform.SetParent(transform, false);
        Stretch(rootObject.GetComponent<RectTransform>());
        rootObject.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.78f);

        GameObject panel = new GameObject("ClubRandomEventPanel", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
        panel.transform.SetParent(rootObject.transform, false);
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(720f, 440f);
        panel.GetComponent<Image>().color = new Color(0.12f, 0.045f, 0.035f, 0.99f);

        VerticalLayoutGroup layout = panel.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(30, 30, 26, 26);
        layout.spacing = 14f;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        CreateLabel(panel.transform, "СЛУЧАЙНОЕ СОБЫТИЕ", 22, 38f, FontStyle.Normal);
        titleText = CreateLabel(panel.transform, string.Empty, 30, 58f, FontStyle.Bold);
        messageText = CreateLabel(panel.transform, string.Empty, 21, 190f, FontStyle.Normal);
        closeButton = CreateButton(panel.transform, "Понятно", Close);
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

    private Button CreateButton(
        Transform parent,
        string caption,
        UnityEngine.Events.UnityAction action)
    {
        GameObject buttonObject = new GameObject(caption, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        buttonObject.transform.SetParent(parent, false);
        Image image = buttonObject.GetComponent<Image>();
        image.color = new Color(0.58f, 0.18f, 0.10f, 1f);
        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(action);
        ColorBlock colors = button.colors;
        colors.normalColor = image.color;
        colors.highlightedColor = new Color(0.78f, 0.28f, 0.15f, 1f);
        colors.selectedColor = colors.highlightedColor;
        colors.pressedColor = new Color(0.34f, 0.09f, 0.05f, 1f);
        colors.colorMultiplier = 1f;
        button.colors = colors;
        buttonObject.GetComponent<LayoutElement>().preferredHeight = 58f;

        Text text = CreateLabel(buttonObject.transform, caption, 21, 58f, FontStyle.Bold);
        RectTransform textRect = text.GetComponent<RectTransform>();
        Stretch(textRect);
        textRect.offsetMin = new Vector2(10f, 3f);
        textRect.offsetMax = new Vector2(-10f, -3f);
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
