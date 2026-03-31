using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Place on a plate or surface with a trigger Collider2D.
/// SINGLE-CLICK CHAIN
/// ───────────────────
/// This script owns the LMB press when the player holds an ingredient over the plate.
/// On that one click it:
///   1. Calls PlayerHand2D.DropHeldItem() — releases the item, re-enables its collider,
///      and sets dropSuppressedThisFrame so PlayerHand2D never double-fires.
///   2. Calls PlaceIngredient() DIRECTLY — does not rely on OnTriggerEnter2D because
///      Unity's Physics2D does not fire trigger callbacks for colliders that were just
///      re-enabled mid-frame. The trigger callback is kept only as a fallback for items
///      dropped by other means (e.g. physics).
///
/// SHRINKING
/// ──────────
/// IngredientShrinker2D now lives on each ingredient prefab, not on the plate.
/// It shrinks the ingredient autonomously while it is held over a plate-tagged object.
///
/// WRONG-COMBO COUNTER
/// ────────────────────
/// failedPlacementCount increments only when the plate holds EXACTLY the number of
/// ingredients required by at least one recipe yet no recipe is satisfied — i.e. a
/// complete wrong set. Partial sets never count.
///
/// SCRIPT EXECUTION ORDER
/// ───────────────────────
/// Set IngredientMerger2D BEFORE PlayerHand2D in Project Settings → Script Execution Order
/// so DropHeldItem() sets dropSuppressedThisFrame before PlayerHand2D.Update() reads LMB.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class IngredientMerger2D : MonoBehaviour
{
    [Header("Recipes")]
    public List<Recipe> recipes = new List<Recipe>();

    [Header("Output")]
    [Tooltip("World-space offset from plate centre where the output spawns.")]
    public Vector2 outputSpawnOffset = Vector2.zero;

    [Tooltip("Minimum number of inputs a matched recipe must have required before\n" +
             "the player is allowed to pick up the output.\n\n" +
             "3 (default) = output is only carriable when the recipe used 3+ ingredients.\n" +
             "0 or 1 = output is always pickupable regardless of ingredient count.")]
    [Min(0)] public int minInputsToPickUpOutput = 3;

    [Header("Ingredient Scale — Stored (Permanent)")]
    [Tooltip("One-time shrink on first placement. Persists when picked back up. 1 = no shrink.")]
    [Range(0.05f, 1f)] public float storedIngredientScale = 0.25f;

    [Header("Ingredient Scale — Display (On Plate)")]
    [Tooltip("Additional multiplier applied on top of stored scale while on the plate.\n" +
             "Individual recipes can override this value.")]
    [Range(0.05f, 1f)] public float placedIngredientScale = 0.4f;
    [Tooltip("World-space offset of the ingredient row from this object's centre.\n" +
             "X = left/right shift of the entire row's centre point.\n" +
             "Y = up/down shift (negative = below the plate).\n" +
             "Z = depth offset (useful for perspective layering).")]
    public Vector3 ingredientRowOffset = new Vector3(0f, -0.55f, 0f);

    [Header("Ingredient Input Filter")]
    [Tooltip("Only objects with this tag can be placed on this plate as ingredients.\n" +
             "Leave blank to allow any tag.")]
    public string ingredientInputTag = "Ingredient";

    [Tooltip("Total world-space width over which placed ingredients are spread along the X axis.")]
    public float ingredientRowWidth = 1.6f;

    [Header("Transitions")]
    [Range(0.05f, 2f)] public float fadeDuration = 0.4f;

    [Header("Failed Placement Purge")]
    [Tooltip("How many COMPLETE wrong combinations are allowed before all ingredients\n" +
             "are destroyed. A 'complete wrong combination' is counted only when the\n" +
             "plate holds exactly the required number of ingredients for at least one\n" +
             "recipe, but no recipe is satisfied.\n\n" +
             "Partial sets (fewer ingredients than any recipe needs) never count.\n" +
             "Resets to 0 on a successful match or after a purge.\n" +
             "Set to 0 to disable automatic purging entirely.")]
    [Min(0)] public int maxFailedPlacements = 5;

    [Tooltip("Flash the plate sprite red on each complete wrong combination.")]
    public bool flashOnFailedPlacement = true;
    [Range(0.05f, 1f)] public float failedPlacementFlashDuration = 0.2f;

    [Tooltip("Fade ingredients out before destroying them on purge. False = instant.")]
    public bool fadeOnPurge = true;

    [System.Serializable]
    public class Recipe
    {
        [Tooltip("Label shown in the Inspector and debug log.")]
        public string recipeName = "Recipe";
        [Tooltip("Required ingredient prefabs. Order does not matter. Duplicates are supported.")]
        public List<GameObject> requiredInputs = new List<GameObject>();
        public GameObject outputPrefab;
        [Tooltip("Optional tag all ingredients must share. Leave blank to ignore.")]
        public string requiredTag = "";
        [Tooltip("Display-scale override while this recipe is active. 0 = use global.")]
        [Range(0f, 1f)] public float ingredientScaleOverride = 0f;
    }

    private Collider2D           myCollider;
    private SpriteRenderer       plateSR;
    private Color                plateOriginalColor;
    private PlayerHand2D         playerHand;
    private readonly Dictionary<GameObject, Vector3> placedIngredients = new Dictionary<GameObject, Vector3>();
    private readonly HashSet<GameObject>             alreadyStored     = new HashSet<GameObject>();

    private GameObject currentOutput;
    private Recipe     currentActiveRecipe;
    private bool       isTransitioning;
    private bool       isPurging;
    private int        failedPlacementCount;
    private Coroutine  flashCoroutine;

    // Scene-wide registry of every output instance currently alive.
    // Keyed by instance ID so destroyed objects leave no stale references.
    // Static so ALL plates share the same set — an output spawned on plate A
    // is blocked as an ingredient on plate B without any cross-plate references.
    private static readonly HashSet<int> spawnedOutputIDs = new HashSet<int>();

    /// <summary>Returns true if <paramref name="obj"/> was spawned as an output
    /// by any IngredientMerger2D and has not yet been destroyed.</summary>
    private static bool IsSpawnedOutput(GameObject obj) =>
        obj != null && IsSpawnedOutputOrChild(obj);

    private static bool IsSpawnedOutputOrChild(GameObject obj)
    {
        // Trigger callbacks can come from child colliders/objects.
        // Treat *any* descendant of a spawned output as blocked input.
        Transform t = obj.transform;
        while (t != null)
        {
            if (spawnedOutputIDs.Contains(t.gameObject.GetInstanceID())) return true;
            t = t.parent;
        }
        return false;
    }

    void Awake()
    {
        myCollider = GetComponent<Collider2D>();
        if (myCollider == null || !myCollider.isTrigger)
        { Debug.LogError($"[IngredientMerger2D] {name}: Collider2D must have 'Is Trigger' = true.", this); enabled = false; }
        plateSR = GetComponent<SpriteRenderer>();
        if (plateSR != null) plateOriginalColor = plateSR.color;

    }

    void Start()
    {
        playerHand = FindObjectOfType<PlayerHand2D>();
    }

    // ── Single-click ownership ────────────────────────────────────────────────

    void Update()
    {
        if (isPurging || isTransitioning) return;
        if (Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame) return;
        if (playerHand == null) return;

        Pickupable2D held = playerHand.GetHeldItem();

        // ── Path A: player is holding nothing — pick up output OR ingredient ────
        if (held == null)
        {
            // A1 — pick up the finished output if one is sitting on the plate
            if (currentOutput != null)
            {
                Pickupable2D outPickup = currentOutput.GetComponent<Pickupable2D>();
                if (outPickup != null && outPickup.CanBePickedUp() &&
                    myCollider.OverlapPoint(currentOutput.transform.position))
                {
                    // Guard: output is only carriable when the recipe that produced it
                    // used at least minInputsToPickUpOutput ingredients.
                    int inputsUsed = currentActiveRecipe != null
                        ? currentActiveRecipe.requiredInputs.Count
                        : 0;

                    if (minInputsToPickUpOutput > 0 && inputsUsed < minInputsToPickUpOutput)
                    {
                        // Not enough ingredients were used — silently block pickup.
                        // The output stays on the plate; the player cannot carry it yet.
                        playerHand.SuppressDropThisFrame();
                        return;
                    }

                    // ForcePickUp assigns heldItem inside PlayerHand2D, suppressing
                    // its own LMB branch — no explicit SuppressDropThisFrame needed.
                    playerHand.ForcePickUp(outPickup);
                    // Output is now owned by the player:
                    // - mark it as held so cleanup systems don't delete it
                    // - remove it from the "spawned output" blocklist so it can be recyclable
                    //   (i.e., usable as a normal ingredient on another plate).
                    SpawnCleanupManager.MarkAsHeld(outPickup.gameObject);
                    spawnedOutputIDs.Remove(outPickup.gameObject.GetInstanceID());
                    currentOutput       = null;
                    currentActiveRecipe = null;
                    DestroyAllPlacedIngredients();
                    return;
                }
            }

            // A2 — pick up a placed ingredient so the player can remove or swap it.
            // Scan placed ingredients and pick the first one whose position is under
            // the cursor. ForcePickUp hands it to PlayerHand2D; RemoveIngredient
            // clears it from the plate state so the row and recipe re-evaluate.
            if (placedIngredients.Count > 0)
            {
                Vector2 cursorPos = playerHand.transform.position;
                foreach (GameObject placed in placedIngredients.Keys.ToList())
                {
                    if (placed == null) continue;
                    // Non-matching tags should not be interactable on the plate.
                    if (!string.IsNullOrEmpty(ingredientInputTag) && !placed.CompareTag(ingredientInputTag)) continue;
                    Pickupable2D pick = placed.GetComponent<Pickupable2D>();
                    if (pick == null || !pick.CanBePickedUp()) continue;

                    // Use a small overlap radius so clicking near (not pixel-perfect
                    // on) the ingredient still works.
                    float dist = Vector2.Distance(cursorPos, placed.transform.position);
                    if (dist > 0.5f) continue;

                    // Pick it up — this calls OnPickup which disables the collider,
                    // so OnTriggerExit2D will NOT fire for it.
                    // We must call RemoveIngredient manually before ForcePickUp
                    // so the plate state is clean when the item leaves.
                    RemoveIngredient(placed);
                    playerHand.ForcePickUp(pick);
                    playerHand.SuppressDropThisFrame();
                    return;
                }
            }

            return;
        }

        // ── Path B: player is holding an ingredient — drop and place it ───────
        if (!held.IsHeld) return;

        // Only act when the held item is physically over this plate trigger
        if (!myCollider.OverlapPoint(held.transform.position)) return;

        // Only accept tagged ingredients as inputs
        if (!string.IsNullOrEmpty(ingredientInputTag) && !held.CompareTag(ingredientInputTag)) return;

        // Guard: don't place the same item twice
        if (placedIngredients.ContainsKey(held.gameObject)) return;

        // Guard: never accept a spawned output as an ingredient — on this plate or any other.
        // IsSpawnedOutput checks the static scene-wide registry, so an output picked up
        // from plate A and carried to plate B is still blocked as an ingredient on plate B.
        if (IsSpawnedOutput(held.gameObject)) return;

        // Drop: re-enables the item's collider and suppresses PlayerHand2D's LMB
        playerHand.DropHeldItem();

        // Place directly — we do NOT rely on OnTriggerEnter2D because Unity does
        // not fire Physics2D trigger callbacks for colliders just re-enabled mid-frame.
        PlaceIngredient(held.gameObject);
    }

    // ── Trigger callbacks (fallback for physics-driven drops) ─────────────────

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other == null || isPurging) return;
        GameObject obj = other.gameObject;

        if (IsOutputBeingPickedUp(obj)) { ResetPlate(false); return; }

        Pickupable2D pickup = obj.GetComponent<Pickupable2D>();
        // Reject: not a pickup item, still held, already placed, or a spawned output.
        // IsSpawnedOutput covers both this plate's output and outputs from other plates.
        if (pickup == null || pickup.IsHeld || placedIngredients.ContainsKey(obj)) return;
        if (IsSpawnedOutput(obj)) return;

        // Only accept tagged ingredients as inputs
        if (!string.IsNullOrEmpty(ingredientInputTag) && !obj.CompareTag(ingredientInputTag)) return;

        PlaceIngredient(obj);
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (other == null || isPurging) return;
        GameObject obj = other.gameObject;

        if (IsOutputBeingPickedUp(obj)) { ResetPlate(false); return; }

        if (placedIngredients.ContainsKey(obj))
        {
            Pickupable2D pickup = obj.GetComponent<Pickupable2D>();
            if (pickup != null && pickup.IsHeld) RemoveIngredient(obj);
        }
    }

    private bool IsOutputBeingPickedUp(GameObject obj)
    {
        if (currentOutput == null) return false;
        // obj may be a child of the output — walk up to the root and compare
        bool isOutputOrChild = obj == currentOutput ||
                               obj.transform.IsChildOf(currentOutput.transform);
        if (!isOutputOrChild) return false;
        Pickupable2D p = currentOutput.GetComponent<Pickupable2D>();
        return p != null && p.IsHeld;
    }

    // ── Placement ─────────────────────────────────────────────────────────────

    private void PlaceIngredient(GameObject obj)
    {
        // Permanent one-time stored-size shrink
        if (!alreadyStored.Contains(obj))
        {
            obj.transform.localScale *= storedIngredientScale;
            alreadyStored.Add(obj);
        }

        Vector3 stored = obj.transform.localScale;
        placedIngredients.Add(obj, stored);
        obj.transform.localScale = stored * ResolveDisplayScale(currentActiveRecipe);

        RepositionIngredients();
        EvaluateRecipes();
    }

    private void RemoveIngredient(GameObject obj)
    {
        if (!placedIngredients.TryGetValue(obj, out Vector3 stored)) return;
        placedIngredients.Remove(obj);
        obj.transform.localScale = stored;
        RepositionIngredients();

        if (currentOutput != null)
        {
            StartCoroutine(FadeOutAndDestroy(currentOutput));
            currentOutput       = null;
            currentActiveRecipe = null;
        }

        ReapplyDisplayScale(null);
        EvaluateRecipes();
    }

    // ── Layout ────────────────────────────────────────────────────────────────

    private void RepositionIngredients()
    {
        List<GameObject> items = placedIngredients.Keys.ToList();
        int     count  = items.Count;
        if (count == 0) return;

        // Row anchor: plate centre shifted by the full 3-axis inspector offset.
        Vector3 anchor = transform.position + ingredientRowOffset;

        // Slot-centre formula — divides ingredientRowWidth into 'count' equal slots
        // and places each ingredient at the centre of its slot.
        // This produces identical gaps between all adjacent items AND between the
        // outermost items and the row boundary, regardless of ingredient count.
        //
        // slot width  = ingredientRowWidth / count
        // item i X    = anchor.x - ingredientRowWidth/2 + slotWidth * (i + 0.5)
        float slotWidth = ingredientRowWidth / count;
        for (int i = 0; i < count; i++)
        {
            if (items[i] == null) continue;
            float x = anchor.x - ingredientRowWidth * 0.5f + slotWidth * (i + 0.5f);
            items[i].transform.position = new Vector3(x, anchor.y, anchor.z);
        }
    }

    // ── Scale helpers ─────────────────────────────────────────────────────────

    private float ResolveDisplayScale(Recipe recipe) =>
        recipe != null && recipe.ingredientScaleOverride > 0f ? recipe.ingredientScaleOverride : placedIngredientScale;

    private void ReapplyDisplayScale(Recipe recipe)
    {
        float scale = ResolveDisplayScale(recipe);
        foreach (KeyValuePair<GameObject, Vector3> kvp in placedIngredients)
            if (kvp.Key != null) kvp.Key.transform.localScale = kvp.Value * scale;
    }

    // ── Recipe evaluation ─────────────────────────────────────────────────────

    private void EvaluateRecipes()
    {
        if (isTransitioning || isPurging) return;

        Recipe bestMatch               = null;
        int    bestCount               = -1;
        bool   plateCountMatchesRecipe = false;   // true when plate is "full" for some recipe

        foreach (Recipe recipe in recipes)
        {
            if (recipe.requiredInputs.Count == 0 || recipe.outputPrefab == null) continue;

            // Only evaluate when the plate has exactly the right ingredient count.
            // Partial sets (fewer items) are never a wrong combo — just incomplete.
            if (placedIngredients.Count != recipe.requiredInputs.Count) continue;

            plateCountMatchesRecipe = true;
            int matched = CountMatches(recipe);
            if (matched == recipe.requiredInputs.Count && matched > bestCount)
            { bestMatch = recipe; bestCount = matched; }
        }

        if (bestMatch != null && bestMatch != currentActiveRecipe)
        {
            // Correct full set — reset counter and show output
            failedPlacementCount = 0;
            StartCoroutine(ShowOutput(bestMatch));
            return;
        }

        // Only count as a failed attempt when the plate is actually "full" for some recipe.
        // Placing a first ingredient toward a 3-ingredient recipe is not a failure.
        if (maxFailedPlacements > 0 && plateCountMatchesRecipe)
        {
            failedPlacementCount++;

            if (flashOnFailedPlacement && plateSR != null)
            {
                if (flashCoroutine != null) StopCoroutine(flashCoroutine);
                flashCoroutine = StartCoroutine(FlashPlate());
            }

            if (failedPlacementCount >= maxFailedPlacements)
            {
                if (fadeOnPurge) StartCoroutine(PurgeWithFade());
                else             PurgeInstant();
            }
        }
    }

    private int CountMatches(Recipe recipe)
    {
        List<GameObject> remaining = new List<GameObject>(recipe.requiredInputs);
        int matched = 0;
        foreach (GameObject placed in placedIngredients.Keys)
        {
            if (placed == null) continue;
            for (int i = 0; i < remaining.Count; i++)
            {
                if (remaining[i] == null) continue;
                bool nameOk = placed.name.Contains(remaining[i].name);
                bool tagOk  = string.IsNullOrEmpty(recipe.requiredTag) || placed.CompareTag(recipe.requiredTag);
                if (!nameOk || !tagOk) continue;
                matched++;
                remaining.RemoveAt(i);
                break;
            }
        }
        return matched;
    }

    // ── Output ────────────────────────────────────────────────────────────────

    private IEnumerator ShowOutput(Recipe recipe)
    {
        isTransitioning = true;
        if (currentOutput != null) { yield return StartCoroutine(FadeOutAndDestroy(currentOutput)); currentOutput = null; }

        Vector3    pos    = (Vector3)((Vector2)transform.position + outputSpawnOffset);
        GameObject newOut = Instantiate(recipe.outputPrefab, pos, Quaternion.identity);
        SpawnCleanupManager.RegisterSpawnedObject(newOut);
        spawnedOutputIDs.Add(newOut.GetInstanceID());   // mark as output — blocks ingredient placement
        currentOutput       = newOut;
        currentActiveRecipe = recipe;
        ReapplyDisplayScale(recipe);

        // ── Fade in ───────────────────────────────────────────────────────────
        SpriteRenderer rend = newOut.GetComponent<SpriteRenderer>();
        if (rend != null)
        {
            Color c = rend.color; c.a = 0f; rend.color = c;
            float elapsed = 0f;
            while (elapsed < fadeDuration)
            {
                if (newOut == null) break;
                elapsed += Time.deltaTime;
                c.a = Mathf.Clamp01(elapsed / fadeDuration);
                rend.color = c;
                yield return null;
            }
            if (newOut != null) { c.a = 1f; rend.color = c; }
        }

        isTransitioning = false;

        // Output sits on the plate after fading in. The player picks it up by
        // clicking LMB over the plate — handled in Update() Path A.
        // If the output prefab has no Pickupable2D it will simply sit in place
        // and cannot be interacted with.
        if (newOut != null)
        {
            Pickupable2D outPickup = newOut.GetComponent<Pickupable2D>();
            if (outPickup == null)
                Debug.LogWarning($"[IngredientMerger2D] {name}: Output prefab " +
                    $"'{recipe.outputPrefab.name}' has no Pickupable2D — " +
                    "it cannot be picked up by the player.", this);
        }
    }

    // ── Ingredient cleanup helper ────────────────────────────────────────────────

    /// <summary>
    /// Destroys all placed ingredients and clears tracking state.
    /// Called after the output is handed to the player so the plate is immediately
    /// ready for a new set of ingredients.
    /// </summary>
    private void DestroyAllPlacedIngredients()
    {
        foreach (GameObject obj in placedIngredients.Keys.ToList())
        { if (obj != null) { SpawnCleanupManager.MarkAsHeld(obj); Destroy(obj); } }
        placedIngredients.Clear();
        alreadyStored.Clear();
        failedPlacementCount = 0;
    }

    // ── Purge ─────────────────────────────────────────────────────────────────

    private IEnumerator PurgeWithFade()
    {
        isPurging = true;
        List<GameObject> toPurge    = placedIngredients.Keys.Where(o => o != null).ToList();
        Color[]          startColors = toPurge.Select(o =>
        {
            var sr = o.GetComponent<SpriteRenderer>();
            return sr != null ? sr.color : Color.white;
        }).ToArray();

        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / fadeDuration;
            for (int i = 0; i < toPurge.Count; i++)
            {
                if (toPurge[i] == null) continue;
                SpriteRenderer sr = toPurge[i].GetComponent<SpriteRenderer>();
                if (sr == null) continue;
                Color c = startColors[i];
                c.a = Mathf.Lerp(startColors[i].a, 0f, t);
                sr.color = c;
            }
            yield return null;
        }

        foreach (GameObject obj in toPurge)
        { if (obj == null) continue; alreadyStored.Remove(obj); SpawnCleanupManager.MarkAsHeld(obj); Destroy(obj); }

        FinalisePurge();
    }

    private void PurgeInstant()
    {
        foreach (GameObject obj in placedIngredients.Keys.ToList())
        { if (obj == null) continue; alreadyStored.Remove(obj); SpawnCleanupManager.MarkAsHeld(obj); Destroy(obj); }
        FinalisePurge();
    }

    private void FinalisePurge()
    {
        placedIngredients.Clear();
        failedPlacementCount = 0;
        isPurging            = false;
        if (currentOutput != null)
        { StartCoroutine(FadeOutAndDestroy(currentOutput)); currentOutput = null; currentActiveRecipe = null; }
    }

    // ── Plate reset ───────────────────────────────────────────────────────────

    private void ResetPlate(bool destroyCurrentOutput)
    {
        foreach (GameObject obj in placedIngredients.Keys.ToList())
        { if (obj != null) { SpawnCleanupManager.MarkAsHeld(obj); Destroy(obj); } }
        placedIngredients.Clear();
        alreadyStored.Clear();
        failedPlacementCount = 0;
        if (currentOutput != null)
        {
            spawnedOutputIDs.Remove(currentOutput.GetInstanceID());
            SpawnCleanupManager.MarkAsHeld(currentOutput);
            if (destroyCurrentOutput) Destroy(currentOutput);
        }
        currentOutput        = null;
        currentActiveRecipe  = null;
        isTransitioning      = false;
        isPurging            = false;
    }

    // ── Fade & flash helpers ──────────────────────────────────────────────────

    private IEnumerator FadeOutAndDestroy(GameObject obj)
    {
        if (obj == null) yield break;

        int objId = obj.GetInstanceID();

        SpriteRenderer rend = obj.GetComponent<SpriteRenderer>();
        if (rend == null)
        {
            // Nothing to fade — still ensure it can be recycled.
            spawnedOutputIDs.Remove(objId);
            SpawnCleanupManager.MarkAsHeld(obj);
            Destroy(obj);
            yield break;
        }

        Pickupable2D pickup = obj.GetComponent<Pickupable2D>();
        float elapsed = 0f;
        Color startC  = rend.color;

        // If the output is already in the player's hand, don't fade/destroy it.
        if (pickup != null && pickup.IsHeld)
        {
            spawnedOutputIDs.Remove(objId);
            SpawnCleanupManager.MarkAsHeld(obj);
            rend.color = startC;
            yield break;
        }

        while (elapsed < fadeDuration)
        {
            if (obj == null) yield break;

            // If the player picks up mid-fade, stop and keep it alive.
            if (pickup != null && pickup.IsHeld)
            {
                spawnedOutputIDs.Remove(objId);
                SpawnCleanupManager.MarkAsHeld(obj);
                rend.color = startC;
                yield break;
            }

            elapsed += Time.deltaTime;
            Color c = startC;
            c.a = Mathf.Lerp(startC.a, 0f, elapsed / fadeDuration);
            rend.color = c;
            yield return null;
        }

        if (obj != null)
        {
            spawnedOutputIDs.Remove(objId);
            SpawnCleanupManager.MarkAsHeld(obj);
            Destroy(obj);
        }
    }

    private IEnumerator FlashPlate()
    {
        plateSR.color = Color.red;
        yield return new WaitForSeconds(failedPlacementFlashDuration);
        if (plateSR != null) plateSR.color = plateOriginalColor;
        flashCoroutine = null;
    }

    // ── Cleanup ───────────────────────────────────────────────────────────────

    void OnDestroy()
    {
        foreach (GameObject obj in placedIngredients.Keys.ToList())
        { if (obj != null) { SpawnCleanupManager.MarkAsHeld(obj); Destroy(obj); } }
        if (currentOutput != null)
        {
            spawnedOutputIDs.Remove(currentOutput.GetInstanceID());
            SpawnCleanupManager.MarkAsHeld(currentOutput);
            Destroy(currentOutput);
        }
    }
}
