using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Collider2D))]
public class FryingProcess2D : MonoBehaviour
{
    [Header("Frying Stages (Drag in order)")]
    [Tooltip("Stage 0 = raw/initial (what player places)\nStage 1 = half-cooked\nStage 2 = almost done\nStage 3 = final fried (STOPS HERE)")]
    public GameObject[] fryingStages = new GameObject[4];  // Exactly 4 stages

    [Header("Timing")]
    [Tooltip("Seconds between each stage replacement")]
    public float timePerStage = 5f;

    [Header("Spawn Position")]
    [Tooltip("Offset from fryer center")]
    public Vector2 spawnOffset = Vector2.zero;

    private Collider2D triggerCollider;
    private GameObject currentFryingObject;
    private int currentStageIndex = -1;
    private bool isFrying = false;

    void Awake()
    {
        triggerCollider = GetComponent<Collider2D>();
        if (triggerCollider == null || !triggerCollider.isTrigger)
        {
            Debug.LogError($"{name}: Needs Collider2D with Is Trigger = true", this);
            enabled = false;
        }

        if (fryingStages.Length != 4)
        {
            Debug.LogWarning($"{name}: Expected exactly 4 frying stages.");
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (isFrying) return;  // Already frying

        Pickupable2D pickup = other.GetComponent<Pickupable2D>();
        if (pickup == null) return;  // Only Pickupable2D ingredients

        StartFrying(other.gameObject);
    }

    private void StartFrying(GameObject initialObject)
    {
        isFrying = true;

        // Remove original input
        Destroy(initialObject);

        // Start at stage 0
        currentStageIndex = 0;
        SpawnNextStage();

        // Begin sequence
        StartCoroutine(FryingSequence());
    }

    private IEnumerator FryingSequence()
    {
        // Progress through stages 0 → 3
        while (currentStageIndex < fryingStages.Length - 1)  // Stops before exceeding 3
        {
            yield return new WaitForSeconds(timePerStage);
            currentStageIndex++;
            SpawnNextStage();
        }

        // Reached final stage (3) → STOP here forever
        isFrying = false;
        Debug.Log($"{name}: Frying complete – stopped at final stage {currentStageIndex}");
    }

    private void SpawnNextStage()
    {
        if (currentStageIndex < 0 || currentStageIndex >= fryingStages.Length)
            return;

        // Destroy old stage
        if (currentFryingObject != null)
        {
            Destroy(currentFryingObject);
        }

        // Instantiate new stage
        Vector2 spawnPos = (Vector2)transform.position + spawnOffset;
        currentFryingObject = Instantiate(fryingStages[currentStageIndex], spawnPos, Quaternion.identity);

        Debug.Log($"{name}: Stage {currentStageIndex + 1}/4: {fryingStages[currentStageIndex].name}");
    }
}