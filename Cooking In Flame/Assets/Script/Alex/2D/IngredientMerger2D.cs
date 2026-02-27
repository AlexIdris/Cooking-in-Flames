using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System.Collections;

[RequireComponent(typeof(Collider2D))]
public class IngredientMerger2D : MonoBehaviour
{
    [Header("Merger Settings")]
    public List<Recipe> recipes = new List<Recipe>();

    [Header("Spawn Position")]
    public Vector2 spawnOffset = Vector2.zero;

    [Header("Visual Transitions")]
    [Range(0.1f, 2f)]
    public float fadeDuration = 0.5f; // Time for fade-in/out

    [Header("Input Delay")]
    [Range(0.1f, 5f)]
    public float inputDelay = 0.1f; // Minimum delay between accepted inputs

    [Header("Tracking")]
    // All instances that have ever entered this collider
    public List<GameObject> allEnteredPrefabs = new List<GameObject>();

    // Only the current/previous output's prefab, used as the "old input"
    public GameObject activeInputPrefab;

    [System.Serializable]
    public class Recipe
    {
        public string recipeName = "Recipe";
        public List<GameObject> requiredInputs = new List<GameObject>(); // prefab assets
        public GameObject outputPrefab;
        public string requiredTag = "";
    }

    private Collider2D myCollider;
    private HashSet<GameObject> currentItemsInside = new HashSet<GameObject>();

    private GameObject currentOutputInstance;
    private Recipe currentActiveRecipe;

    // Prevent multiple ReplaceOutput coroutines from overlapping
    private bool isReplacingOutput = false;

    // Time when the last input was accepted
    private float lastInputTime = -Mathf.Infinity;

    void Awake()
    {
        myCollider = GetComponent<Collider2D>();
        if (myCollider == null || !myCollider.isTrigger)
        {
            Debug.LogError($"{name}: Collider2D with Is Trigger = true required!");
            enabled = false;
        }
    }

    void Update()
    {
        // If the output has been taken/destroyed externally,
        // reset the chain (no active input prefab anymore).
        if (currentOutputInstance == null && currentActiveRecipe != null && !isReplacingOutput)
        {
            activeInputPrefab = null;
            currentActiveRecipe = null;
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other == null) return;

        // Respect delay between accepted inputs
        if (Time.time - lastInputTime < inputDelay)
            return;

        lastInputTime = Time.time;

        GameObject obj = other.gameObject;

        currentItemsInside.Add(obj);

        if (!allEnteredPrefabs.Contains(obj))
            allEnteredPrefabs.Add(obj);

        ReEvaluateAndUpdateOutput();
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (other == null) return;

        GameObject obj = other.gameObject;
        currentItemsInside.Remove(obj);

        // NEW: if the current output is dragged out of the collider,
        // wipe the active input prefab and reset the current recipe.
        if (obj == currentOutputInstance)
        {
            currentOutputInstance = null;
            activeInputPrefab = null;
            currentActiveRecipe = null;
        }

        ReEvaluateAndUpdateOutput();
    }

    private void ReEvaluateAndUpdateOutput()
    {
        if (isReplacingOutput) return; // don't start a new replace while one is running

        Recipe bestMatch = null;
        int bestMatchCount = -1;

        foreach (var recipe in recipes)
        {
            if (recipe.requiredInputs == null ||
                recipe.requiredInputs.Count == 0 ||
                recipe.outputPrefab == null)
                continue;

            int matched = CountMatchingInputsByPrefabIncludingOutput(recipe);

            // Require a full match
            if (matched == recipe.requiredInputs.Count && matched > bestMatchCount)
            {
                // FIRST OUTPUT:
                if (currentActiveRecipe == null || currentOutputInstance == null || activeInputPrefab == null)
                {
                    bestMatch = recipe;
                    bestMatchCount = matched;
                    continue;
                }

                // CHAINING / UPGRADE:
                GameObject previousOutputPrefab = activeInputPrefab;

                bool usesPreviousOutputAsInput =
                    previousOutputPrefab != null &&
                    recipe.requiredInputs.Contains(previousOutputPrefab);

                bool hasAdditionalPrefab =
                    recipe.requiredInputs.Any(p => p != null && p != previousOutputPrefab);

                if (usesPreviousOutputAsInput && hasAdditionalPrefab)
                {
                    bestMatch = recipe;
                    bestMatchCount = matched;
                }
            }
        }

        // Only change the output if a new/better recipe is found
        if (bestMatch != null && bestMatch != currentActiveRecipe)
        {
            StartCoroutine(ReplaceOutputWithNew(bestMatch));
        }
    }

    // Match by prefab TYPE, considering:
    // - All current items inside the collider
    // - The current output instance as a candidate object (so it can be used as input)
    private int CountMatchingInputsByPrefabIncludingOutput(Recipe recipe)
    {
        int matched = 0;

        var candidates = new HashSet<GameObject>(currentItemsInside);
        if (currentOutputInstance != null)
            candidates.Add(currentOutputInstance);

        foreach (var requiredPrefab in recipe.requiredInputs)
        {
            if (requiredPrefab == null)
                continue;

            bool found = candidates.Any(item =>
            {
                if (item == null) return false;

                if (item.name.Contains(requiredPrefab.name))
                {
                    if (string.IsNullOrEmpty(recipe.requiredTag) || item.CompareTag(recipe.requiredTag))
                        return true;
                }
                return false;
            });

            if (found)
                matched++;
        }

        return matched;
    }

    private IEnumerator ReplaceOutputWithNew(Recipe newRecipe)
    {
        if (isReplacingOutput) yield break;
        isReplacingOutput = true;

        currentActiveRecipe = newRecipe;
        activeInputPrefab = newRecipe.outputPrefab;

        // Step 1: Spawn NEW output first
        Vector2 spawnPos = (Vector2)transform.position + spawnOffset;
        GameObject newOutput = Instantiate(newRecipe.outputPrefab, spawnPos, Quaternion.identity);

        // Fade in new output
        SpriteRenderer newRend = newOutput.GetComponent<SpriteRenderer>();
        if (newRend != null)
        {
            Color startC = newRend.color;
            startC.a = 0f;
            newRend.color = startC;

            float elapsed = 0f;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / fadeDuration;
                Color c = newRend.color;
                c.a = Mathf.Lerp(0f, 1f, t);
                newRend.color = c;
                yield return null;
            }
        }

        // Step 2: Fade out and destroy OLD output (after new one is visible)
        if (currentOutputInstance != null)
        {
            yield return StartCoroutine(FadeOutAndDestroy(currentOutputInstance));
        }

        // Step 3: Track the new output as the only active one
        currentOutputInstance = newOutput;

        Debug.Log(
            $"Merger {name}: Output -> {newRecipe.recipeName} / {newRecipe.outputPrefab.name}. " +
            $"Active input prefab (old output) is now: {(activeInputPrefab != null ? activeInputPrefab.name : "null")}"
        );

        isReplacingOutput = false;
    }

    private IEnumerator FadeOutAndDestroy(GameObject obj)
    {
        if (obj == null)
        {
            isReplacingOutput = false;
            yield break;
        }

        SpriteRenderer rend = obj.GetComponent<SpriteRenderer>();
        if (rend == null)
        {
            Destroy(obj);
            isReplacingOutput = false;
            yield break;
        }

        float elapsed = 0f;
        Color startC = rend.color;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / fadeDuration;
            Color c = startC;
            c.a = Mathf.Lerp(startC.a, 0f, t);
            rend.color = c;
            yield return null;
        }

        Destroy(obj);
    }
}