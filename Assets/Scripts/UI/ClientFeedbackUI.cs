using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public sealed class ClientFeedbackUI : MonoBehaviour
{
    [Header("Display Settings")]
    [SerializeField] private float displayDuration = 3.5f;
    [SerializeField] private float fadeDuration = 0.35f;

    [SerializeField] private Vector2 panelSize =
        new Vector2(350f, 96f);

    [SerializeField] private Vector2 panelOffset =
        new Vector2(-18f, -188f);

    [Header("Text Settings")]
    [SerializeField] private int titleFontSize = 17;
    [SerializeField] private int messageFontSize = 15;

    private readonly Queue<ClientFeedbackData> feedbackQueue = new();

    private GameObject panelObject;
    private CanvasGroup canvasGroup;

    private Text titleText;
    private Text messageText;
    private Text reputationText;

    private Coroutine displayCoroutine;
    private Font runtimeFont;

    public int ActiveCardCount => panelObject != null &&
        panelObject.activeSelf ? 1 : 0;
    public int MaximumVisibleCards => 1;
    public Vector2 PanelSize => panelSize;
    public Vector2 PanelOffset => panelOffset;

    private void Awake()
    {
        panelSize = new Vector2(350f, 96f);
        panelOffset = new Vector2(-18f, -188f);
        titleFontSize = 17;
        messageFontSize = 15;
        BuildInterface();
        HideImmediately();
    }

    private void Start()
    {
        if (ClubReputationManager.Instance == null)
        {
            Debug.LogWarning(
                "ClubReputationManager не найден. " +
                "Отзывы клиентов не будут отображаться."
            );
            return;
        }

        ClubReputationManager.Instance.ClientFeedbackCreated +=
            OnClientFeedbackCreated;
    }

    private void OnDestroy()
    {
        if (ClubReputationManager.Instance != null)
        {
            ClubReputationManager.Instance.ClientFeedbackCreated -=
                OnClientFeedbackCreated;
        }
    }

    private void OnClientFeedbackCreated(ClientFeedbackData feedback)
    {
        feedbackQueue.Enqueue(feedback);

        if (displayCoroutine == null)
        {
            displayCoroutine = StartCoroutine(DisplayFeedbackQueue());
        }
    }

    private IEnumerator DisplayFeedbackQueue()
    {
        while (feedbackQueue.Count > 0)
        {
            ClientFeedbackData feedback = feedbackQueue.Dequeue();

            ApplyFeedback(feedback);
            panelObject.SetActive(true);

            yield return FadeCanvasGroup(0f, 1f);

            float remainingTime = displayDuration;

            while (remainingTime > 0f)
            {
                if (!IsPauseMenuOpen())
                {
                    remainingTime -= Time.unscaledDeltaTime;
                }

                yield return null;
            }

            yield return FadeCanvasGroup(1f, 0f);

            panelObject.SetActive(false);
        }

        displayCoroutine = null;
    }

    private IEnumerator FadeCanvasGroup(float startAlpha, float targetAlpha)
    {
        canvasGroup.alpha = startAlpha;

        if (fadeDuration <= 0f)
        {
            canvasGroup.alpha = targetAlpha;
            yield break;
        }

        float elapsedTime = 0f;

        while (elapsedTime < fadeDuration)
        {
            if (!IsPauseMenuOpen())
            {
                elapsedTime += Time.unscaledDeltaTime;
            }

            float progress = Mathf.Clamp01(elapsedTime / fadeDuration);
            canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, progress);

            yield return null;
        }

        canvasGroup.alpha = targetAlpha;
    }

    private void ApplyFeedback(ClientFeedbackData feedback)
    {
        titleText.text = GetClientTypeDisplayName(feedback.ClientType);
        messageText.text = CompactMessage(feedback.Message);

        string changePrefix = feedback.ReputationChange >= 0 ? "+" : string.Empty;
        string waitingText = feedback.WaitingTime > 0.05f
            ? $" | Ожидание: {feedback.WaitingTime:F1} сек."
            : string.Empty;

        reputationText.text =
            $"Репутация: {changePrefix}{feedback.ReputationChange}" +
            waitingText;
    }

    private void BuildInterface()
    {
        runtimeFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        panelObject = new GameObject(
            "ClientFeedbackPanel",
            typeof(RectTransform),
            typeof(Image),
            typeof(CanvasGroup),
            typeof(VerticalLayoutGroup)
        );
        panelObject.AddComponent<ScalableUIRoot>();
        panelObject.transform.SetParent(transform, false);

        RectTransform panelRect = panelObject.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(1f, 1f);
        panelRect.anchorMax = new Vector2(1f, 1f);
        panelRect.pivot = new Vector2(1f, 1f);
        panelRect.anchoredPosition = panelOffset;
        panelRect.sizeDelta = panelSize;

        Image panelImage = panelObject.GetComponent<Image>();
        panelImage.color = new Color(0.025f, 0.032f, 0.047f, 0.95f);
        panelImage.raycastTarget = false;

        canvasGroup = panelObject.GetComponent<CanvasGroup>();
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        VerticalLayoutGroup layout =
            panelObject.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(12, 12, 6, 6);
        layout.spacing = 1f;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        titleText = CreateText(
            "ClientTypeText",
            titleFontSize,
            20f,
            FontStyle.Bold
        );
        messageText = CreateText(
            "FeedbackMessageText",
            messageFontSize,
            32f,
            FontStyle.Normal
        );
        reputationText = CreateText(
            "ReputationChangeText",
            messageFontSize,
            18f,
            FontStyle.Bold
        );
    }

    private Text CreateText(
        string objectName,
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
        textObject.transform.SetParent(panelObject.transform, false);

        Text text = textObject.GetComponent<Text>();
        text.font = runtimeFont;
        text.fontSize = fontSize;
        text.fontStyle = fontStyle;
        text.color = Color.white;
        text.alignment = TextAnchor.MiddleLeft;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        text.raycastTarget = false;

        LayoutElement layoutElement = textObject.GetComponent<LayoutElement>();
        layoutElement.preferredHeight = preferredHeight;

        return text;
    }

    private void HideImmediately()
    {
        canvasGroup.alpha = 0f;
        panelObject.SetActive(false);
    }

    private static bool IsPauseMenuOpen()
    {
        return PauseMenuController.Instance != null &&
               PauseMenuController.Instance.IsMenuOpen;
    }

    private static string GetClientTypeDisplayName(ClientType clientType)
    {
        return clientType switch
        {
            ClientType.Regular => "Обычный клиент",
            ClientType.Gamer => "Геймер",
            ClientType.VIP => "VIP-клиент",
            _ => clientType.ToString()
        };
    }

    private static string CompactMessage(string message)
    {
        if (string.IsNullOrWhiteSpace(message) || message.Length <= 86)
        {
            return message;
        }

        return message.Substring(0, 83).TrimEnd() + "...";
    }

    private void OnValidate()
    {
        displayDuration = Mathf.Max(0.5f, displayDuration);
        fadeDuration = Mathf.Max(0f, fadeDuration);
        panelSize.x = Mathf.Clamp(panelSize.x, 300f, 360f);
        panelSize.y = Mathf.Clamp(panelSize.y, 86f, 100f);
        titleFontSize = Mathf.Max(14, titleFontSize);
        messageFontSize = Mathf.Max(12, messageFontSize);
    }
}
