using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Attach to a GameObject with a trigger Collider2D (the "cleanup zone").
/// Periodically checks for orphaned ingredient objects whose collider overlaps
/// THIS zone's trigger bounds. Only objects physically inside the zone are
/// evaluated — nothing outside it is ever touched.
///
/// WHAT COUNTS AS AN ANOMALY (inside the zone)
/// ─────────────────────────────────────────────
/// A Pickupable2D is treated as anomalous when ALL of the following are true:
///   1. Its tag matches anomalyTag (default "ingredient").
///   2. Its collider centre overlaps this zone's Collider2D.
///   3. It is NOT held by the player (IsHeld == false).
///   4. It is NOT placed on any IngredientMerger2D plate.
///   5. It is NOT a spawned output of any IngredientMerger2D.
///   6. It IS registered with SpawnCleanupManager (was created at runtime).
///
/// SETUP
/// ──────
/// 1. Add this component to a zone GameObject.
/// 2. Add a Collider2D to the same GameObject and tick "Is Trigger".
///    Size and shape the collider to cover the area you want monitored.
/// 3. Set anomalyTag to match your ingredient prefab tag (default "ingredient").
/// 4. No other references need to be wired in the Inspector.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class IngredientAnomalyCleanup : MonoBehaviour
{
    [Header("Zone Settings")]
    [Tooltip("Tag that marks objects eligible for anomaly cleanup.\n" +
             "Must match the tag used on ingredient prefabs exactly (case-sensitive).")]
    public string anomalyTag = "ingredient";

    [Header("Timing")]
    [Tooltip("Seconds after the scene loads before the first scan runs.")]
    [Min(0f)] public float initialDelay = 3f;

    [Tooltip("Seconds between each subsequent scan of this zone.")]
    [Min(0.5f)] public float scanInterval = 5f;

    [Header("Removal")]
    [Tooltip("Seconds over which a detected anomaly fades out before being destroyed.\n" +
             "Set to 0 for instant destruction.")]
    [Min(0f)] public float fadeDuration = 0.5f;

    [Tooltip("Log a message each time an anomaly is removed.")]
    public bool logAnomalies = true;

    private Collider2D zoneCollider;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    void Awake()
    {
        zoneCollider = GetComponent<Collider2D>();

        if (zoneCollider == null || !zoneCollider.isTrigger)
            Debug.LogWarning($"[IngredientAnomalyCleanup] {name}: Collider2D must have " +
                             "'Is Trigger' = true for zone detection to work.", this);
    }

    void Start()
    {
        StartCoroutine(ScanLoop());
    }

    // ── Scan loop ─────────────────────────────────────────────────────────────

    private IEnumerator ScanLoop()
    {
        yield return new WaitForSeconds(initialDelay);

        while (true)
        {
            ScanAndClean();
            yield return new WaitForSeconds(scanInterval);
        }
    }

    // ── Core scan ─────────────────────────────────────────────────────────────

    private void ScanAndClean()
    {
        if (zoneCollider == null || !zoneCollider.isTrigger) return;

        // Build the set of every ingredient legitimately placed on any plate
        IngredientMerger2D[] mergers        = FindObjectsOfType<IngredientMerger2D>();
        HashSet<GameObject>  activePlacedSet = new HashSet<GameObject>();

        foreach (IngredientMerger2D merger in mergers)
        {
            foreach (GameObject placed in merger.GetPlacedIngredients())
                if (placed != null) activePlacedSet.Add(placed);
        }

        // Query only the colliders that overlap THIS zone's bounds
        // OverlapCollider fills a pre-allocated array — no scene-wide search
        ContactFilter2D filter  = new ContactFilter2D();
        filter.useTriggers      = true;   // detect items whose collider is a trigger
        filter.useLayerMask     = false;  // accept all layers
        filter.SetLayerMask(Physics2D.AllLayers);

        Collider2D[] hits    = new Collider2D[64];
        int          hitCount = zoneCollider.Overlap(filter, hits);

        int cleaned = 0;

        for (int h = 0; h < hitCount; h++)
        {
            Collider2D hit = hits[h];
            if (hit == null || hit.gameObject == gameObject) continue;

            Pickupable2D pickup = hit.GetComponent<Pickupable2D>();
            if (pickup == null) continue;

            GameObject obj = pickup.gameObject;

            // Guard 1 — tag filter
            if (!obj.CompareTag(anomalyTag)) continue;

            // Guard 2 — never remove something the player is holding
            if (pickup.IsHeld) continue;

            // Guard 3 — never remove an active plate ingredient
            if (activePlacedSet.Contains(obj)) continue;

            // Guard 4 — never remove a spawned output
            if (IngredientMerger2D.IsSpawnedOutputPublic(obj)) continue;

            // Guard 5 — only remove runtime-spawned objects (registered with
            //           SpawnCleanupManager). Scene-placed static props are excluded.
            if (!SpawnCleanupManager.IsRegistered(obj)) continue;

            // All guards passed — this is an orphaned runtime ingredient
            SpawnCleanupManager.MarkAsHeld(obj);

            if (logAnomalies)
                Debug.Log($"[IngredientAnomalyCleanup] '{name}' removing orphaned " +
                          $"ingredient '{obj.name}' inside zone.");

            if (fadeDuration > 0f)
                StartCoroutine(FadeAndDestroy(pickup));
            else
                Destroy(obj);

            cleaned++;
        }

        if (cleaned > 0)
            Debug.Log($"[IngredientAnomalyCleanup] '{name}': removed {cleaned} anomaly(s).");
    }

    // ── Fade & destroy ────────────────────────────────────────────────────────

    private IEnumerator FadeAndDestroy(Pickupable2D pickup)
    {
        if (pickup == null) yield break;

        SpriteRenderer sr = pickup.GetComponent<SpriteRenderer>();
        if (sr == null) { if (pickup != null) Destroy(pickup.gameObject); yield break; }

        float elapsed = 0f;
        Color startC  = sr.color;

        while (elapsed < fadeDuration)
        {
            if (pickup == null) yield break;
            elapsed  += Time.deltaTime;
            Color c   = startC;
            c.a       = Mathf.Lerp(startC.a, 0f, elapsed / fadeDuration);
            sr.color  = c;
            yield return null;
        }

        if (pickup != null) Destroy(pickup.gameObject);
    }
}