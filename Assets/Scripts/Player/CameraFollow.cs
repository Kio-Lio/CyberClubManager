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

    [Header("Bounds")]
    [SerializeField] private CameraBounds2D cameraBounds;

    private Camera controlledCamera;
    private Vector3 followVelocity;
    private float zoomVelocity;
    private float targetOrthographicSize;
    private float gamepadZoomInput;

    public Transform Target => target;

    public float CurrentOrthographicSize =>
        controlledCamera != null
            ? controlledCamera.orthographicSize
            : targetOrthographicSize;

    private void Awake()
    {
        controlledCamera = GetComponent<Camera>();
        controlledCamera.orthographic = true;

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
        float verticalSize = bounds.extents.y;
        float horizontalSize = bounds.extents.x /
            Mathf.Max(0.01f, controlledCamera.aspect);

        targetOrthographicSize = Mathf.Clamp(
            Mathf.Max(verticalSize, horizontalSize) + Mathf.Max(0f, padding),
            minimumOrthographicSize,
            maximumOrthographicSize
        );

        if (immediate)
        {
            controlledCamera.orthographicSize = targetOrthographicSize;
            zoomVelocity = 0f;
        }

        ClampCurrentPosition();
    }

    public void ZoomIn()
    {
        targetOrthographicSize -= zoomStep;
        ClampTargetZoom();
    }

    public void ZoomOut()
    {
        targetOrthographicSize += zoomStep;
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
        return new Vector3(
            target.position.x + targetOffset.x,
            target.position.y + targetOffset.y,
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

        targetOrthographicSize -= scrollValue * mouseWheelSensitivity;
        ClampTargetZoom();
    }

    private void UpdateGamepadZoom()
    {
        if (Mathf.Abs(gamepadZoomInput) < 0.01f)
        {
            return;
        }

        targetOrthographicSize -= gamepadZoomInput *
            gamepadZoomSpeed * Time.unscaledDeltaTime;
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

        float minimumX = bounds.min.x + horizontalExtent;
        float maximumX = bounds.max.x - horizontalExtent;
        float minimumY = bounds.min.y + verticalExtent;
        float maximumY = bounds.max.y - verticalExtent;

        float clampedX = minimumX > maximumX
            ? bounds.center.x
            : Mathf.Clamp(desiredPosition.x, minimumX, maximumX);
        float clampedY = minimumY > maximumY
            ? bounds.center.y
            : Mathf.Clamp(desiredPosition.y, minimumY, maximumY);

        return new Vector3(clampedX, clampedY, desiredPosition.z);
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
        NormalizeZoomSettings();
    }
}
