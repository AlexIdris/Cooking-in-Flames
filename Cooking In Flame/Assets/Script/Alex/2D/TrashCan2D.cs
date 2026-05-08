using UnityEngine;
using System.Collections;

/// <summary>
/// Attach to the trash-can world-space sprite GameObject.
/// Requires a Collider2D (Is Trigger) sized to the can.
///
/// BEHAVIOUR
/// ──────────
/// • Glows to hoverColor whenever the player is carrying any item (IsHoldingItem).
/// • Returns to normalColor when the hand is empty.
/// • When the player drops an item into the trigger (allowed via allowedDropTags)
///   the item is destroyed immediately.
///
/// SETUP
/// ──────
/// 1. Add a SpriteRenderer and a Collider2D (Is Trigger) to the trash-can object.
/// 2. Add this script.
/// 3. Set the trash-can GameObject's tag to "trash" (or whichever tag you have in
///    Pickupable2D.allowedDropTags for items you want to be trashable).
/// 4. PlayerHand2D is auto-found at runtime.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(Collider2D))]
public class TrashCan2D : MonoBehaviour
{
    [Header("Visuals")]
    [Tooltip("Sprite colour while the player is NOT holding an item.")]
    public Color normalColor = Color.white;

    [Tooltip("Sprite colour while the player IS holding an item — draws attention to the trash can.")]
    public Color hoverColor  = new Color(1f, 0.4f, 0.4f, 1f);  // soft red

    [Tooltip("How quickly the colour lerps between normal and hover.")]
    [Range(2f, 20f)]
    public float smoothSpeed = 8f;

    [Tooltip("Uniform scale multiplier applied while the player holds an item.\n" +
             "1 = no size change.")]
    [Range(0.9f, 1.5f)]
    public float heldScale = 1.1f;

    [Header("Scene References")]
    [Tooltip("Auto-found at runtime if left blank.")]
    public PlayerHand2D playerHand;

    // ── Private ───────────────────────────────────────────────────────────────

    private SpriteRenderer spriteRend;
    private Vector3        originalScale;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    void Awake()
    {
        spriteRend    = GetComponent<SpriteRenderer>();
        originalScale = transform.localScale;

        Collider2D col = GetComponent<Collider2D>();
        if (col == null || !col.isTrigger)
            Debug.LogWarning($"[TrashCan2D] {name}: Collider2D should be a trigger.", this);
    }

    void Start()
    {
        if (playerHand == null) playerHand = FindObjectOfType<PlayerHand2D>();

        spriteRend.color     = normalColor;
        transform.localScale = originalScale;
    }

    // ── Update — glow while item is held ─────────────────────────────────────

    void Update()
    {
        bool holding = playerHand != null && playerHand.IsHoldingItem;

        Color   targetColor = holding ? hoverColor            : normalColor;
        Vector3 targetScale = holding ? originalScale * heldScale : originalScale;

        // Unscaled delta so the glow animates even when the game is paused
        float dt = Time.unscaledDeltaTime;

        spriteRend.color     = Color.Lerp(spriteRend.color,     targetColor, smoothSpeed * dt);
        transform.localScale = Vector3.Lerp(transform.localScale, targetScale, smoothSpeed * dt);
    }

    // ── Destroy items dropped into the trigger ────────────────────────────────

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other == null) return;

        Pickupable2D pickup = other.GetComponent<Pickupable2D>();
        // Ignore items that are still held by the player — OnDrop fires first
        // and re-enables the collider, then physics reports the overlap
        if (pickup != null && pickup.IsHeld) return;

        // Destroy whatever lands in the trash (tag validation was handled by
        // PlayerHand2D.TryDrop which only drops here if "trash" is in allowedDropTags)
        SpawnCleanupManager.MarkAsHeld(other.gameObject);
        Destroy(other.gameObject);

        Debug.Log($"[TrashCan2D] Destroyed '{other.name}'.");
    }
}