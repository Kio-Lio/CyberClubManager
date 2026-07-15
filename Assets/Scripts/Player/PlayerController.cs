using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 5f;

    private Rigidbody2D rb;
    private Vector2 movement;
    private CameraFollow cameraFollow;
    private CameraBounds2D clubBounds;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    private void Start()
    {
        cameraFollow = FindAnyObjectByType<CameraFollow>();
        clubBounds = FindAnyObjectByType<CameraBounds2D>();
    }

    public void OnMove(InputValue value)
    {
        movement = value.Get<Vector2>();
    }

    public void OnCameraZoom(InputValue inputValue)
    {
        if (cameraFollow == null)
        {
            cameraFollow = FindAnyObjectByType<CameraFollow>();
        }

        if (cameraFollow != null)
        {
            cameraFollow.OnCameraZoom(inputValue);
        }
    }

    private void FixedUpdate()
    {
        if (GameplayInputState.IsBlocked)
        {
            return;
        }

        Vector2 nextPosition = rb.position +
            movement * moveSpeed * Time.fixedDeltaTime;

        rb.MovePosition(ClampToClubBounds(nextPosition));
    }

    private Vector2 ClampToClubBounds(Vector2 desiredPosition)
    {
        if (clubBounds == null)
        {
            clubBounds = FindAnyObjectByType<CameraBounds2D>();
        }

        if (clubBounds == null)
        {
            return desiredPosition;
        }

        Bounds bounds = clubBounds.WorldBounds;
        Vector2 playerExtents = GetSolidColliderExtents();

        return new Vector2(
            Mathf.Clamp(
                desiredPosition.x,
                bounds.min.x + playerExtents.x,
                bounds.max.x - playerExtents.x
            ),
            Mathf.Clamp(
                desiredPosition.y,
                bounds.min.y + playerExtents.y,
                bounds.max.y - playerExtents.y
            )
        );
    }

    private Vector2 GetSolidColliderExtents()
    {
        foreach (Collider2D collider in GetComponents<Collider2D>())
        {
            if (!collider.isTrigger)
            {
                return collider.bounds.extents;
            }
        }

        return new Vector2(0.45f, 0.45f);
    }
}
