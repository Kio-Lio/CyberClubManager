using UnityEngine;

public class InteractionPromptUI : MonoBehaviour
{
    [SerializeField] private int fontSize = 24;
    [SerializeField] private float panelWidth = 620f;
    [SerializeField] private float panelHeight = 50f;
    [SerializeField] private float bottomOffset = 30f;

    private PlayerInteraction playerInteraction;
    private string currentPrompt = string.Empty;
    private GUIStyle labelStyle;

    private void Start()
    {
        playerInteraction = FindAnyObjectByType<PlayerInteraction>();

        if (playerInteraction == null)
        {
            Debug.LogWarning(
                "PlayerInteraction не найден. " +
                "Подсказки взаимодействия отключены."
            );
            return;
        }

        playerInteraction.PromptChanged += OnPromptChanged;
        currentPrompt = playerInteraction.CurrentPrompt;
    }

    private void OnDestroy()
    {
        if (playerInteraction != null)
        {
            playerInteraction.PromptChanged -= OnPromptChanged;
        }
    }

    private void OnPromptChanged(string prompt)
    {
        currentPrompt = prompt;
    }

    private void OnGUI()
    {
        if (string.IsNullOrWhiteSpace(currentPrompt))
        {
            return;
        }

        InitializeStyle();

        Rect panelRect = new Rect(
            (Screen.width - panelWidth) / 2f,
            Screen.height - panelHeight - bottomOffset,
            panelWidth,
            panelHeight
        );

        GUI.Box(panelRect, string.Empty);
        GUI.Label(panelRect, currentPrompt, labelStyle);
    }

    private void InitializeStyle()
    {
        if (labelStyle != null)
        {
            return;
        }

        labelStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = fontSize,
            alignment = TextAnchor.MiddleCenter,
            wordWrap = true
        };
    }
}
