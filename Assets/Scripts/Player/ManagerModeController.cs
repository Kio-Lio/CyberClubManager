using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DefaultExecutionOrder(100)]
public sealed class ManagerModeController : MonoBehaviour,
    IInteractionPromptSource
{
    private const string GameplaySceneName = "SampleScene";
    private static readonly List<RaycastResult> UIRaycastResults = new();

    [Header("Camera")]
    [SerializeField, Min(0.1f)] private float keyboardPanSpeed = 8f;
    [SerializeField, Min(0.01f)] private float mouseDragSensitivity = 1f;

    [Header("Interaction")]
    [SerializeField] private LayerMask interactionLayers = ~0;

    private Camera controlledCamera;
    private CameraFollow cameraFollow;
    private PlayerController playerController;
    private PlayerInteraction playerInteraction;
    private Rigidbody2D playerBody;
    private Collider2D[] playerColliders;
    private Renderer[] playerRenderers;
    private YSortRenderer playerYSort;

    private Vector2 panInput;
    private Vector2 previousMousePosition;
    private MonoBehaviour hoveredBehaviour;
    private MonoBehaviour selectedBehaviour;
    private GameObject selectionIndicator;
    private string currentPrompt = string.Empty;
    private bool isDraggingCamera;

    public string CurrentPrompt => currentPrompt;
    public MonoBehaviour SelectedBehaviour => selectedBehaviour;

    public event Action<string> PromptChanged;
    public event Action<MonoBehaviour> SelectionChanged;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterSceneCallback()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != GameplaySceneName)
        {
            return;
        }

        GameObject playerObject = GameObject.Find("Player");
        if (playerObject != null &&
            playerObject.GetComponent<ManagerModeController>() == null)
        {
            playerObject.AddComponent<ManagerModeController>();
        }
    }

    private void Awake()
    {
        playerController = GetComponent<PlayerController>();
        playerInteraction = GetComponent<PlayerInteraction>();
        playerBody = GetComponent<Rigidbody2D>();
        playerColliders = GetComponentsInChildren<Collider2D>(true);
        playerRenderers = GetComponentsInChildren<Renderer>(true);
        playerYSort = GetComponent<YSortRenderer>();

        controlledCamera = Camera.main ?? FindAnyObjectByType<Camera>();
        cameraFollow = controlledCamera != null
            ? controlledCamera.GetComponent<CameraFollow>()
            : null;

        if (GetComponent<ManagerBuildController>() == null)
        {
            gameObject.AddComponent<ManagerBuildController>();
        }

        EnableManagerMode();
    }

    private void Start()
    {
        if (controlledCamera == null)
        {
            controlledCamera = Camera.main ?? FindAnyObjectByType<Camera>();
        }

        if (cameraFollow == null && controlledCamera != null)
        {
            cameraFollow = controlledCamera.GetComponent<CameraFollow>();
        }

        cameraFollow?.SetTarget(null);

        ClubHUDCanvas hud = ClubHUDCanvas.Instance;
        if (hud != null && hud.GetComponent<ManagerSelectionPanel>() == null)
        {
            hud.gameObject.AddComponent<ManagerSelectionPanel>();
        }

        if (hud != null && hud.GetComponent<ManagerCommandBar>() == null)
        {
            hud.gameObject.AddComponent<ManagerCommandBar>();
        }
    }

    private void Update()
    {
        if (GameplayInputState.IsBlocked)
        {
            isDraggingCamera = false;
            SetHoveredBehaviour(null);
            return;
        }

        UpdateKeyboardPan();
        UpdateMouseDrag();

        if (ManagerBuildController.Instance != null &&
            ManagerBuildController.Instance.IsPlacing)
        {
            SetHoveredBehaviour(null);
            return;
        }

        UpdatePointerInteraction();
    }

    private void OnDisable()
    {
        SetHoveredBehaviour(null);
        ClearSelection();
    }

    public void OnMove(InputValue value)
    {
        panInput = value.Get<Vector2>();
    }

    public void OnCameraZoom(InputValue value)
    {
        cameraFollow?.OnCameraZoom(value);
    }

    public void OnToggleHUD(InputValue value)
    {
        if (value.isPressed && !GameplayInputState.IsBlocked)
        {
            ClubHUDCanvas.Instance?.ToggleHUDMode();
        }
    }

    public bool TryInteractAtWorldPosition(Vector2 worldPosition)
    {
        MonoBehaviour candidate = FindInteractableAt(worldPosition);
        SetHoveredBehaviour(candidate);

        if (candidate is not IInteractable interactable)
        {
            return false;
        }

        interactable.Interact();
        RefreshPrompt();
        return true;
    }

    public bool TrySelectAtWorldPosition(Vector2 worldPosition)
    {
        MonoBehaviour candidate = FindInteractableAt(worldPosition);
        SelectBehaviour(candidate);
        return candidate != null;
    }

    public bool TryActivateAtWorldPosition(Vector2 worldPosition)
    {
        MonoBehaviour candidate = FindInteractableAt(worldPosition);
        SetHoveredBehaviour(candidate);

        if (candidate == null)
        {
            ClearSelection();
            return false;
        }

        if (candidate != selectedBehaviour)
        {
            SelectBehaviour(candidate);
            return true;
        }

        if (candidate is not IInteractable interactable)
        {
            return false;
        }

        interactable.Interact();
        SelectionChanged?.Invoke(selectedBehaviour);
        RefreshPrompt();
        return true;
    }

    public void SelectBehaviour(MonoBehaviour behaviour)
    {
        if (selectedBehaviour == behaviour)
        {
            RefreshSelectionIndicator();
            return;
        }

        selectedBehaviour = behaviour;
        RefreshSelectionIndicator();
        SelectionChanged?.Invoke(selectedBehaviour);

        if (selectedBehaviour is PC)
        {
            FirstDayTutorialManager.Instance?.ReportAction(
                TutorialStepType.ApproachPC
            );
        }
    }

    public void ClearSelection()
    {
        if (selectedBehaviour == null && selectionIndicator == null)
        {
            return;
        }

        selectedBehaviour = null;
        DestroySelectionIndicator();
        SelectionChanged?.Invoke(null);
    }

    public void InteractSelected()
    {
        if (selectedBehaviour is not IInteractable interactable ||
            GameplayInputState.IsBlocked)
        {
            return;
        }

        interactable.Interact();
        SelectionChanged?.Invoke(selectedBehaviour);
        RefreshPrompt();
    }

    public bool TryFocusAtWorldPosition(Vector2 worldPosition)
    {
        MonoBehaviour candidate = FindInteractableAt(worldPosition);
        SetHoveredBehaviour(candidate);
        return candidate != null;
    }

    private void EnableManagerMode()
    {
        if (playerController != null)
        {
            playerController.enabled = false;
        }

        if (playerInteraction != null)
        {
            playerInteraction.enabled = false;
        }

        if (playerYSort != null)
        {
            playerYSort.enabled = false;
        }

        if (playerBody != null)
        {
            playerBody.simulated = false;
        }

        foreach (Collider2D playerCollider in playerColliders)
        {
            if (playerCollider != null)
            {
                playerCollider.enabled = false;
            }
        }

        foreach (Renderer playerRenderer in playerRenderers)
        {
            if (playerRenderer != null)
            {
                playerRenderer.enabled = false;
            }
        }

        cameraFollow?.SetTarget(null);
    }

    private void UpdateKeyboardPan()
    {
        if (panInput.sqrMagnitude < 0.001f)
        {
            return;
        }

        Vector2 movement = Vector2.ClampMagnitude(panInput, 1f) *
            keyboardPanSpeed * Time.unscaledDeltaTime;
        PanCamera(movement);
    }

    private void UpdateMouseDrag()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null)
        {
            return;
        }

        Vector2 mousePosition = mouse.position.ReadValue();

        if (mouse.middleButton.wasPressedThisFrame && !IsPointerOverUI())
        {
            isDraggingCamera = true;
            previousMousePosition = mousePosition;
        }

        if (mouse.middleButton.wasReleasedThisFrame)
        {
            isDraggingCamera = false;
        }

        if (!isDraggingCamera || controlledCamera == null)
        {
            return;
        }

        Vector3 previousWorld = controlledCamera.ScreenToWorldPoint(
            previousMousePosition
        );
        Vector3 currentWorld = controlledCamera.ScreenToWorldPoint(
            mousePosition
        );
        Vector2 movement = (Vector2)(previousWorld - currentWorld) *
            mouseDragSensitivity;

        PanCamera(movement);
        previousMousePosition = mousePosition;
    }

    private void UpdatePointerInteraction()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null || controlledCamera == null || IsPointerOverUI())
        {
            SetHoveredBehaviour(null);
            return;
        }

        Vector2 worldPosition = controlledCamera.ScreenToWorldPoint(
            mouse.position.ReadValue()
        );
        if (mouse.leftButton.wasPressedThisFrame)
        {
            TryActivateAtWorldPosition(worldPosition);
            return;
        }

        TryFocusAtWorldPosition(worldPosition);
    }

    private MonoBehaviour FindInteractableAt(Vector2 worldPosition)
    {
        Collider2D[] hits = Physics2D.OverlapPointAll(
            worldPosition,
            interactionLayers
        );
        MonoBehaviour bestCandidate = null;
        int bestSortingOrder = int.MinValue;

        foreach (Collider2D hit in hits)
        {
            MonoBehaviour candidate = FindInteractableBehaviour(hit);
            if (candidate == null)
            {
                continue;
            }

            SpriteRenderer renderer =
                candidate.GetComponentInChildren<SpriteRenderer>();
            int sortingOrder = renderer != null
                ? renderer.sortingOrder
                : 0;

            if (bestCandidate == null || sortingOrder > bestSortingOrder)
            {
                bestCandidate = candidate;
                bestSortingOrder = sortingOrder;
            }
        }

        return bestCandidate;
    }

    private void SetHoveredBehaviour(MonoBehaviour behaviour)
    {
        if (hoveredBehaviour == behaviour)
        {
            RefreshPrompt();
            return;
        }

        hoveredBehaviour = behaviour;

        if (hoveredBehaviour is PC)
        {
            FirstDayTutorialManager.Instance?.ReportAction(
                TutorialStepType.ApproachPC
            );
        }

        RefreshPrompt();
    }

    private void RefreshSelectionIndicator()
    {
        DestroySelectionIndicator();

        if (selectedBehaviour == null)
        {
            return;
        }

        SpriteRenderer sourceRenderer =
            selectedBehaviour.GetComponentInChildren<SpriteRenderer>();
        if (sourceRenderer == null || sourceRenderer.sprite == null)
        {
            return;
        }

        selectionIndicator = new GameObject("ManagerSelectionIndicator");
        selectionIndicator.transform.SetParent(
            sourceRenderer.transform,
            false
        );
        selectionIndicator.transform.localPosition = Vector3.zero;
        selectionIndicator.transform.localRotation = Quaternion.identity;
        selectionIndicator.transform.localScale = Vector3.one * 1.18f;

        SpriteRenderer indicatorRenderer =
            selectionIndicator.AddComponent<SpriteRenderer>();
        indicatorRenderer.sprite = sourceRenderer.sprite;
        indicatorRenderer.color = new Color(0.04f, 0.72f, 1f, 0.35f);
        indicatorRenderer.sortingLayerID = sourceRenderer.sortingLayerID;
        indicatorRenderer.sortingOrder = sourceRenderer.sortingOrder + 25;
    }

    private void DestroySelectionIndicator()
    {
        if (selectionIndicator == null)
        {
            return;
        }

        Destroy(selectionIndicator);
        selectionIndicator = null;
    }

    private void RefreshPrompt()
    {
        if (hoveredBehaviour is not IInteractable interactable)
        {
            SetPrompt(string.Empty);
            return;
        }

        string prompt = interactable.GetInteractionPrompt() ?? string.Empty;
        prompt = prompt.Replace("E - ", string.Empty)
            .Replace("E — ", string.Empty);

        if (hoveredBehaviour != selectedBehaviour)
        {
            SetPrompt($"ЛКМ · выбрать {hoveredBehaviour.name}");
            return;
        }

        SetPrompt(string.IsNullOrWhiteSpace(prompt)
            ? string.Empty
            : $"ЛКМ ещё раз · {prompt}");
    }

    private void SetPrompt(string prompt)
    {
        prompt ??= string.Empty;
        if (currentPrompt == prompt)
        {
            return;
        }

        currentPrompt = prompt;
        PromptChanged?.Invoke(currentPrompt);
    }

    private void PanCamera(Vector2 movement)
    {
        if (cameraFollow != null)
        {
            cameraFollow.Pan(movement);
            return;
        }

        if (controlledCamera != null)
        {
            controlledCamera.transform.position += (Vector3)movement;
        }
    }

    private static bool IsPointerOverUI()
    {
        EventSystem eventSystem = EventSystem.current;
        Mouse mouse = Mouse.current;
        if (eventSystem == null || mouse == null)
        {
            return false;
        }

        PointerEventData pointerData = new(eventSystem)
        {
            position = mouse.position.ReadValue()
        };

        UIRaycastResults.Clear();
        eventSystem.RaycastAll(pointerData, UIRaycastResults);

        foreach (RaycastResult result in UIRaycastResults)
        {
            GameObject hitObject = result.gameObject;
            if (hitObject == null)
            {
                continue;
            }

            if (hitObject.GetComponentInParent<Selectable>() != null ||
                IsManagerPanel(hitObject.transform))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsManagerPanel(Transform current)
    {
        while (current != null)
        {
            if (current.name == "ManagerCommandBar" ||
                current.name == "ManagerSelectionPanel")
            {
                return true;
            }

            current = current.parent;
        }

        return false;
    }

    private static MonoBehaviour FindInteractableBehaviour(Collider2D collider)
    {
        MonoBehaviour[] behaviours =
            collider.GetComponentsInParent<MonoBehaviour>(true);

        foreach (MonoBehaviour behaviour in behaviours)
        {
            if (behaviour is IInteractable && behaviour.isActiveAndEnabled)
            {
                return behaviour;
            }
        }

        return null;
    }

    private void OnValidate()
    {
        keyboardPanSpeed = Mathf.Max(0.1f, keyboardPanSpeed);
        mouseDragSensitivity = Mathf.Max(0.01f, mouseDragSensitivity);
    }
}
