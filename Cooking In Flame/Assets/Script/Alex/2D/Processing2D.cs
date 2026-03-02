using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Collider2D))]
public class Processing2D : MonoBehaviour
{
    [Header("Input Prefab (Only this one triggers processing)")]
    [Tooltip("ONLY objects matching this prefab will start processing")]
    public GameObject inputPrefab;  // ← Specific assigned prefab

    [Header("Processing Stages")]
    [Tooltip("List of prefabs to spawn in order. Last one stays forever.")]
    public GameObject[] processingStages = new GameObject[4];  // e.g., 0=raw, 1=half, 2=almost, 3=done

    [Header("Timing")]
    public float timePerStage = 5f;  // Seconds between stages

    [Header("Spawn Settings")]
    [Tooltip("Optional explicit spawn point. If null, uses this object's position + offset.")]
    public Transform spawnPoint;
    public Vector2 spawnOffset = Vector2.zero;

    private Collider2D triggerCollider;
    private GameObject currentProcessedObject;
    private int currentStageIndex = -1;
    private bool isProcessing = false;
    private Coroutine processingCoroutine;

    void Awake()
    {
        triggerCollider = GetComponent<Collider2D>();
        if (triggerCollider == null || !triggerCollider.isTrigger)
        {
            Debug.LogError($"{name}: Needs Collider2D with Is Trigger = true", this);
            enabled = false;
        }

        if (inputPrefab == null)
        {
            Debug.LogError($"{name}: Assign the specific input prefab!", this);
            enabled = false;
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (isProcessing || other == null) return;

        GameObject enteredObj = other.gameObject;

        // Only work on the SPECIFIC assigned prefab
        if (enteredObj.name.Contains(inputPrefab.name))
        {
            StartProcessing(enteredObj);
        }
    }

    private void StartProcessing(GameObject inputObj)
    {
        isProcessing = true;

        // Destroy the input immediately
        Destroy(inputObj);

        // Start from stage 0 (first in list)
        currentStageIndex = 0;
        SpawnCurrentStage();

        // Begin stage timer sequence
        processingCoroutine = StartCoroutine(ProcessingSequence());
    }

    private IEnumerator ProcessingSequence()
    {
        while (currentStageIndex < processingStages.Length - 1)  // Stop at last prefab
        {
            yield return new WaitForSeconds(timePerStage);
            currentStageIndex++;
            SpawnCurrentStage();
        }

        // Processing complete – last stage stays forever
        isProcessing = false;
        processingCoroutine = null;
        Debug.Log($"{name}: Processing complete – final stage {processingStages.Length - 1} reached.");
    }

    private void SpawnCurrentStage()
    {
        // Destroy previous stage
        if (currentProcessedObject != null)
        {
            Destroy(currentProcessedObject);
        }

        // Spawn current stage
        if (currentStageIndex >= 0 && currentStageIndex < processingStages.Length)
        {
            Vector2 spawnPos;

            if (spawnPoint != null)
            {
                spawnPos = spawnPoint.position;
            }
            else
            {
                spawnPos = (Vector2)transform.position + spawnOffset;
            }

            currentProcessedObject = Instantiate(processingStages[currentStageIndex], spawnPos, Quaternion.identity);
            Debug.Log($"{name}: Stage {currentStageIndex + 1}/{processingStages.Length}");
        }
    }

    /// <summary>
    /// Reset processing and stop producing more prefabs.
    /// Called when the spawned prefab is removed.
    /// </summary>
    private void ResetProcessing()
    {
        if (processingCoroutine != null)
        {
            StopCoroutine(processingCoroutine);
            processingCoroutine = null;
        }

        if (currentProcessedObject != null)
        {
            Destroy(currentProcessedObject);
            currentProcessedObject = null;
        }

        currentStageIndex = -1;
        isProcessing = false;

        Debug.Log($"{name}: Processing reset because spawned prefab was removed.");
    }

    void Update()
    {
        // Unity treats destroyed objects as == null,
        // so this will be true if the current stage prefab is removed externally.
        if (isProcessing && currentProcessedObject == null)
        {
            ResetProcessing();
        }
    }

    // Draw waypoint indicator in the Scene view
    void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;

        Vector3 pos;
        if (spawnPoint != null)
        {
            pos = spawnPoint.position;
        }
        else
        {
            pos = transform.position + (Vector3)spawnOffset;
        }

        Gizmos.DrawWireSphere(pos, 0.3f);
        Gizmos.DrawLine(transform.position, pos);
    }
}