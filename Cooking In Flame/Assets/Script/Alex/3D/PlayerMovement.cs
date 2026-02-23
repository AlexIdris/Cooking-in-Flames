using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMovement : MonoBehaviour
{
    [Header("Movement Settings")]
    public float moveSpeed = 5f;           // Walk speed (units/sec)
    public float gravity = -9.81f;          // Earth gravity
    public float groundCheckDistance = 0.4f; // How far down to check ground

    [Header("Advanced")]
    public LayerMask groundMask = 1;       // Ground layer (default everything)
    public Transform groundCheck;          // Optional: Child sphere for ground check

    private CharacterController controller;
    private Camera playerCamera;
    private Vector3 velocity;              // Current velocity (Y for gravity)
    private bool isGrounded;

    void Start()
    {
        controller = GetComponent<CharacterController>();
        if (controller == null)
        {
            Debug.LogError("PlayerMovement: Add CharacterController to Player!");
            return;
        }

        playerCamera = GetComponentInChildren<Camera>();
        if (playerCamera == null)
        {
            Debug.LogError("PlayerMovement: No Camera child!");
            return;
        }

        // Auto-create ground check point
        if (groundCheck == null)
        {
            GameObject check = new GameObject("GroundCheck");
            check.transform.SetParent(transform);
            check.transform.localPosition = Vector3.zero;
            groundCheck = check.transform;
        }
    }

    void Update()
    {
        // Ground check (sphere cast down)
        isGrounded = Physics.CheckSphere(groundCheck.position, controller.radius * 0.9f, groundMask);
        if (isGrounded && velocity.y < 0)
        {
            velocity.y = -2f; // Small snap to ground
        }

        HandleMovement();
        ApplyGravity();

        // Move player
        controller.Move(velocity * Time.deltaTime);
    }

    void HandleMovement()
    {
        // WASD input (Vector2: x=horizontal, y=vertical)
        Vector2 input = Vector2.zero;
        if (Keyboard.current != null)
        {
            input.x = Keyboard.current.aKey.isPressed ? -1f : 0f;
            input.x += Keyboard.current.dKey.isPressed ? 1f : 0f;
            input.y = Keyboard.current.wKey.isPressed ? 1f : 0f;
            input.y -= Keyboard.current.sKey.isPressed ? 1f : 0f;

            // Normalize diagonal (no speed boost)
            input = input.normalized;
        }

        // World direction: Relative to camera (Y rotation only)
        Vector3 moveDir = transform.right * input.x + transform.forward * input.y;
        moveDir *= moveSpeed;

        // X/Z velocity (flatten Y)
        velocity.x = moveDir.x;
        velocity.z = moveDir.z;
    }

    void ApplyGravity()
    {
        velocity.y += gravity * Time.deltaTime;
    }

    // Visualize in Scene View
    void OnDrawGizmosSelected()
    {
        if (groundCheck != null)
        {
            Gizmos.color = isGrounded ? Color.green : Color.red;
            Gizmos.DrawWireSphere(groundCheck.position, controller.radius * 0.9f);
        }
    }
}