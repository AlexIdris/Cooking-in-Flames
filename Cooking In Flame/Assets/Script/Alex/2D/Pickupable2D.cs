using UnityEngine;

/// <summary>
/// Core pickup component. Handles hover glow, held state, drop-tag validation,
/// processing locks, external scale notifications, and click audio feedback.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(Collider2D))]
public class Pickupable2D : MonoBehaviour
{
    [Header("Visuals")]
    public Color normalColor = Color.white;
    public Color hoverColor  = new Color(0.55f, 1f,   1f,    1f);
    public Color heldColor   = new Color(1f,    1f,   0.55f, 1f);
    public Color lockedColor = new Color(1f,    0.6f, 0.6f,  1f);
    public float hoverScale  = 1.15f;
    [Range(4f, 24f)] public float smoothSpeed = 12f;

    [Header("Allowed Drop Destinations")]
    [Tooltip("Item can only be released inside a trigger whose tag is in this list.")]
    public string[] allowedDropTags = { "plate", "pot", "pan", "trash", "customer", "coffee machine" };

    [Header("Audio")]
    [Tooltip("Sound played each time the item is picked up or dropped.")]
    public AudioClip clickSound;

    /// <summary>True while this item is carried by the player.</summary>
    public bool IsHeld { get; private set; }

    /// <summary>True while a Processing2D machine holds this stage locked in place.</summary>
    public bool IsProcessingLocked { get; private set; }

    private SpriteRenderer spriteRend;
    private Collider2D     myCollider;
    private Vector3        originalScale;
    private Color          targetColor;
    private Vector3        targetScale;

    void Awake()
    {
        spriteRend    = GetComponent<SpriteRenderer>();
        myCollider    = GetComponent<Collider2D>();
        originalScale = transform.localScale;
        targetColor   = normalColor;
        targetScale   = originalScale;
        ApplyVisuals(normalColor, originalScale);
    }

    void Update()
    {
        spriteRend.color     = Color.Lerp(spriteRend.color,     targetColor, smoothSpeed * Time.deltaTime);
        transform.localScale = Vector3.Lerp(transform.localScale, targetScale, smoothSpeed * Time.deltaTime);
    }

    // ── Called by PlayerHand2D ────────────────────────────────────────────────

    /// <summary>Returns true when the item is idle and available to be picked up.</summary>
    public bool CanBePickedUp() => !IsHeld && !IsProcessingLocked;

    /// <summary>Applies hover glow. Ignored while held or processing-locked.</summary>
    public void SetHovered(bool hovered)
    {
        if (IsHeld || IsProcessingLocked) return;
        targetColor = hovered ? hoverColor : normalColor;
        targetScale = hovered ? originalScale * hoverScale : originalScale;
    }

    /// <summary>Marks the item as held: disables its collider and notifies the cleanup manager.</summary>
    public void OnPickup()
    {
        IsHeld             = true;
        myCollider.enabled = false;
        targetColor        = heldColor;
        targetScale        = originalScale;
        SpawnCleanupManager.MarkAsHeld(gameObject);
        PlayClickSound();
    }

    /// <summary>Marks the item as dropped: re-enables its collider and restores visuals.</summary>
    public void OnDrop()
    {
        IsHeld             = false;
        myCollider.enabled = true;
        ApplyVisuals(normalColor, originalScale);
        targetColor = normalColor;
        targetScale = originalScale;
        SpawnCleanupManager.MarkAsDropped(gameObject);
        PlayClickSound();
    }

    // ── Called by IngredientShrinker2D ────────────────────────────────────────

    /// <summary>
    /// Updates the internal scale baseline after an external shrink so OnDrop,
    /// SetHovered, and the smooth-lerp all reference the new smaller size.
    /// Call immediately after setting transform.localScale to the new value.
    /// </summary>
    public void NotifyShrink(Vector3 newScale)
    {
        originalScale = newScale;
        targetScale   = newScale;
    }

    // ── Called by Processing2D ────────────────────────────────────────────────

    /// <summary>
    /// Engages (true) or releases (false) the processing lock.
    /// While locked: pickup is blocked and the item tints to lockedColor.
    /// On release: item becomes pickupable again and reverts to normalColor.
    /// </summary>
    public void SetProcessingLock(bool locked)
    {
        IsProcessingLocked = locked;
        Color   col   = locked ? lockedColor : normalColor;
        Vector3 scale = transform.localScale;
        ApplyVisuals(col, scale);
        targetColor = col;
        targetScale = scale;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void PlayClickSound()
    {
        if (clickSound != null)
            AudioSource.PlayClipAtPoint(clickSound, transform.position);
    }

    private void ApplyVisuals(Color color, Vector3 scale)
    {
        if (spriteRend) spriteRend.color = color;
        transform.localScale = scale;
    }
}