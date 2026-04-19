using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Place on a plate or surface with a trigger Collider2D.
///
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
/// WRONG INGREDIENT PURGE
/// ───────────────────────
/// Every individual ingredient placement that does NOT immediately complete a correct
/// recipe increments wrongIngredientCount. When that counter reaches maxWrongIngredients
/// (default 6, customizable), ALL placed ingredients are destroyed and the plate resets.
/// The counter also resets to 0 on any successful recipe match.
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

    [Header("Wrong Ingredient Purge")]
    [Tooltip("Total number of individual ingredient placements that produce no correct match\n" +
             "before ALL placed ingredients are destroyed and the plate fully resets.\n\n" +
             "Every single drop that does not immediately complete a valid recipe counts\n" +
             "as one strike — including partial drops toward a multi-ingredient recipe.\n\n" +
             "Counter resets to 0 on a successful recipe match or after a purge.\n" +
             "Set to 0 to disable automatic purging entirely.")]
    [Min(0)] public int maxWrongIngredients = 6;

    [Tooltip("Flash the plate sprite red on each wrong ingredient placement.")]
    public bool flashOnWrongIngredient = true;
    [Range(0.05f, 1f)] public float wrongIngredientFlashDuration = 0.2f;

    [Tooltip("Fade ingredients out over fadeDuration before destroying them on purge.\n" +
             "False = instant destroy.")]
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

    private Collider2D     myCollider;
    private SpriteRenderer plateSR;
    private Color          plateOriginalColor;
    private PlayerHand2D   playerHand;

    private readonly Dictionary<GameObject, Vector3> placedIngredients = new Dictionary<GameObject, Vector3>();
    private readonly HashSet<GameObject>             alreadyStored     = new HashSet<GameObject>();

    private GameObject currentOutput;
    private Recipe     currentActiveRecipe;
    private bool       isTransitioning;
    private bool       isPurging;

    // Counts every individual ingredient placement that did not immediately
    // complete a correct recipe. Resets on success or after a purge.
    private int       wrongIngredientCount;
    private Coroutine flashCoroutine;

    // Scene-wide registry of every output instance currently alive.
    // Static so ALL plates share the same set — an output spawned on plate A
    // is blocked as an ingredient on plate B without any cross-plate references.
    private static readonly HashSet<int> spawnedOutputIDs = new HashSet<int>();

    private static bool IsSpawnedOutput(GameObject obj) =>
        obj != null && IsSpawnedOutputOrChild(obj);

    private static bool IsSpawnedOutputOrChild(GameObject obj)
    {
        Transform t = obj.transform;
        while (t != null)
        {
            if (spawnedOutputIDs.Contains(t.gameObject.GetInstanceID())) return true;
            t = t.parent;
        }
        return false;
    }

    // ── Public accessors for IngredientAnomalyCleanup ────────────────────────

    /// <summary>
    /// Returns a snapshot of all GameObjects currently placed on this plate as
    /// ingredients. Used by IngredientAnomalyCleanup to identify legitimate inputs.
    /// </summary>
    public IEnumerable<GameObject> GetPlacedIngredients() =>
        new List<GameObject>(placedIngredients.Keys);

    /// <summary>
    /// Static wrapper around IsSpawnedOutput so IngredientAnomalyCleanup can
    /// query the scene-wide output registry without a plate instance reference.
    /// </summary>
    public static bool IsSpawnedOutputPublic(GameObject obj) => IsSpawnedOutput(obj);

    // ── Lifecycle ─────────────────────────────────────────────────────────────

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

        // ── Path A: player holds nothing — pick up output or remove ingredient ─
        if (held == null)
        {
            // A1 — pick up the finished output sitting on the plate.
            // Requires genuine hover contact: the cursor must be over the output's
            // own Pickupable2D collider (detected by PlayerHand2D.UpdateHover) so
            // the player must physically touch the output sprite to grab it.
            if (currentOutput != null)
            {
                Pickupable2D outPickup  = currentOutput.GetComponent<Pickupable2D>();
                Pickupable2D hovered    = playerHand.GetHoveredItem();

                // Only proceed when PlayerHand2D is hovering THIS output specifically
                if (outPickup != null && outPickup.CanBePickedUp() && hovered == outPickup)
                {
                    int inputsUsed = currentActiveRecipe != null
                        ? currentActiveRecipe.requiredInputs.Count : 0;

                    if (minInputsToPickUpOutput > 0 && inputsUsed < minInputsToPickUpOutput)
                    {
                        // Recipe did not use enough ingredients — block pickup silently.
                        playerHand.SuppressDropThisFrame();
                        return;
                    }

                    // Hand the output to PlayerHand2D via ForcePickUp, which calls
                    // OnPickup (disabling the collider) and suppresses PlayerHand2D's
                    // own LMB branch — no double-processing.
                    playerHand.ForcePickUp(outPickup);
                    SpawnCleanupManager.MarkAsHeld(outPickup.gameObject);
                    spawnedOutputIDs.Remove(outPickup.gameObject.GetInstanceID());
                    currentOutput       = null;
                    currentActiveRecipe = null;
                    DestroyAllPlacedIngredients();
                    return;
                }
            }

            // A2 — pick up a placed ingredient to remove or swap it
            if (placedIngredients.Count > 0)
            {
                Vector2 cursorPos = playerHand.transform.position;
                foreach (GameObject placed in placedIngredients.Keys.ToList())
                {
                    if (placed == null) continue;
                    if (!string.IsNullOrEmpty(ingredientInputTag) && !placed.CompareTag(ingredientInputTag)) continue;
                    Pickupable2D pick = placed.GetComponent<Pickupable2D>();
                    if (pick == null || !pick.CanBePickedUp()) continue;
                    if (Vector2.Distance(cursorPos, placed.transform.position) > 0.5f) continue;

                    RemoveIngredient(placed);
                    playerHand.ForcePickUp(pick);
                    playerHand.SuppressDropThisFrame();
                    return;
                }
            }

            return;
        }

        // ── Path B: player holds an ingredient — drop and place it ────────────
        if (!held.IsHeld) return;
        if (!myCollider.OverlapPoint(held.transform.position)) return;
        if (!string.IsNullOrEmpty(ingredientInputTag) && !held.CompareTag(ingredientInputTag)) return;
        if (placedIngredients.ContainsKey(held.gameObject)) return;
        if (IsSpawnedOutput(held.gameObject)) return;

        playerHand.DropHeldItem();
        PlaceIngredient(held.gameObject);
    }

    // ── Trigger callbacks (fallback for physics-driven drops) ─────────────────

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other == null || isPurging) return;
        GameObject obj = other.gameObject;

        if (IsOutputBeingPickedUp(obj)) { ResetPlate(false); return; }

        Pickupable2D pickup = obj.GetComponent<Pickupable2D>();
        if (pickup == null || pickup.IsHeld || placedIngredients.ContainsKey(obj)) return;
        if (IsSpawnedOutput(obj)) return;
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
        bool isOutputOrChild = obj == currentOutput || obj.transform.IsChildOf(currentOutput.transform);
        if (!isOutputOrChild) return false;
        Pickupable2D p = currentOutput.GetComponent<Pickupable2D>();
        return p != null && p.IsHeld;
    }

    // ── Placement ─────────────────────────────────────────────────────────────

    private void PlaceIngredient(GameObject obj)
    {
        if (!alreadyStored.Contains(obj))
        {
            obj.transform.localScale *= storedIngredientScale;
            alreadyStored.Add(obj);
        }

        Vector3 stored = obj.transform.localScale;
        placedIngredients.Add(obj, stored);
        obj.transform.localScale = stored * ResolveDisplayScale(currentActiveRecipe);

        RepositionIngredients();

        // ── Duplicate-slot guard ───────────────────────────────────────────────
        // Each input slot must hold a unique ingredient type. If the newly placed
        // item shares a name with any already-placed item AND no recipe explicitly
        // requires two of that ingredient (i.e. the duplicate is unstructured),
        // the placement is invalid — purge all ingredients and any current output.
        if (HasUnstructuredDuplicate())
        {
            Debug.Log($"[IngredientMerger2D] {name}: Duplicate ingredient '{obj.name}' " +
                      "placed without a matching recipe slot — resetting plate.");
            if (fadeOnPurge) StartCoroutine(PurgeWithFade());
            else             PurgeInstant();
            return;
        }

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

        Vector3 anchor    = transform.position + ingredientRowOffset;
        float   slotWidth = ingredientRowWidth / count;

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

        Recipe bestMatch = null;
        int    bestCount = -1;

        foreach (Recipe recipe in recipes)
        {
            if (recipe.requiredInputs.Count == 0 || recipe.outputPrefab == null) continue;
            if (placedIngredients.Count != recipe.requiredInputs.Count) continue;
            int matched = CountMatches(recipe);
            if (matched == recipe.requiredInputs.Count && matched > bestCount)
            { bestMatch = recipe; bestCount = matched; }
        }

        if (bestMatch != null && bestMatch != currentActiveRecipe)
        {
            // Correct match — reset the wrong counter and produce the output
            wrongIngredientCount = 0;
            StartCoroutine(ShowOutput(bestMatch));
            return;
        }

        // This placement did not complete a correct recipe — count the strike
        if (maxWrongIngredients > 0)
        {
            wrongIngredientCount++;

            Debug.Log($"[IngredientMerger2D] {name}: Wrong ingredient " +
                      $"({wrongIngredientCount}/{maxWrongIngredients}).");

            if (flashOnWrongIngredient && plateSR != null)
            {
                if (flashCoroutine != null) StopCoroutine(flashCoroutine);
                flashCoroutine = StartCoroutine(FlashPlate());
            }

            if (wrongIngredientCount >= maxWrongIngredients)
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

    /// <summary>
    /// Returns true if any two ingredients currently on the plate share the same
    /// base name AND no configured recipe explicitly requires two of that ingredient.
    ///
    /// "Explicitly requires two" means the recipe's requiredInputs list contains
    /// the same prefab reference at least twice.  If every recipe that could
    /// theoretically accommodate the duplicate accounts for it in its slot list,
    /// the placement is treated as valid and this returns false.
    ///
    /// This enforces the rule: each input slot holds one unique ingredient type
    /// unless the recipe was deliberately designed to need duplicates.
    /// </summary>
    private bool HasUnstructuredDuplicate()
    {
        List<GameObject> placed = placedIngredients.Keys.Where(o => o != null).ToList();

        // Build a frequency map: ingredient base-name → count on plate
        Dictionary<string, int> nameCount = new Dictionary<string, int>();
        foreach (GameObject obj in placed)
        {
            string baseName = obj.name.Replace("(Clone)", "").Trim();
            if (!nameCount.ContainsKey(baseName)) nameCount[baseName] = 0;
            nameCount[baseName]++;
        }

        // Check every ingredient type that appears more than once
        foreach (KeyValuePair<string, int> kv in nameCount)
        {
            if (kv.Value < 2) continue;  // no duplicate for this type

            // Count how many slots any recipe allocates for this ingredient
            // A recipe "covers" the duplicate if it lists the ingredient at least
            // as many times as it appears on the plate.
            bool coveredByRecipe = false;
            foreach (Recipe recipe in recipes)
            {
                if (recipe.requiredInputs == null || recipe.requiredInputs.Count == 0) continue;

                int slotsForType = recipe.requiredInputs
                    .Where(r => r != null)
                    .Count(r => r.name.Replace("(Clone)", "").Trim() == kv.Key ||
                                kv.Key.Contains(r.name.Replace("(Clone)", "").Trim()));

                if (slotsForType >= kv.Value)
                {
                    coveredByRecipe = true;
                    break;
                }
            }

            if (!coveredByRecipe)
                return true;  // unstructured duplicate found
        }

        return false;
    }

    // ── Output ────────────────────────────────────────────────────────────────

    private IEnumerator ShowOutput(Recipe recipe)
    {
        isTransitioning = true;
        if (currentOutput != null) { yield return StartCoroutine(FadeOutAndDestroy(currentOutput)); currentOutput = null; }

        Vector3    pos    = (Vector3)((Vector2)transform.position + outputSpawnOffset);
        GameObject newOut = Instantiate(recipe.outputPrefab, pos, Quaternion.identity);
        SpawnCleanupManager.RegisterSpawnedObject(newOut);
        spawnedOutputIDs.Add(newOut.GetInstanceID());
        currentOutput       = newOut;
        currentActiveRecipe = recipe;
        ReapplyDisplayScale(recipe);

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

        if (newOut != null)
        {
            Pickupable2D outPickup = newOut.GetComponent<Pickupable2D>();
            if (outPickup == null)
                Debug.LogWarning($"[IngredientMerger2D] {name}: Output prefab " +
                    $"'{recipe.outputPrefab.name}' has no Pickupable2D — " +
                    "it cannot be picked up by the player.", this);
        }
    }

    // ── Ingredient cleanup ────────────────────────────────────────────────────

    private void DestroyAllPlacedIngredients()
    {
        foreach (GameObject obj in placedIngredients.Keys.ToList())
        { if (obj != null) { SpawnCleanupManager.MarkAsHeld(obj); Destroy(obj); } }
        placedIngredients.Clear();
        alreadyStored.Clear();
        wrongIngredientCount = 0;
    }

    // ── Purge ─────────────────────────────────────────────────────────────────

    private IEnumerator PurgeWithFade()
    {
        isPurging = true;
        List<GameObject> toPurge     = placedIngredients.Keys.Where(o => o != null).ToList();
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
        wrongIngredientCount = 0;
        isPurging            = false;
        if (currentOutput != null)
        { StartCoroutine(FadeOutAndDestroy(currentOutput)); currentOutput = null; currentActiveRecipe = null; }

        Debug.Log($"[IngredientMerger2D] {name}: Purge complete — plate reset.");
    }

    // ── Plate reset ───────────────────────────────────────────────────────────

    private void ResetPlate(bool destroyCurrentOutput)
    {
        foreach (GameObject obj in placedIngredients.Keys.ToList())
        { if (obj != null) { SpawnCleanupManager.MarkAsHeld(obj); Destroy(obj); } }
        placedIngredients.Clear();
        alreadyStored.Clear();
        wrongIngredientCount = 0;

        if (currentOutput != null)
        {
            spawnedOutputIDs.Remove(currentOutput.GetInstanceID());
            SpawnCleanupManager.MarkAsHeld(currentOutput);
            if (destroyCurrentOutput) Destroy(currentOutput);
        }

        currentOutput       = null;
        currentActiveRecipe = null;
        isTransitioning     = false;
        isPurging           = false;
    }

    // ── Fade & flash helpers ──────────────────────────────────────────────────

    private IEnumerator FadeOutAndDestroy(GameObject obj)
    {
        if (obj == null) yield break;

        int            objId  = obj.GetInstanceID();
        SpriteRenderer rend   = obj.GetComponent<SpriteRenderer>();
        Pickupable2D   pickup = obj.GetComponent<Pickupable2D>();

        if (rend == null)
        {
            spawnedOutputIDs.Remove(objId);
            SpawnCleanupManager.MarkAsHeld(obj);
            Destroy(obj);
            yield break;
        }

        // If the item is already held, don't fade or destroy it
        if (pickup != null && pickup.IsHeld)
        {
            spawnedOutputIDs.Remove(objId);
            SpawnCleanupManager.MarkAsHeld(obj);
            yield break;
        }

        float elapsed = 0f;
        Color startC  = rend.color;

        while (elapsed < fadeDuration)
        {
            if (obj == null) yield break;

            // Stop if the player picks it up mid-fade
            if (pickup != null && pickup.IsHeld)
            {
                spawnedOutputIDs.Remove(objId);
                SpawnCleanupManager.MarkAsHeld(obj);
                rend.color = startC;
                yield break;
            }

            elapsed += Time.deltaTime;
            Color c  = startC;
            c.a      = Mathf.Lerp(startC.a, 0f, elapsed / fadeDuration);
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
        yield return new WaitForSeconds(wrongIngredientFlashDuration);
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