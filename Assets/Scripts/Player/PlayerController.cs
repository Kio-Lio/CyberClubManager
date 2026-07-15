using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 5f;

    private Rigidbody2D rb;
    private Vector2 movement;
    private CameraFollow cameraFollow;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    private void Start()
    {
        cameraFollow = FindAnyObjectByType<CameraFollow>();
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
        rb.MovePosition(rb.position + movement * moveSpeed * Time.fixedDeltaTime);
    }
}
