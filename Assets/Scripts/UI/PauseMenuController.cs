using UnityEngine;
using UnityEngine.InputSystem;

#if UNITY_EDITOR
using UnityEditor;
#endif

public sealed class PauseMenuController : MonoBehaviour
{
    public static PauseMenuController Instance { get; private set; }

    [Header("Window Settings")]
    [SerializeField] private float menuWidth = 520f;
    [SerializeField] private float menuHeight = 470f;

    private bool isMenuOpen;
    private bool isGameOverMode;
    private bool confirmNewGame;

    private bool cursorStateCaptured;
    private bool previousCursorVisible;
    private CursorLockMode previousCursorLockMode;

    private string statusMessage = string.Empty;
    private float statusMessageUntil;

    private GUIStyle titleStyle;
    private GUIStyle textStyle;
    private GUIStyle buttonStyle;
    private GUIStyle warningStyle;

    public bool IsMenuOpen => isMenuOpen;

    public bool BlocksGameplayInput => isMenuOpen || isGameOverMode;

    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.SubsystemRegistration)]
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

        if (confirmNewGame)
        {
            confirmNewGame = false;
            return;
        }

        SetMenuOpen(!isMenuOpen);
    }

    private void OnGameOverTriggered()
    {
        isGameOverMode = true;
        confirmNewGame = false;
        SetMenuOpen(true);
    }

    private void SetMenuOpen(bool shouldOpen)
    {
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
            return;
        }

        Time.timeScale = 1f;
        RestoreCursorState();
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

    private void OnGUI()
    {
        if (!isMenuOpen)
        {
            return;
        }

        InitializeStyles();
        DrawBackground();
        DrawMenu();
    }

    private void DrawBackground()
    {
        Color previousColor = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.7f);

        GUI.Box(
            new Rect(0f, 0f, Screen.width, Screen.height),
            GUIContent.none
        );

        GUI.color = previousColor;
    }

    private void DrawMenu()
    {
        Rect menuRect = new Rect(
            (Screen.width - menuWidth) / 2f,
            (Screen.height - menuHeight) / 2f,
            menuWidth,
            menuHeight
        );

        GUI.Box(menuRect, GUIContent.none);

        Rect contentRect = new Rect(
            menuRect.x + 25f,
            menuRect.y + 20f,
            menuRect.width - 50f,
            menuRect.height - 40f
        );

        GUILayout.BeginArea(contentRect);
        DrawTitle();
        GUILayout.Space(20f);

        if (isGameOverMode)
        {
            DrawGameOverInformation();
        }
        else
        {
            DrawPauseButtons();
        }

        GUILayout.Space(10f);
        DrawNewGameSection();
        GUILayout.Space(10f);

        if (GUILayout.Button("Выйти из игры", buttonStyle))
        {
            QuitGame();
        }

        DrawStatusMessage();
        GUILayout.EndArea();
    }

    private void DrawTitle()
    {
        string title = isGameOverMode ? "КЛУБ ОБАНКРОТИЛСЯ" : "ПАУЗА";
        GUILayout.Label(title, titleStyle);
    }

    private void DrawPauseButtons()
    {
        if (GUILayout.Button("Продолжить", buttonStyle))
        {
            SetMenuOpen(false);
        }

        GUILayout.Space(10f);

        if (GUILayout.Button("Сохранить игру", buttonStyle))
        {
            SaveGame();
        }
    }

    private void DrawGameOverInformation()
    {
        BankruptcyManager bankruptcy = BankruptcyManager.Instance;

        if (bankruptcy == null)
        {
            GUILayout.Label("Игра завершена.", warningStyle);
            return;
        }

        GUILayout.Label(
            $"Пройдено дней: {bankruptcy.GameOverDay}",
            textStyle
        );

        GUILayout.Label(
            $"Итоговый баланс: {bankruptcy.FinalBalance} ₽",
            textStyle
        );

        GUILayout.Label(
            "Сохранение этой попытки удалено.",
            warningStyle
        );
    }

    private void DrawNewGameSection()
    {
        if (!confirmNewGame)
        {
            if (GUILayout.Button("Новая игра", buttonStyle))
            {
                confirmNewGame = true;
            }

            return;
        }

        GUILayout.Label(
            "Удалить сохранение и начать заново?",
            warningStyle
        );

        GUILayout.BeginHorizontal();

        if (GUILayout.Button("Да, начать заново", buttonStyle))
        {
            StartNewGame();
        }

        if (GUILayout.Button("Отмена", buttonStyle))
        {
            confirmNewGame = false;
        }

        GUILayout.EndHorizontal();
    }

    private void SaveGame()
    {
        if (SaveManager.Instance == null)
        {
            ShowStatusMessage("SaveManager не найден.");
            return;
        }

        bool success = SaveManager.Instance.TrySaveGame();
        ShowStatusMessage(success ? "Игра сохранена." : "Не удалось сохранить игру.");
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
        statusMessage = message;
        statusMessageUntil = Time.unscaledTime + 2.5f;
    }

    private void DrawStatusMessage()
    {
        if (string.IsNullOrWhiteSpace(statusMessage))
        {
            return;
        }

        if (Time.unscaledTime > statusMessageUntil)
        {
            statusMessage = string.Empty;
            return;
        }

        GUILayout.Space(15f);
        GUILayout.Label(statusMessage, textStyle);
    }

    private void InitializeStyles()
    {
        if (titleStyle != null)
        {
            return;
        }

        titleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 32,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };

        textStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 22,
            alignment = TextAnchor.MiddleCenter,
            wordWrap = true
        };

        warningStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 20,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            wordWrap = true
        };

        buttonStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 22,
            fixedHeight = 52f
        };
    }
}
