using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Camera))]
public sealed class CameraFollow : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform target;

    [Header("Follow")]
    [SerializeField, Min(0.01f)] private float followSmoothTime = 0.18f;
    [SerializeField] private Vector2 targetOffset;

    [Header("Zoom")]
    [SerializeField, Min(0.1f)] private float defaultOrthographicSize = 8.8f;
    [SerializeField, Min(0.1f)] private float minimumOrthographicSize = 4f;
    [SerializeField, Min(0.1f)] private float maximumOrthographicSize = 12f;
    [SerializeField, Min(0.1f)] private float zoomStep = 0.75f;
    [SerializeField, Min(0.01f)] private float zoomSmoothTime = 0.12f;
    [SerializeField, Min(0.001f)] private float mouseWheelSensitivity = 0.01f;
    [SerializeField, Min(0.01f)] private float gamepadZoomSpeed = 2f;
    [SerializeField, Min(0.1f)] private float zoomSpeedMultiplier = 4f;

    [Header("Manager View")]
    [SerializeField, Min(0f)] private float overviewPadding = 0.15f;
    [SerializeField, Min(0.1f)] private float focusOrthographicSize = 4.5f;
    [SerializeField, Range(0f, 0.3f)]
    private float topHudReservedFraction = 0.11f;
    [SerializeField, Range(0f, 0.2f)]
    private float bottomHudReservedFraction = 0.07f;
    [SerializeField, Range(0f, 0.4f)]
    private float focusPanelReservedFraction = 0.28f;

    [Header("Bounds")]
    [SerializeField] private CameraBounds2D cameraBounds;

    private Camera controlledCamera;
    private Vector3 followVelocity;
    private float zoomVelocity;
    private float targetOrthographicSize;
    private float gamepadZoomInput;
    private bool useFocusComposition;

    public Transform Target => target;

    public float CurrentOrthographicSize =>
        controlledCamera != null
            ? controlledCamera.orthographicSize
            : targetOrthographicSize;

    public float ZoomSpeedMultiplier => zoomSpeedMultiplier;
    public bool IsFocused => target != null;
    public float FocusOrthographicSize => focusOrthographicSize;
    public float OverviewPadding => overviewPadding;
    public float FocusPanelReservedFraction => focusPanelReservedFraction;

    private void Awake()
    {
        controlledCamera = GetComponent<Camera>();
        controlledCamera.orthographic = true;
        controlledCamera.backgroundColor =
            new Color(0.008f, 0.011f, 0.018f, 1f);

        NormalizeZoomSettings();
        targetOrthographicSize = defaultOrthographicSize;
        controlledCamera.orthographicSize = targetOrthographicSize;
    }

    private void OnEnable()
    {
        followVelocity = Vector3.zero;
        zoomVelocity = 0f;
    }

    private void Start()
    {
        if (target == null)
        {
            PlayerController player = FindAnyObjectByType<PlayerController>();
            if (player != null)
            {
                target = player.transform;
            }
        }

        if (cameraBounds == null)
        {
            cameraBounds = FindAnyObjectByType<CameraBounds2D>();
        }

        ClampTargetZoom();
        controlledCamera.orthographicSize = targetOrthographicSize;
        SnapToTarget();
    }

    private void Update()
    {
        bool inputBlocked = GameplayInputState.IsBlocked;

        if (!inputBlocked)
        {
            ReadMouseZoom();
            UpdateGamepadZoom();
        }
        else
        {
            gamepadZoomInput = 0f;
        }

        UpdateZoom();
    }

    private void LateUpdate()
    {
        FollowTarget();
    }

    public void OnCameraZoom(InputValue inputValue)
    {
        gamepadZoomInput = inputValue.Get<float>();
    }

    public void SetTarget(Transform newTarget)
    {
        useFocusComposition = false;
        target = newTarget;
        SnapToTarget();
    }

    public void Pan(Vector2 worldDelta)
    {
        if (worldDelta.sqrMagnitude < 0.000001f)
        {
            return;
        }

        target = null;
        useFocusComposition = false;
        transform.position = ClampCameraPosition(
            transform.position + (Vector3)worldDelta
        );
        followVelocity = Vector3.zero;
    }

    public void SetBounds(CameraBounds2D newBounds)
    {
        cameraBounds = newBounds;
        ClampTargetZoom();

        if (controlledCamera != null)
        {
            controlledCamera.orthographicSize = Mathf.Min(
                controlledCamera.orthographicSize,
                GetBoundsLimitedMaximumSize()
            );
        }

        ClampCurrentPosition();
    }

    public void ResetZoom()
    {
        targetOrthographicSize = defaultOrthographicSize;
        ClampTargetZoom();
    }

    public bool ShowOverview(bool immediate = true)
    {
        if (cameraBounds == null)
        {
            cameraBounds = FindAnyObjectByType<CameraBounds2D>();
        }

        target = null;
        useFocusComposition = false;
        if (cameraBounds == null || controlledCamera == null)
        {
            ResetZoom();
            return false;
        }

        followVelocity = Vector3.zero;
        FrameBounds(overviewPadding, immediate);
        return true;
    }

    public bool FocusOn(Transform focusTarget, bool immediate = true)
    {
        if (focusTarget == null || controlledCamera == null)
        {
            return false;
        }

        target = focusTarget;
        useFocusComposition = true;
        targetOrthographicSize = Mathf.Clamp(
            focusOrthographicSize,
            minimumOrthographicSize,
            GetBoundsLimitedMaximumSize()
        );
        if (immediate)
        {
            controlledCamera.orthographicSize = targetOrthographicSize;
            zoomVelocity = 0f;
        }

        SnapToTarget();

        return true;
    }

    public void FrameBounds(float padding = 0.6f, bool immediate = true)
    {
        if (cameraBounds == null)
        {
            cameraBounds = FindAnyObjectByType<CameraBounds2D>();
        }

        if (cameraBounds == null || controlledCamera == null)
        {
            ResetZoom();
            return;
        }

        Bounds bounds = cameraBounds.WorldBounds;
        float safeHeight = Mathf.Max(
            0.4f,
            1f - topHudReservedFraction - bottomHudReservedFraction
        );
        float verticalSize =
            (bounds.extents.y + Mathf.Max(0f, padding)) / safeHeight;
        float horizontalSize = (bounds.extents.x + Mathf.Max(0f, padding)) /
            Mathf.Max(0.01f, controlledCamera.aspect);

        targetOrthographicSize = Mathf.Clamp(
            Mathf.Max(verticalSize, horizontalSize),
            minimumOrthographicSize,
            maximumOrthographicSize
        );

        if (immediate)
        {
            controlledCamera.orthographicSize = targetOrthographicSize;
            zoomVelocity = 0f;
        }

        transform.position = GetComposedCameraPosition(
            bounds.center,
            false,
            targetOrthographicSize
        );
        followVelocity = Vector3.zero;
        ClampCurrentPosition();
    }

    public void ZoomIn()
    {
        targetOrthographicSize -= zoomStep * zoomSpeedMultiplier;
        ClampTargetZoom();
    }

    public void ZoomOut()
    {
        targetOrthographicSize += zoomStep * zoomSpeedMultiplier;
        ClampTargetZoom();
    }

    public void SnapToTarget()
    {
        if (target == null)
        {
            return;
        }

        transform.position = ClampCameraPosition(GetDesiredPosition());
        followVelocity = Vector3.zero;
    }

    private void FollowTarget()
    {
        if (target == null)
        {
            return;
        }

        Vector3 smoothedPosition = Vector3.SmoothDamp(
            transform.position,
            GetDesiredPosition(),
            ref followVelocity,
            followSmoothTime
        );

        transform.position = ClampCameraPosition(smoothedPosition);
    }

    private Vector3 GetDesiredPosition()
    {
        Vector2 subjectPosition = new(
            target.position.x + targetOffset.x,
            target.position.y + targetOffset.y
        );
        return GetComposedCameraPosition(
            subjectPosition,
            useFocusComposition,
            controlledCamera != null
                ? controlledCamera.orthographicSize
                : targetOrthographicSize
        );
    }

    private Vector3 GetComposedCameraPosition(
        Vector2 subjectPosition,
        bool reserveFocusPanel,
        float orthographicSize)
    {
        float safeLeft = 0f;
        float safeRight = reserveFocusPanel
            ? 1f - focusPanelReservedFraction
            : 1f;
        float safeBottom = bottomHudReservedFraction;
        float safeTop = 1f - topHudReservedFraction;
        float safeCenterX = (safeLeft + safeRight) * 0.5f;
        float safeCenterY = (safeBottom + safeTop) * 0.5f;
        float aspect = controlledCamera != null
            ? controlledCamera.aspect
            : 1f;

        float horizontalOffset =
            (0.5f - safeCenterX) * 2f * orthographicSize * aspect;
        float verticalOffset =
            (0.5f - safeCenterY) * 2f * orthographicSize;

        return new Vector3(
            subjectPosition.x + horizontalOffset,
            subjectPosition.y + verticalOffset,
            transform.position.z
        );
    }

    private void ReadMouseZoom()
    {
        if (Mouse.current == null)
        {
            return;
        }

        float scrollValue = Mouse.current.scroll.ReadValue().y;
        if (Mathf.Abs(scrollValue) < 0.01f)
        {
            return;
        }

        targetOrthographicSize -= scrollValue * mouseWheelSensitivity *
            zoomSpeedMultiplier;
        ClampTargetZoom();
    }

    private void UpdateGamepadZoom()
    {
        if (Mathf.Abs(gamepadZoomInput) < 0.01f)
        {
            return;
        }

        targetOrthographicSize -= gamepadZoomInput *
            gamepadZoomSpeed * zoomSpeedMultiplier * Time.unscaledDeltaTime;
        ClampTargetZoom();
    }

    private void UpdateZoom()
    {
        if (controlledCamera == null)
        {
            return;
        }

        controlledCamera.orthographicSize = Mathf.SmoothDamp(
            controlledCamera.orthographicSize,
            targetOrthographicSize,
            ref zoomVelocity,
            zoomSmoothTime,
            Mathf.Infinity,
            Time.unscaledDeltaTime
        );

        ClampCurrentPosition();
    }

    private float GetBoundsLimitedMaximumSize()
    {
        return maximumOrthographicSize;
    }

    private void ClampTargetZoom()
    {
        float boundsLimitedMaximum = Mathf.Max(
            0.1f,
            GetBoundsLimitedMaximumSize()
        );
        float effectiveMinimum = Mathf.Min(
            minimumOrthographicSize,
            boundsLimitedMaximum
        );

        targetOrthographicSize = Mathf.Clamp(
            targetOrthographicSize,
            effectiveMinimum,
            boundsLimitedMaximum
        );
    }

    private void ClampCurrentPosition()
    {
        transform.position = ClampCameraPosition(transform.position);
    }

    private Vector3 ClampCameraPosition(Vector3 desiredPosition)
    {
        if (cameraBounds == null || controlledCamera == null)
        {
            return desiredPosition;
        }

        Bounds bounds = cameraBounds.WorldBounds;
        float verticalExtent = controlledCamera.orthographicSize;
        float horizontalExtent = verticalExtent * controlledCamera.aspect;

        float leftExtent = horizontalExtent;
        float rightExtent = horizontalExtent *
            (useFocusComposition
                ? 1f - focusPanelReservedFraction * 2f
                : 1f);
        float bottomExtent = verticalExtent *
            (1f - bottomHudReservedFraction * 2f);
        float topExtent = verticalExtent *
            (1f - topHudReservedFraction * 2f);

        float clampedX = ClampComposedAxis(
            desiredPosition.x,
            bounds.min.x,
            bounds.max.x,
            leftExtent,
            rightExtent
        );
        float clampedY = ClampComposedAxis(
            desiredPosition.y,
            bounds.min.y,
            bounds.max.y,
            bottomExtent,
            topExtent
        );

        return new Vector3(clampedX, clampedY, desiredPosition.z);
    }

    private static float ClampComposedAxis(
        float desiredCenter,
        float boundsMinimum,
        float boundsMaximum,
        float negativeExtent,
        float positiveExtent)
    {
        float minimumCenter = boundsMinimum + negativeExtent;
        float maximumCenter = boundsMaximum - positiveExtent;

        if (minimumCenter <= maximumCenter)
        {
            return Mathf.Clamp(desiredCenter, minimumCenter, maximumCenter);
        }

        return Mathf.Clamp(
            desiredCenter,
            boundsMaximum - positiveExtent,
            boundsMinimum + negativeExtent
        );
    }

    private void NormalizeZoomSettings()
    {
        minimumOrthographicSize = Mathf.Max(0.1f, minimumOrthographicSize);
        maximumOrthographicSize = Mathf.Max(
            minimumOrthographicSize,
            maximumOrthographicSize
        );
        defaultOrthographicSize = Mathf.Clamp(
            defaultOrthographicSize,
            minimumOrthographicSize,
            maximumOrthographicSize
        );
    }

    private void OnValidate()
    {
        followSmoothTime = Mathf.Max(0.01f, followSmoothTime);
        zoomSmoothTime = Mathf.Max(0.01f, zoomSmoothTime);
        zoomStep = Mathf.Max(0.1f, zoomStep);
        mouseWheelSensitivity = Mathf.Max(0.001f, mouseWheelSensitivity);
        gamepadZoomSpeed = Mathf.Max(0.01f, gamepadZoomSpeed);
        zoomSpeedMultiplier = Mathf.Max(0.1f, zoomSpeedMultiplier);
        overviewPadding = Mathf.Max(0f, overviewPadding);
        focusOrthographicSize = Mathf.Max(0.1f, focusOrthographicSize);
        topHudReservedFraction = Mathf.Clamp(topHudReservedFraction, 0f, 0.3f);
        bottomHudReservedFraction = Mathf.Clamp(
            bottomHudReservedFraction,
            0f,
            0.2f
        );
        focusPanelReservedFraction = Mathf.Clamp(
            focusPanelReservedFraction,
            0f,
            0.4f
        );
        NormalizeZoomSettings();
    }
}
