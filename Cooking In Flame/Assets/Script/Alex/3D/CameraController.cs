using UnityEngine;
using UnityEngine.InputSystem;

public class CameraController : MonoBehaviour
{
    [Header("Mouse Look")]
    public float mouseSensitivity = 2f; // Adjust in Inspector (2–5 is common)

    [Header("Clamp Angles (Vertical only now)")]
    public float minPitch = -90f;       // Up/down limit
    public float maxPitch = 90f;

    public Transform playerBody;        // Drag Player object here (auto-fills)
    public Transform playerCamera;      // Drag Main Camera here (auto-finds)

    private float xRotation = 0f;       // Vertical pitch only
    // No more yRotation variable — we let playerBody rotate freely

    void Start()
    {
        // Auto-assign if not dragged
        if (playerBody == null) playerBody = transform;
        if (playerCamera == null) playerCamera = transform.Find("Main Camera");

        if (playerCamera == null)
        {
            Debug.LogError("CameraController: No 'Main Camera' child found!");
            return;
        }

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        // Reset rotations
        xRotation = 0f;
        playerCamera.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
        playerBody.rotation = Quaternion.identity;
    }

    void Update()
    {
        if (Mouse.current == null) return;

        // ESC → unlock cursor
        if (Keyboard.current?.escapeKey.wasPressedThisFrame == true)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            return;
        }

        // Click to re-lock
        if (Cursor.lockState != CursorLockMode.Locked)
        {
            if (Mouse.current.leftButton.wasPressedThisFrame)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
            return;
        }

        // Mouse movement
        Vector2 mouseDelta = Mouse.current.delta.ReadValue();
        float mouseX = mouseDelta.x * mouseSensitivity * Time.deltaTime;
        float mouseY = mouseDelta.y * mouseSensitivity * Time.deltaTime;

        // ── Vertical (pitch) – still clamped ───────────────────────────────
        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, minPitch, maxPitch);
        playerCamera.localRotation = Quaternion.Euler(xRotation, 0f, 0f);

        // ── Horizontal (yaw) – NO CLAMP anymore! Full 360° freedom ────────
        playerBody.Rotate(Vector3.up * mouseX);
        // That's it — no yRotation variable, no clamping
    }
}