using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

/// <summary>
/// Single-slot ingredient holder. Attach to any GameObject with a trigger Collider2D.
///
/// ALLOWED DROP TAGS
/// ──────────────────
/// This holder acts as a drop surface exactly like a plate or pot. The ingredient's
/// Pickupable2D.allowedDropTags list must include this GameObject's tag for a deposit
/// to be accepted. Set the holder's tag in the Inspector to match one of the tags
/// in your ingredient's allowedDropTags (e.g. "plate", "pot", or a custom tag like
/// "holder"). If the tag is not in the ingredient's allowedDropTags the deposit is
/// silently rejected — consistent with how PlayerHand2D.TryDrop() works.
///
/// SINGLE SLOT — GUARANTEED
/// ─────────────────────────
/// Exactly one ingredient can be stored at a time. Every entry point (LMB click,
/// OnTriggerEnter2D physics fallback) checks the slot and the allowed-drop tag
/// before accepting a deposit.
///
/// CENTERING
/// ──────────
/// The stored ingredient is pinned to (transform.position + holderOffset) every
/// LateUpdate. Pickupable2D.NotifyShrink() is called on deposit so Pickupable2D's
/// smooth-lerp target matches the display scale and does not fight the pin.
/// Rigidbody2D (if any) is frozen while stored.
///
/// LMB ONLY
/// ─────────
/// DROP  — player holds a tagged ingredient, cursor overlaps this holder, LMB click,
///          AND this holder's tag is in the ingredient's allowedDropTags.
/// PICKUP — player holds nothing, cursor hovers the stored ingredient, LMB click.
///
/// SCRIPT EXECUTION ORDER
/// ───────────────────────
/// Set IngredientHolder2D BEFORE PlayerHand2D in Project Settings.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class IngredientHolder2D : MonoBehaviour
{
    [Header("Tag Filter — Incoming Ingredients")]
    [Tooltip("Only items with this tag can be deposited.\n" +
             "Leave blank to accept any Pickupable2D that has this holder's tag\n" +
             "in its allowedDropTags list.")]
    public string ingredientInputTag = "Ingredient";

    [Header("Display")]
    [Tooltip("World-space offset from this holder's pivot where the ingredient is centred.")]
    public Vector3 holderOffset = Vector3.zero;

    [Tooltip("Uniform scale multiplier applied to the ingredient while stored.\n" +
             "1 = no change from the ingredient's original scale.")]
    [Range(0.05f, 3f)]
    public float displayScale = 1f;

    [Header("Pickup Lockout")]
    [Tooltip("Seconds all input is ignored after the ingredient is picked up.\n" +
             "Prevents the same click immediately re-depositing the item. 0 = disabled.")]
    [Range(0f, 3f)]
    public float lockoutDuration = 0.5f;

    [Header("Transitions")]
    [Tooltip("Seconds the ingredient fades in when deposited. 0 = instant.")]
    [Range(0f, 2f)]
    public float fadeInDuration = 0.2f;

    // ── Private ───────────────────────────────────────────────────────────────

    private Collider2D   myCollider;
    private PlayerHand2D playerHand;

    private GameObject   storedObject;   // ingredient currently in the slot (null = empty)
    private Vector3      storedScale;    // ingredient's localScale BEFORE displayScale applied

    private bool      isLockedOut;
    private Coroutine lockoutCoroutine;
    private Coroutine fadeCoroutine;

    private Vector3 SlotPosition => transform.position + holderOffset;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    void Awake()
    {
        myCollider = GetComponent<Collider2D>();
        if (myCollider == null || !myCollider.isTrigger)
            Debug.LogError($"[IngredientHolder2D] {name}: Collider2D must have " +
                           "'Is Trigger' = true.", this);
    }

    void Start()
    {
        playerHand = FindObjectOfType<PlayerHand2D>();
        if (playerHand == null)
            Debug.LogWarning($"[IngredientHolder2D] {name}: No PlayerHand2D found.", this);
    }

    // ── LateUpdate — pin stored ingredient every frame ────────────────────────

    void LateUpdate()
    {
        if (storedObject == null) return;
        storedObject.transform.position   = SlotPosition;
        storedObject.transform.localScale = storedScale * displayScale;
    }

    // ── Update — LMB handler ──────────────────────────────────────────────────

    void Update()
    {
        if (isLockedOut || Mouse.current == null || playerHand == null) return;
        if (!Mouse.current.leftButton.wasPressedThisFrame) return;

        Pickupable2D held = playerHand.GetHeldItem();

        // ── Path A: nothing held — pick up stored ingredient when hovered ──────
        if (held == null)
        {
            if (storedObject == null) return;

            Pickupable2D slotPickup = storedObject.GetComponent<Pickupable2D>();
            Pickupable2D hovered    = playerHand.GetHoveredItem();

            if (slotPickup == null || !slotPickup.CanBePickedUp() || hovered != slotPickup) return;

            PickUpFromSlot(slotPickup);
            return;
        }

        // ── Path B: holding an ingredient — deposit if over this holder ────────
        if (!held.IsHeld)                                          return;
        if (storedObject != null)                                  return;  // SLOT OCCUPIED
        if (!myCollider.OverlapPoint(held.transform.position))     return;
        if (!TagAllowed(held.gameObject))                          return;

        // Validate allowedDropTags — this holder's tag must be in the ingredient's
        // allowed list, exactly as PlayerHand2D.TryDrop() would require.
        if (!DropTagAllowed(held))
        {
            Debug.Log($"[IngredientHolder2D] '{held.name}' does not allow dropping on " +
                      $"tag '{gameObject.tag}'. Add '{gameObject.tag}' to its allowedDropTags.");
            return;
        }

        // DropHeldItem calls OnDrop (plays click sound, re-enables collider) and
        // sets dropSuppressedThisFrame so PlayerHand2D skips its own LMB path.
        playerHand.DropHeldItem();
        Deposit(held);
    }

    // ── Physics fallback — catches Rigidbody2D-driven drops ───────────────────

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other == null || isLockedOut)  return;
        if (storedObject != null)          return;  // SLOT OCCUPIED

        Pickupable2D pickup = other.GetComponent<Pickupable2D>();
        if (pickup == null || pickup.IsHeld) return;
        if (!TagAllowed(other.gameObject))   return;

        // For physics drops, check allowedDropTags against this holder's tag
        if (!DropTagAllowed(pickup)) return;

        Deposit(pickup);
    }

    // ── Deposit ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Stores the ingredient. The item must already be free (not held) when this
    /// is called — DropHeldItem() must be called first in the LMB path.
    /// </summary>
    private void Deposit(Pickupable2D pickup)
    {
        storedObject = pickup.gameObject;
        storedScale  = pickup.transform.localScale;

        // Sync Pickupable2D's internal smooth-lerp target so it does not fight
        // LateUpdate's position/scale pin with a mismatched targetScale.
        Vector3 displayedScale = storedScale * displayScale;
        pickup.NotifyShrink(displayedScale);

        FreezeRigidbody(storedObject, true);

        storedObject.transform.position   = SlotPosition;
        storedObject.transform.localScale = displayedScale;

        if (fadeInDuration > 0f)
        {
            if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);
            fadeCoroutine = StartCoroutine(FadeIn(storedObject));
        }
    }

    // ── Pickup from slot ──────────────────────────────────────────────────────

    private void PickUpFromSlot(Pickupable2D slotPickup)
    {
        slotPickup.transform.localScale = storedScale;
        slotPickup.NotifyShrink(storedScale);

        FreezeRigidbody(storedObject, false);
        playerHand.ForcePickUp(slotPickup);

        storedObject = null;
        storedScale  = Vector3.one;

        playerHand.SuppressDropThisFrame();

        if (lockoutDuration > 0f)
        {
            if (lockoutCoroutine != null) StopCoroutine(lockoutCoroutine);
            lockoutCoroutine = StartCoroutine(Lockout());
        }
    }

    // ── Validation helpers ────────────────────────────────────────────────────

    /// <summary>
    /// Returns true if the ingredient's own tag filter (ingredientInputTag) allows
    /// this object, or if the filter is blank.
    /// </summary>
    private bool TagAllowed(GameObject obj) =>
        string.IsNullOrEmpty(ingredientInputTag) || obj.CompareTag(ingredientInputTag);

    /// <summary>
    /// Returns true if this holder's own GameObject tag is present in the
    /// ingredient's Pickupable2D.allowedDropTags list.
    /// This mirrors the check PlayerHand2D.TryDrop() performs — the holder is
    /// treated as a drop surface and must carry a tag the ingredient permits.
    /// </summary>
    private bool DropTagAllowed(Pickupable2D pickup)
    {
        if (pickup == null) return false;
        string holderTag = gameObject.tag;
        foreach (string allowed in pickup.allowedDropTags)
        {
            if (string.Equals(allowed, holderTag, System.StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    // ── Physics freeze ────────────────────────────────────────────────────────

    private static void FreezeRigidbody(GameObject obj, bool freeze)
    {
        Rigidbody2D rb = obj.GetComponent<Rigidbody2D>();
        if (rb == null) return;
        if (freeze)
        {
            rb.linearVelocity  = Vector2.zero;
            rb.angularVelocity = 0f;
            rb.bodyType        = RigidbodyType2D.Static;
        }
        else
        {
            rb.bodyType = RigidbodyType2D.Dynamic;
        }
    }

    // ── Coroutines ────────────────────────────────────────────────────────────

    private IEnumerator FadeIn(GameObject obj)
    {
        SpriteRenderer sr = obj?.GetComponent<SpriteRenderer>();
        if (sr == null) yield break;

        Color c = sr.color; c.a = 0f; sr.color = c;
        float elapsed = 0f;

        while (elapsed < fadeInDuration)
        {
            if (obj == null) yield break;
            elapsed += Time.deltaTime;
            c.a      = Mathf.Lerp(0f, 1f, elapsed / fadeInDuration);
            sr.color = c;
            yield return null;
        }

        if (obj != null) { c.a = 1f; sr.color = c; }
        fadeCoroutine = null;
    }

    private IEnumerator Lockout()
    {
        isLockedOut = true;
        yield return new WaitForSeconds(lockoutDuration);
        isLockedOut      = false;
        lockoutCoroutine = null;
    }

    // ── Cleanup ───────────────────────────────────────────────────────────────

    void OnDestroy()
    {
        if (storedObject != null)
            FreezeRigidbody(storedObject, false);
    }
}