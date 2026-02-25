using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(SpriteRenderer))]
public class PlayerHand2D : MonoBehaviour
{
    [Header("Pickup Settings")]
    public float holdDistance = 0.8f;
    public LayerMask pickupLayer;

    [Header("Cursor Icon")]
    public Sprite cursorSprite;
    public Color cursorColor = Color.white;
    [Range(0.1f, 2f)]
    public float cursorScale = 0.6f;

    [Header("Hover Feedback")]
    public float hoverScale = 1.15f;

    [Header("Debug Override (for Scene view gizmo drag during Play)")]
    [Tooltip("If true, script skips setting position → allows manual drag in Scene view while playing")]
    public bool manualOverride = false;

    private Camera mainCam;
    private SpriteRenderer myRenderer;
    private Pickupable2D heldItem;
    private Pickupable2D hoveredItem;

    // Plane at z = 0 — change this if your sprites are at different Z
    private Plane interactionPlane = new Plane(Vector3.forward, Vector3.zero);

    void Awake()
    {
        mainCam = Camera.main;
        myRenderer = GetComponent<SpriteRenderer>();

        if (mainCam == null)
        {
            Debug.LogError("[PlayerHand2D] No MainCamera tagged 'MainCamera' found!", this);
            enabled = false;
            return;
        }
    }

    void Start()
    {
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Confined;  // Keeps input inside Game view

        if (cursorSprite != null && myRenderer != null)
        {
            myRenderer.sprite = cursorSprite;
            myRenderer.color = cursorColor;
            transform.localScale = Vector3.one * cursorScale;
        }

        Debug.Log("[PlayerHand2D] Cursor ready. Camera mode: " + 
                  (mainCam.orthographic ? "Orthographic" : "Perspective"));
    }

    void Update()
    {
        if (Mouse.current == null || mainCam == null) return;

        // ── Get mouse position in screen space ───────────────────────────────
        Vector2 mouseScreenPos = Mouse.current.position.ReadValue();

        // ── Convert to world position (works for BOTH Ortho & Perspective) ───
        Vector3 mouseWorldPos;

        if (mainCam.orthographic)
        {
            // Orthographic: simple direct conversion
            mouseWorldPos = mainCam.ScreenToWorldPoint(new Vector3(mouseScreenPos.x, mouseScreenPos.y, mainCam.nearClipPlane));
            mouseWorldPos.z = 0f; // force to our plane
        }
        else
        {
            // Perspective: cast ray and intersect with z=0 plane
            Ray ray = mainCam.ScreenPointToRay(mouseScreenPos);
            if (interactionPlane.Raycast(ray, out float enter))
            {
                mouseWorldPos = ray.GetPoint(enter);
            }
            else
            {
                // Fallback: use near clip plane (rare)
                mouseWorldPos = mainCam.ScreenToWorldPoint(new Vector3(mouseScreenPos.x, mouseScreenPos.y, mainCam.nearClipPlane));
                mouseWorldPos.z = 0f;
            }
        }

        // Apply position (unless debugging with gizmo)
        if (!manualOverride)
        {
            transform.position = mouseWorldPos;
        }

        // Optional debug log (remove later)
        // if (Time.frameCount % 60 == 0)
        //     Debug.Log($"Cursor at world: {mouseWorldPos}");

        // Hover logic only when not holding
        if (heldItem == null)
        {
            HandleHover(mouseWorldPos);
        }

        // Hold LMB → pickup if hovering
        if (Mouse.current.leftButton.isPressed)
        {
            if (heldItem == null && hoveredItem != null)
            {
                PickupItem(hoveredItem);
            }
        }
        // Release LMB → drop
        else if (Mouse.current.leftButton.wasReleasedThisFrame)
        {
            if (heldItem != null)
            {
                DropItem();
            }
        }

        // ESC to unlock cursor for Editor use
        if (Keyboard.current?.escapeKey.wasPressedThisFrame == true)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    private void HandleHover(Vector2 mousePos)
    {
        if (hoveredItem != null)
        {
            hoveredItem.SetHovered(false);
            hoveredItem = null;
        }

        Collider2D hit = Physics2D.OverlapPoint(mousePos, pickupLayer);
        if (hit != null)
        {
            Pickupable2D target = hit.GetComponent<Pickupable2D>();
            if (target != null && target.CanBePickedUp())
            {
                hoveredItem = target;
                target.SetHovered(true);
            }
        }
    }

    private void PickupItem(Pickupable2D item)
    {
        heldItem = item;
        item.OnPickup();

        item.transform.SetParent(transform);
        item.transform.localPosition = Vector2.right * holdDistance;
        item.transform.localRotation = Quaternion.identity;

        if (hoveredItem == item)
        {
            hoveredItem.SetHovered(false);
            hoveredItem = null;
        }
    }

    private void DropItem()
    {
        if (heldItem == null) return;

        heldItem.transform.SetParent(null);
        heldItem.OnDrop();
        heldItem = null;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = heldItem != null ? Color.green : Color.yellow;
        Gizmos.DrawWireSphere(transform.position, 0.3f);
    }
}