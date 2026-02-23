using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(SpriteRenderer))]
public class PlayerHand2D : MonoBehaviour
{
    [Header("Pickup Settings")]
    public float holdDistance = 0.8f;           // how far the item floats in front of cursor
    public LayerMask pickupLayer;               // layer of pickable objects

    [Header("Cursor Icon")]
    public Sprite cursorSprite;
    public Color cursorColor = Color.white;
    [Range(0.1f, 2f)]
    public float cursorScale = 0.6f;

    [Header("Hover Feedback")]
    public float hoverScale = 1.15f;

    private Camera mainCam;
    private SpriteRenderer myRenderer;
    private Pickupable2D heldItem;
    private Pickupable2D hoveredItem;

    void Awake()
    {
        mainCam = Camera.main;
        myRenderer = GetComponent<SpriteRenderer>();
    }

    void Start()
    {
        Cursor.visible = false;  // hide default OS cursor

        if (cursorSprite != null && myRenderer != null)
        {
            myRenderer.sprite = cursorSprite;
            myRenderer.color = cursorColor;
            transform.localScale = Vector3.one * cursorScale;
        }
    }

    void Update()
    {
        // Move cursor to mouse position
        Vector2 mouseWorld = mainCam.ScreenToWorldPoint(Mouse.current.position.ReadValue());
        transform.position = mouseWorld;

        // Handle hover (glow) only when NOT holding anything
        if (heldItem == null)
        {
            HandleHover(mouseWorld);
        }

        // ── Hold & Release logic ────────────────────────────────────────────────
        bool isHoldingButton = Mouse.current.leftButton.isPressed;

        if (isHoldingButton)
        {
            // While holding button down
            if (heldItem == null && hoveredItem != null)
            {
                // Start pickup if hovering something valid
                PickupItem(hoveredItem);
            }
        }
        else
        {
            // Button was just released
            if (heldItem != null)
            {
                DropItem();
            }
        }
    }

    private void HandleHover(Vector2 mousePos)
    {
        // Clear previous hover
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
                hoveredItem.SetHovered(true);
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

        // Turn off hover glow immediately
        if (hoveredItem == item)
        {
            hoveredItem.SetHovered(false);
            hoveredItem = null;
        }

        Debug.Log($"Picked up: {item.name} (holding LMB)");
    }

    private void DropItem()
    {
        if (heldItem == null) return;

        // Drop exactly where cursor is
        heldItem.transform.SetParent(null);
        heldItem.OnDrop();

        Debug.Log($"Dropped {heldItem.name} at cursor position");

        heldItem = null;
    }

    void OnDrawGizmosSelected()
    {
        if (heldItem != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(transform.position, 0.3f);
        }
    }
}