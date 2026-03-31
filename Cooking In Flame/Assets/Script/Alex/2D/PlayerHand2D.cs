using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Cursor-as-hand controller. Moves a sprite to the mouse position in world space,
/// handles hover glow, single-click pickup and tag-gated drop for Pickupable2D items.
/// Works correctly with perspective cameras via z=0 plane raycasting.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class PlayerHand2D : MonoBehaviour
{
    [Header("Cursor")]
    public Sprite cursorSprite;
    public Color  cursorColor = Color.white;
    [Range(0.1f, 3f)] public float cursorScale = 0.6f;

    [Header("Pickup")]
    [Tooltip("Layer(s) containing Pickupable2D objects.")]
    public LayerMask pickupLayer;
    [Tooltip("Overlap radius used to detect valid drop surfaces.")]
    public float dropCheckRadius = 0.5f;

    private Camera         mainCam;
    private SpriteRenderer myRenderer;
    private Pickupable2D   heldItem;
    private Pickupable2D   hoveredItem;
    private bool           dropSuppressedThisFrame;

    private readonly Plane worldPlane = new Plane(Vector3.forward, Vector3.zero);

    void Awake()
    {
        mainCam    = Camera.main;
        myRenderer = GetComponent<SpriteRenderer>();
        if (mainCam == null) { Debug.LogError("[PlayerHand2D] No MainCamera found.", this); enabled = false; }
    }

    void Start()
    {
        Cursor.visible   = false;
        Cursor.lockState = CursorLockMode.Confined;
        if (cursorSprite != null && myRenderer != null)
        {
            myRenderer.sprite    = cursorSprite;
            myRenderer.color     = cursorColor;
            transform.localScale = Vector3.one * cursorScale;
        }
    }

    void Update()
    {
        if (Mouse.current == null || mainCam == null) return;

        Vector3 worldPos = GetMouseWorldPosition();
        transform.position = worldPos;

        if (heldItem != null)
            heldItem.transform.position = worldPos;
        else
            UpdateHover(worldPos);

        if (!dropSuppressedThisFrame && Mouse.current.leftButton.wasPressedThisFrame)
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
    /// Prevents the LMB drop/pickup action firing this frame.
    /// Called by IngredientShrinker2D or IngredientMerger2D when they consume the click.
    /// </summary>
    public void SuppressDropThisFrame() => dropSuppressedThisFrame = true;

    /// <summary>
    /// Immediately drops the held item unconditionally (no tag check).
    /// Called by IngredientMerger2D after it has already validated the drop surface,
    /// so PlayerHand2D does not need to repeat the surface check on the same click.
    /// </summary>
    public void DropHeldItem()
    {
        if (heldItem == null) return;
        heldItem.OnDrop();
        heldItem = null;
        dropSuppressedThisFrame = true; // prevent the normal LMB path from also firing
    }

    /// <summary>
    /// Immediately picks up <paramref name="target"/>, dropping whatever is
    /// currently held. Used by Spawnable2D to hand off a freshly spawned item.
    /// </summary>
    public void ForcePickUp(Pickupable2D target)
    {
        if (target == null) return;
        if (heldItem != null) { heldItem.OnDrop(); heldItem = null; }
        target.SetHovered(false);
        hoveredItem = null;
        target.OnPickup();
        heldItem = target;
        heldItem.transform.position = transform.position;
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