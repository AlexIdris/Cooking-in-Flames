using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Cursor-as-hand controller. The hand cursor is a UI Image on a Screen Space Overlay
/// canvas so it always renders above ALL other canvases, world-space sprites, and UI
/// elements regardless of sorting order or canvas stack position.
///
/// The cursor moves to follow the mouse in screen space. World-space raycasting still
/// uses z=0 plane projection for all pickup/drop logic — only the visual is UI-based.
///
/// SCRIPT EXECUTION ORDER
/// ───────────────────────
/// Set IngredientMerger2D BEFORE PlayerHand2D so DropHeldItem() sets
/// dropSuppressedThisFrame before PlayerHand2D.Update() reads LMB.
///
/// SETUP
/// ──────
/// 1. Create a Canvas: Render Mode = Screen Space - Overlay, Sort Order = 32767.
/// 2. Inside it create an Image child — this is the hand cursor.
/// 3. Attach PlayerHand2D to the Image GameObject.
/// 4. Assign cursorSprite (or leave the Image sprite set in the Inspector).
/// 5. The Image pivot should be (0, 1) top-left to match OS cursor hot-spot.
/// </summary>
[RequireComponent(typeof(RectTransform))]
[RequireComponent(typeof(Image))]
public class PlayerHand2D : MonoBehaviour
{
    [Header("Cursor")]
    [Tooltip("Sprite for the hand cursor. Can also be set directly on the Image component.")]
    public Sprite cursorSprite;
    public Color  cursorColor = Color.white;
    [Range(0.1f, 3f)] public float cursorScale = 0.6f;

    [Header("Pickup")]
    [Tooltip("Layer(s) containing Pickupable2D objects.")]
    public LayerMask pickupLayer;
    [Tooltip("Overlap radius used to detect valid drop surfaces.")]
    public float dropCheckRadius = 0.5f;

    private Camera        mainCam;
    private Image         cursorImage;
    private RectTransform cursorRect;
    private Canvas        overlayCanvas;
    private Pickupable2D  heldItem;
    private Pickupable2D  hoveredItem;
    private bool          dropSuppressedThisFrame;
    private bool          interactionEnabled = false;
    private Vector3       currentWorldPosition;   // last computed world position of the cursor

    private readonly Plane worldPlane = new Plane(Vector3.forward, Vector3.zero);

    void Awake()
    {
        mainCam      = Camera.main;
        cursorImage  = GetComponent<Image>();
        cursorRect   = GetComponent<RectTransform>();
        overlayCanvas = GetComponentInParent<Canvas>();

        if (mainCam == null)
        { Debug.LogError("[PlayerHand2D] No MainCamera found.", this); enabled = false; }
        if (overlayCanvas == null)
        { Debug.LogError("[PlayerHand2D] PlayerHand2D must be a child of a Canvas.", this); enabled = false; }
        if (overlayCanvas != null && overlayCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
            Debug.LogWarning("[PlayerHand2D] Parent Canvas should use 'Screen Space - Overlay' " +
                             "to guarantee the cursor appears above all other UI.", this);
    }

    void Start()
    {
        Cursor.visible   = false;
        Cursor.lockState = CursorLockMode.Confined;

        if (cursorImage != null)
        {
            if (cursorSprite != null) cursorImage.sprite = cursorSprite;
            cursorImage.color    = cursorColor;
            cursorImage.raycastTarget = false;   // cursor must never block UI clicks
        }

        if (cursorRect != null)
        {
            cursorRect.pivot          = new Vector2(0f, 1f);  // top-left hot-spot
            cursorRect.sizeDelta      = Vector2.one * 64f * cursorScale;
            cursorRect.anchorMin      = Vector2.zero;
            cursorRect.anchorMax      = Vector2.zero;
        }
    }

    void Update()
    {
        if (Mouse.current == null || mainCam == null) return;

        // ── Move UI cursor to mouse position in screen space ─────────────────
        Vector2 screenPos = Mouse.current.position.ReadValue();
        if (cursorRect != null)
            cursorRect.anchoredPosition = screenPos;

        // ── World-space position for raycasting and held item tracking ─────────
        currentWorldPosition = GetMouseWorldPosition();

        if (heldItem != null)
            heldItem.transform.position = currentWorldPosition;
        else if (interactionEnabled)
            UpdateHover(currentWorldPosition);

        if (interactionEnabled && !dropSuppressedThisFrame && Mouse.current.leftButton.wasPressedThisFrame)
        {
            if (heldItem != null) TryDrop();
            else if (hoveredItem != null) PickUp(hoveredItem);
        }

        dropSuppressedThisFrame = false;
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>True while an item is being carried.</summary>
    public bool IsHoldingItem => heldItem != null;

    /// <summary>Returns the currently held Pickupable2D, or null.</summary>
    public Pickupable2D GetHeldItem() => heldItem;

    /// <summary>
    /// The cursor's last known position in world space (z = 0 plane).
    /// Use this instead of transform.position — the cursor is now a UI Image
    /// whose transform lives in screen-pixel coordinates, not world space.
    /// Processing2D and any other world-space scripts must use this property
    /// for all overlap and proximity checks.
    /// </summary>
    public Vector3 WorldPosition => currentWorldPosition;

    /// <summary>
    /// Returns the Pickupable2D the cursor is currently hovering over, or null.
    /// Used by IngredientMerger2D to check whether the player is hovering the
    /// output before allowing a pickup — ensuring genuine contact is required.
    /// </summary>
    public Pickupable2D GetHoveredItem() => hoveredItem;

    /// <summary>
    /// Enables or disables all pickup and drop interaction.
    /// Called by ShopToggle — false on scene load, true when the shop opens,
    /// false again when the day ends and the panel fades back in.
    /// While disabled the cursor still moves but nothing can be grabbed or dropped.
    /// </summary>
    public void SetInteractionEnabled(bool enabled)
    {
        interactionEnabled = enabled;

        // Clear hover state immediately when locking so no item stays highlighted
        if (!enabled && hoveredItem != null)
        {
            hoveredItem.SetHovered(false);
            hoveredItem = null;
        }
    }

    /// <summary>
    /// True while pickup/drop interaction is active (shop is open).
    /// Read by Spawnable2D to gate its own spawn behaviour.
    /// </summary>
    public bool IsInteractionEnabled => interactionEnabled;

    /// <summary>
    /// Suppresses the LMB drop/pickup action for this frame.
    /// Called by external scripts that have already handled the click.
    /// </summary>
    public void SuppressDropThisFrame() => dropSuppressedThisFrame = true;

    /// <summary>
    /// Drops the held item unconditionally and suppresses PlayerHand2D's own LMB
    /// handling for this frame. Called by IngredientMerger2D after it has validated
    /// that the item is over the plate, so no tag check is needed here.
    /// </summary>
    public void DropHeldItem()
    {
        if (heldItem == null) return;
        heldItem.OnDrop();
        heldItem                = null;
        dropSuppressedThisFrame = true;
    }

    /// <summary>
    /// Returns true if the currently held item's allowedDropTags list contains
    /// <paramref name="surfaceTag"/>. Use this before calling DropHeldItem() to
    /// ensure the drop surface is permitted by the ingredient's own rules.
    /// Returns true when nothing is held (no restriction to check).
    /// </summary>
    public bool CanDropOnTag(string surfaceTag)
    {
        if (heldItem == null) return true;
        foreach (string tag in heldItem.allowedDropTags)
            if (string.Equals(tag, surfaceTag, System.StringComparison.OrdinalIgnoreCase))
                return true;
        return false;
    }

    /// <summary>
    /// Releases the held item in place — no tag-surface check, no suppression.
    /// Used by IngredientHolder2D when the player right-clicks to deposit an item.
    /// Unlike DropHeldItem(), this does NOT set dropSuppressedThisFrame so other
    /// scripts can still react to the same frame if needed.
    /// </summary>
    public void ReleaseHeldItem()
    {
        if (heldItem == null) return;
        heldItem.OnDrop();
        heldItem = null;
    }

    /// <summary>
    /// Picks up <paramref name="target"/> and attaches it to the cursor.
    /// Returns true on success. Returns false and does nothing if the hand is
    /// already holding an item — the player must drop their current item first.
    ///
    /// Callers (Spawnable2D, IngredientMerger2D, IngredientHolder2D) should check
    /// the return value and abort their spawn/pickup logic if false.
    /// </summary>
    public bool ForcePickUp(Pickupable2D target)
    {
        if (target == null)   return false;
        if (heldItem != null) return false;   // hand is full — player must drop first

        target.SetHovered(false);
        hoveredItem = null;
        target.OnPickup();
        heldItem = target;
        heldItem.transform.position = currentWorldPosition;
        return true;
    }

    // ── Private ───────────────────────────────────────────────────────────────

    private void PickUp(Pickupable2D target)
    {
        target.SetHovered(false);
        hoveredItem = null;
        target.OnPickup();
        heldItem = target;
    }

    private void TryDrop()
    {
        Collider2D col = heldItem.GetComponent<Collider2D>();
        bool wasEnabled = col != null && col.enabled;
        if (col != null) col.enabled = true;

        bool dropped = false;
        foreach (Collider2D hit in Physics2D.OverlapCircleAll(heldItem.transform.position, dropCheckRadius))
        {
            if (hit.gameObject == heldItem.gameObject) continue;
            foreach (string tag in heldItem.allowedDropTags)
            {
                if (!hit.CompareTag(tag)) continue;
                heldItem.OnDrop();
                heldItem = null;
                dropped  = true;
                break;
            }
            if (dropped) break;
        }

        if (!dropped && col != null) col.enabled = wasEnabled;
    }

    private void UpdateHover(Vector2 mousePos)
    {
        if (hoveredItem != null) { hoveredItem.SetHovered(false); hoveredItem = null; }
        Collider2D hit = Physics2D.OverlapPoint(mousePos, pickupLayer);
        if (hit == null) return;
        Pickupable2D target = hit.GetComponent<Pickupable2D>();
        if (target != null && target.CanBePickedUp()) { hoveredItem = target; target.SetHovered(true); }
    }

    private Vector3 GetMouseWorldPosition()
    {
        Vector2 screen = Mouse.current.position.ReadValue();
        Ray ray = mainCam.ScreenPointToRay(screen);
        if (worldPlane.Raycast(ray, out float dist)) return ray.GetPoint(dist);
        Vector3 p = mainCam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, mainCam.nearClipPlane));
        p.z = 0f;
        return p;
    }

    void OnDisable()
    {
        Cursor.visible   = true;
        Cursor.lockState = CursorLockMode.None;
        if (hoveredItem != null) { hoveredItem.SetHovered(false); hoveredItem = null; }
    }
}