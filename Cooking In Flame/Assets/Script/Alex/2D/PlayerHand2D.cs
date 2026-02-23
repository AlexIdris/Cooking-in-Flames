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

    private Camera mainCam;
    private SpriteRenderer myRenderer;
    private Pickupable2D heldItem;
    private Pickupable2D hoveredItem;

    void Awake()
    {
        mainCam = Camera.main;

        if (mainCam == null)
        {
            Debug.LogError("[PlayerHand2D] No Camera tagged 'MainCamera' found in scene!", this);
            enabled = false;
            return;
        }

        myRenderer = GetComponent<SpriteRenderer>();
    }

    void Start()
    {
        // Hide OS mouse cursor
        Cursor.visible = false;

        if (cursorSprite != null && myRenderer != null)
        {
            myRenderer.sprite = cursorSprite;
            myRenderer.color = cursorColor;
            transform.localScale = Vector3.one * cursorScale;
        }
        else
        {
            Debug.LogWarning("[PlayerHand2D] No cursor sprite assigned or no SpriteRenderer.");
        }

        // Debug: Confirm Input System is active
        if (Mouse.current == null)
        {
            Debug.LogError("[PlayerHand2D] Mouse.current is NULL → Input System not active! " +
                           "Go to Edit → Project Settings → Player → Active Input Handling → 'Input System Package (New)'");
        }
        else
        {
            Debug.Log("[PlayerHand2D] Input System active. Cursor should now follow mouse.");
        }
    }

    void Update()
    {
        // Safety: skip if no mouse or camera
        if (Mouse.current == null || mainCam == null) return;

        // Get mouse position in world space (this is the FIXED line)
        Vector2 mouseWorldPos = mainCam.ScreenToWorldPoint(Mouse.current.position.ReadValue());

        // Move the cursor/hand to mouse position
        transform.position = mouseWorldPos;

        // Handle hover only if not holding anything
        if (heldItem == null)
        {
            HandleHover(mouseWorldPos);
        }

        // Hold/release logic
        if (Mouse.current.leftButton.isPressed)
        {
            if (heldItem == null && hoveredItem != null)
            {
                PickupItem(hoveredItem);
            }
        }
        else if (Mouse.current.leftButton.wasReleasedThisFrame)
        {
            if (heldItem != null)
            {
                DropItem();
            }
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
}