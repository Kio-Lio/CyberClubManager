using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public sealed class ManagerBuildController : MonoBehaviour
{
    private enum PlacementOperation
    {
        None,
        Purchase,
        Move
    }

    private static readonly Color ValidColor =
        new(0.10f, 0.92f, 0.66f, 0.58f);
    private static readonly Color InvalidColor =
        new(1f, 0.28f, 0.32f, 0.58f);

    [SerializeField, Min(0.1f)] private float gridSize = 0.5f;
    [SerializeField] private Vector2 pcFootprint = new(0.9f, 0.9f);
    [SerializeField, Min(0f)] private float boundsPadding = 0.15f;

    private Camera controlledCamera;
    private CameraBounds2D cameraBounds;
    private ManagerModeController managerMode;
    private GameObject previewObject;
    private SpriteRenderer previewRenderer;
    private GameObject buildPanel;
    private Text buildText;
    private Vector2 currentPosition;
    private Vector3 originalPCPosition;
    private PC movingPC;
    private Renderer[] movingRenderers;
    private bool[] movingRendererStates;
    private Collider2D[] movingColliders;
    private bool[] movingColliderStates;
    private PlacementOperation operation;
    private bool currentPositionValid;
    private bool isPlacing;

    public static ManagerBuildController Instance { get; private set; }

    public bool IsPlacing => isPlacing;
    public bool IsCurrentPositionValid => currentPositionValid;
    public Vector2 CurrentPosition => currentPosition;

    public event Action StateChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
        managerMode = GetComponent<ManagerModeController>();
        controlledCamera = Camera.main ?? FindAnyObjectByType<Camera>();
        cameraBounds = FindAnyObjectByType<CameraBounds2D>();
    }

    private void Start()
    {
        BuildStatusPanel();
        SetBuildUIVisible(false);
    }

    private void Update()
    {
        if (!isPlacing)
        {
            return;
        }

        if (GameplayInputState.IsBlocked)
        {
            previewObject?.SetActive(false);
            SetBuildUIVisible(false);
            return;
        }

        previewObject?.SetActive(true);
        SetBuildUIVisible(true);

        Mouse mouse = Mouse.current;
        if (mouse == null || controlledCamera == null)
        {
            return;
        }

        Vector2 worldPosition = controlledCamera.ScreenToWorldPoint(
            mouse.position.ReadValue()
        );
        UpdatePreview(worldPosition);

        if (mouse.rightButton.wasPressedThisFrame)
        {
            CancelPlacement();
            return;
        }

        if (mouse.leftButton.wasPressedThisFrame && !IsPointerOverUI())
        {
            TryPlaceAt(worldPosition);
        }
    }

    private void OnDestroy()
    {
        RestoreMovingPCVisuals();

        if (Instance == this)
        {
            Instance = null;
        }
    }

    public bool BeginPCPlacement()
    {
        PCExpansionManager expansion = PCExpansionManager.Instance;
        EconomyManager economy = EconomyManager.Instance;

        if (expansion == null || !expansion.HasAvailableSlot ||
            economy == null || economy.Money < expansion.PurchaseCost)
        {
            return false;
        }

        if (isPlacing)
        {
            return false;
        }

        EnsurePreview();
        operation = PlacementOperation.Purchase;
        isPlacing = true;
        managerMode?.ClearSelection();
        previewObject.SetActive(true);
        SetBuildUIVisible(true);
        RefreshBuildText();
        StateChanged?.Invoke();
        return true;
    }

    public bool BeginPCMove(PC pc)
    {
        PCExpansionManager expansion = PCExpansionManager.Instance;
        if (isPlacing || expansion == null || !expansion.CanMovePC(pc) ||
            (BankruptcyManager.Instance != null &&
             BankruptcyManager.Instance.IsGameOver))
        {
            return false;
        }

        EnsurePreview();
        operation = PlacementOperation.Move;
        movingPC = pc;
        originalPCPosition = pc.transform.position;
        CacheAndHideMovingPCVisuals();
        isPlacing = true;
        managerMode?.ClearSelection();
        UpdatePreview(originalPCPosition);
        previewObject.SetActive(true);
        SetBuildUIVisible(true);
        StateChanged?.Invoke();
        return true;
    }

    public void CancelPlacement()
    {
        if (!isPlacing)
        {
            return;
        }

        EndPlacement(true);
    }

    public bool TryPlaceAt(Vector2 worldPosition)
    {
        if (!isPlacing)
        {
            return false;
        }

        UpdatePreview(worldPosition);
        if (!currentPositionValid)
        {
            return false;
        }

        if (operation == PlacementOperation.Move)
        {
            PC movedPC = movingPC;
            if (movedPC == null)
            {
                CancelPlacement();
                return false;
            }

            movedPC.transform.position = currentPosition;
            EndPlacement(false);
            managerMode?.SelectBehaviour(movedPC);
            return true;
        }

        PCExpansionManager expansion = PCExpansionManager.Instance;
        if (operation != PlacementOperation.Purchase || expansion == null ||
            !expansion.TryPurchasePCAt(currentPosition, out PC createdPC))
        {
            RefreshBuildText();
            return false;
        }

        EndPlacement(false);
        managerMode?.SelectBehaviour(createdPC);
        return true;
    }

    private void EndPlacement(bool restoreOriginalPosition)
    {
        PC movedPC = movingPC;
        if (movedPC != null && restoreOriginalPosition)
        {
            movedPC.transform.position = originalPCPosition;
        }

        RestoreMovingPCVisuals();
        if (movedPC != null)
        {
            ClientNavigationManager navigation =
                ClientNavigationManager.Instance ??
                ClientNavigationManager.EnsureRuntimeGraph();
            navigation.EnsureApproachNode(movedPC);
        }

        movingPC = null;
        movingRenderers = null;
        movingRendererStates = null;
        movingColliders = null;
        movingColliderStates = null;
        operation = PlacementOperation.None;
        isPlacing = false;
        currentPositionValid = false;
        previewObject?.SetActive(false);
        SetBuildUIVisible(false);
        StateChanged?.Invoke();
    }

    private void CacheAndHideMovingPCVisuals()
    {
        movingRenderers = movingPC.GetComponentsInChildren<Renderer>(true);
        movingRendererStates = new bool[movingRenderers.Length];
        for (int index = 0; index < movingRenderers.Length; index++)
        {
            movingRendererStates[index] = movingRenderers[index].enabled;
            movingRenderers[index].enabled = false;
        }

        movingColliders = movingPC.GetComponentsInChildren<Collider2D>(true);
        movingColliderStates = new bool[movingColliders.Length];
        for (int index = 0; index < movingColliders.Length; index++)
        {
            movingColliderStates[index] = movingColliders[index].enabled;
            movingColliders[index].enabled = false;
        }
    }

    private void RestoreMovingPCVisuals()
    {
        if (movingRenderers != null && movingRendererStates != null)
        {
            for (int index = 0; index < movingRenderers.Length; index++)
            {
                if (movingRenderers[index] != null)
                {
                    movingRenderers[index].enabled = movingRendererStates[index];
                }
            }
        }

        if (movingColliders != null && movingColliderStates != null)
        {
            for (int index = 0; index < movingColliders.Length; index++)
            {
                if (movingColliders[index] != null)
                {
                    movingColliders[index].enabled = movingColliderStates[index];
                }
            }
        }
    }

    public Vector2 SnapToGrid(Vector2 worldPosition)
    {
        float effectiveGrid = Mathf.Max(0.1f, gridSize);
        return new Vector2(
            Mathf.Round(worldPosition.x / effectiveGrid) * effectiveGrid,
            Mathf.Round(worldPosition.y / effectiveGrid) * effectiveGrid
        );
    }

    public bool IsPlacementValid(Vector2 worldPosition)
    {
        Vector2 snappedPosition = SnapToGrid(worldPosition);

        if (!IsInsideClubBounds(snappedPosition))
        {
            return false;
        }

        Physics2D.SyncTransforms();
        foreach (Collider2D hit in Physics2D.OverlapBoxAll(
            snappedPosition,
            pcFootprint,
            0f
        ))
        {
            if (ShouldIgnorePlacementCollider(hit))
            {
                continue;
            }

            return false;
        }

        ClientNavigationManager navigation =
            ClientNavigationManager.Instance ??
            ClientNavigationManager.EnsureRuntimeGraph();
        return navigation.TryGetOpenApproachPosition(
            snappedPosition,
            out _
        );
    }

    private void UpdatePreview(Vector2 worldPosition)
    {
        EnsurePreview();
        currentPosition = SnapToGrid(worldPosition);
        currentPositionValid = IsPlacementValid(currentPosition);

        previewObject.transform.position = new Vector3(
            currentPosition.x,
            currentPosition.y,
            0f
        );
        previewRenderer.color = currentPositionValid
            ? ValidColor
            : InvalidColor;
        RefreshBuildText();
    }

    private bool IsInsideClubBounds(Vector2 position)
    {
        if (cameraBounds == null)
        {
            cameraBounds = FindAnyObjectByType<CameraBounds2D>();
        }

        if (cameraBounds == null)
        {
            return true;
        }

        Bounds bounds = cameraBounds.WorldBounds;
        Vector2 extents = pcFootprint * 0.5f +
            Vector2.one * boundsPadding;

        return position.x >= bounds.min.x + extents.x &&
            position.x <= bounds.max.x - extents.x &&
            position.y >= bounds.min.y + extents.y &&
            position.y <= bounds.max.y - extents.y;
    }

    private static bool ShouldIgnorePlacementCollider(Collider2D collider)
    {
        if (collider == null || !collider.enabled)
        {
            return true;
        }

        if (collider.GetComponentInParent<CameraBounds2D>() != null)
        {
            return true;
        }

        return collider.name.StartsWith(
            "PCTable_",
            StringComparison.Ordinal
        );
    }

    private void EnsurePreview()
    {
        if (previewObject != null)
        {
            return;
        }

        previewObject = new GameObject("PCPlacementPreview");
        previewRenderer = previewObject.AddComponent<SpriteRenderer>();
        previewRenderer.sprite = CreateSquareSprite();
        previewRenderer.sortingOrder = 30000;
        previewObject.transform.localScale = pcFootprint;
    }

    private void BuildStatusPanel()
    {
        ClubHUDCanvas hud = ClubHUDCanvas.Instance;
        if (hud == null)
        {
            return;
        }

        buildPanel = new GameObject(
            "ManagerBuildPanel",
            typeof(RectTransform),
            typeof(Image),
            typeof(HorizontalLayoutGroup)
        );
        buildPanel.transform.SetParent(hud.transform, false);

        RectTransform panelRect = buildPanel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0f);
        panelRect.anchorMax = new Vector2(0.5f, 0f);
        panelRect.pivot = new Vector2(0.5f, 0f);
        panelRect.anchoredPosition = new Vector2(0f, 92f);
        panelRect.sizeDelta = new Vector2(720f, 64f);

        buildPanel.GetComponent<Image>().color =
            new Color(0.015f, 0.045f, 0.085f, 0.96f);

        HorizontalLayoutGroup layout =
            buildPanel.GetComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(18, 10, 8, 8);
        layout.spacing = 16f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = true;

        buildText = CreateText(buildPanel.transform);
        LayoutElement textLayout = buildText.gameObject.AddComponent<LayoutElement>();
        textLayout.preferredWidth = 550f;
        textLayout.flexibleWidth = 1f;

        Button cancelButton = CreateButton(buildPanel.transform, "ОТМЕНА");
        cancelButton.onClick.AddListener(CancelPlacement);
        LayoutElement buttonLayout =
            cancelButton.gameObject.AddComponent<LayoutElement>();
        buttonLayout.preferredWidth = 130f;
    }

    private void RefreshBuildText()
    {
        if (buildText == null)
        {
            return;
        }

        PCExpansionManager expansion = PCExpansionManager.Instance;
        string pcName = operation == PlacementOperation.Move && movingPC != null
            ? movingPC.name
            : expansion != null ? expansion.GetNextPCName() : "PC";
        string status = currentPositionValid
            ? "ПОЗИЦИЯ ДОСТУПНА"
            : "РАЗМЕЩЕНИЕ НЕВОЗМОЖНО";

        string action = operation == PlacementOperation.Move
            ? "ЛКМ переместить"
            : "ЛКМ поставить";
        buildText.text =
            $"{pcName}  |  {status}  |  {action} · ПКМ отменить";
        Color statusColor = currentPositionValid
            ? ValidColor
            : InvalidColor;
        statusColor.a = 1f;
        buildText.color = statusColor;
    }

    private void SetBuildUIVisible(bool visible)
    {
        buildPanel?.SetActive(visible);
    }

    private static Text CreateText(Transform parent)
    {
        GameObject textObject = new GameObject(
            "BuildStatus",
            typeof(RectTransform),
            typeof(Text)
        );
        textObject.transform.SetParent(parent, false);

        Text text = textObject.GetComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 19;
        text.alignment = TextAnchor.MiddleLeft;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        return text;
    }

    private static Button CreateButton(Transform parent, string label)
    {
        GameObject buttonObject = new GameObject(
            "CancelBuildButton",
            typeof(RectTransform),
            typeof(Image),
            typeof(Button)
        );
        buttonObject.transform.SetParent(parent, false);

        Image image = buttonObject.GetComponent<Image>();
        image.color = new Color(0.04f, 0.14f, 0.24f, 1f);

        Button button = buttonObject.GetComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = new Color(0.04f, 0.14f, 0.24f, 1f);
        colors.highlightedColor = new Color(0.04f, 0.32f, 0.52f, 1f);
        colors.pressedColor = new Color(0.02f, 0.48f, 0.70f, 1f);
        button.colors = colors;

        Text text = CreateText(buttonObject.transform);
        text.text = label;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;

        RectTransform textRect = text.rectTransform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        return button;
    }

    private static Sprite CreateSquareSprite()
    {
        Texture2D texture = new Texture2D(2, 2)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };
        texture.SetPixels(new[]
        {
            Color.white, Color.white, Color.white, Color.white
        });
        texture.Apply();
        return Sprite.Create(
            texture,
            new Rect(0f, 0f, 2f, 2f),
            new Vector2(0.5f, 0.5f),
            2f
        );
    }

    private static bool IsPointerOverUI()
    {
        return EventSystem.current != null &&
            EventSystem.current.IsPointerOverGameObject();
    }

    private void OnValidate()
    {
        gridSize = Mathf.Max(0.1f, gridSize);
        pcFootprint.x = Mathf.Max(0.1f, pcFootprint.x);
        pcFootprint.y = Mathf.Max(0.1f, pcFootprint.y);
        boundsPadding = Mathf.Max(0f, boundsPadding);
    }
}
