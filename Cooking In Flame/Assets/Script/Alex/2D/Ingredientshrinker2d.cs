using UnityEngine;
using System.Collections;

/// <summary>
/// Attach to an ingredient prefab (the same GameObject that has Pickupable2D).
///
/// BEHAVIOUR
/// ──────────
/// While the player holds this ingredient and it overlaps a trigger whose tag
/// matches plateTag (default "plate"), the ingredient is continuously scaled down
/// by shrinkRate per second — giving a smooth, visible shrink as it hovers over
/// the plate. Shrinking stops the moment the ingredient leaves the plate area or
/// is dropped. The scale is clamped to minimumScale so the item never disappears.
///
/// Because Pickupable2D.OnPickup() disables the ingredient's own Collider2D,
/// Unity cannot fire OnTriggerEnter/Stay on the ingredient itself while held.
/// Instead, Update() calls Physics2D.OverlapCircle at the item's world position
/// each frame to check for any nearby plate-tagged trigger independently.
///
/// NotifyShrink() is called every frame the scale changes so Pickupable2D's
/// internal baseline stays in sync — OnDrop() will restore to the new smaller
/// size rather than snapping back to the original prefab size.
/// </summary>
[RequireComponent(typeof(Pickupable2D))]
public class IngredientShrinker2D : MonoBehaviour
{
    [Header("Detection")]
    [Tooltip("Tag of the plate/surface triggers that activate shrinking.\n" +
             "Must match the tag on the plate GameObject exactly (case-sensitive).")]
    public string plateTag = "plate";

    [Tooltip("Radius around the ingredient's world position used to detect plate triggers.\n" +
             "Should be large enough to overlap the plate collider while the item is held.")]
    public float detectionRadius = 0.6f;

    [Header("Shrink")]
    [Tooltip("Scale reduction per second while over a plate. Applied as a multiplier:\n" +
             "current scale × (1 - shrinkRate × deltaTime) each frame.\n\n" +
             "0.5 = halves size in ~1.4 seconds.\n" +
             "1.0 = reaches minimum almost immediately.")]
    [Range(0.01f, 5f)] public float shrinkRate = 0.5f;

    [Tooltip("Per-axis minimum localScale the ingredient can reach. Prevents it\n" +
             "disappearing entirely. 0 = no floor (not recommended).")]
    [Min(0.01f)] public float minimumScale = 0.05f;

    [Header("Flash Feedback")]
    [Tooltip("Colour briefly flashed on the ingredient when shrinking begins\n" +
             "(transitions from idle to over-plate). Alpha 0 = disabled.")]
    public Color shrinkFlashColor = new Color(0.4f, 0.8f, 1f, 1f);
    [Range(0f, 1f)] public float flashDuration = 0.15f;

    private Pickupable2D pickup;
    private bool         wasOverPlate  = false;
    private Coroutine    flashCoroutine;

    void Awake()
    {
        pickup = GetComponent<Pickupable2D>();
    }

    void Update()
    {
        // Only active while the player is holding this ingredient
        if (!pickup.IsHeld) { wasOverPlate = false; return; }

        // Detect any plate-tagged trigger overlapping the held item's position.
        // Physics2D.OverlapCircle works even though this object's own collider is
        // disabled (Pickupable2D.OnPickup disables it), because we are checking for
        // OTHER colliders (the plate), not our own.
        Collider2D plateCollider = Physics2D.OverlapCircle(
            transform.position, detectionRadius,
            ~0,                        // all layers
            -100f, 100f                // full Z range for 2D
        );

        // Filter: we need a trigger with the right tag
        bool overPlate = false;
        if (plateCollider != null && plateCollider.isTrigger && plateCollider.CompareTag(plateTag))
            overPlate = true;

        if (!overPlate)
        {
            // Scan all overlapping colliders in case multiple are in range
            Collider2D[] nearby = Physics2D.OverlapCircleAll(transform.position, detectionRadius);
            foreach (Collider2D col in nearby)
            {
                if (col.isTrigger && col.CompareTag(plateTag)) { overPlate = true; break; }
            }
        }

        if (overPlate)
        {
            // Flash once when the item first enters the plate area
            if (!wasOverPlate && flashDuration > 0f && shrinkFlashColor.a > 0f)
            {
                if (flashCoroutine != null) StopCoroutine(flashCoroutine);
                flashCoroutine = StartCoroutine(FlashItem());
            }

            ApplyShrink();
        }

        wasOverPlate = overPlate;
    }

    // ── Shrink ────────────────────────────────────────────────────────────────

    private void ApplyShrink()
    {
        Vector3 current = transform.localScale;

        // All axes already at or below the floor — nothing to do
        if (current.x <= minimumScale && current.y <= minimumScale && current.z <= minimumScale)
            return;

        // Smooth continuous reduction: lerp toward zero, clamped at minimum
        float factor   = Mathf.Max(0f, 1f - shrinkRate * Time.deltaTime);
        Vector3 scaled = current * factor;

        scaled.x = Mathf.Max(scaled.x, minimumScale);
        scaled.y = Mathf.Max(scaled.y, minimumScale);
        scaled.z = Mathf.Max(scaled.z, minimumScale);

        transform.localScale = scaled;

        // Keep Pickupable2D's internal baseline in sync so OnDrop() restores
        // to the new smaller size rather than the original prefab size.
        pickup.NotifyShrink(scaled);
    }

    // ── Flash ─────────────────────────────────────────────────────────────────

    private IEnumerator FlashItem()
    {
        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if (sr == null) yield break;

        Color restore = pickup.heldColor;
        sr.color = shrinkFlashColor;

        float elapsed = 0f;
        while (elapsed < flashDuration)
        {
            elapsed  += Time.deltaTime;
            sr.color  = Color.Lerp(shrinkFlashColor, restore, elapsed / flashDuration);
            yield return null;
        }

        sr.color       = restore;
        flashCoroutine = null;
    }
}