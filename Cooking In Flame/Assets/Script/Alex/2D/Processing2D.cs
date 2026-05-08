using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

/// <summary>
/// Place on a machine with a trigger Collider2D and AudioSource.
/// Accepts a specific input prefab, runs through a sequence of stage prefabs
/// with configurable timing, and produces a final pickupable output.
///
/// SINGLE-CLICK DEPOSIT
/// ─────────────────────
/// Player holds a matching ingredient, cursor hovers the machine, LMB click
/// → ingredient is placed and processing begins. A second input is never
/// accepted while the machine is already running.
///
/// RETRIEVAL (when not locked)
/// ────────────────────────────
/// While processing, if the current stage is NOT in lockedStageIndices, the
/// player can click LMB over the machine (holding nothing) to retrieve the
/// current stage object — aborting the sequence and returning it to the hand.
///
/// LOCKED STAGES
/// ──────────────
/// Stages listed in lockedStageIndices cannot be retrieved. The player must
/// wait for that stage to complete before the next stage can be retrieved or
/// the process to finish. The final output is never locked.
/// </summary>
[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(AudioSource))]
public class Processing2D : MonoBehaviour
{
    [Header("Input")]
    [Tooltip("Only items whose name contains this prefab's name will start processing.")]
    public GameObject inputPrefab;

    [Header("Stages")]
    [Tooltip("Prefabs spawned in order. Index 0 = first stage, last = final pickupable output.")]
    public GameObject[] processingStages = new GameObject[4];
    [Tooltip("Seconds spent on each stage before advancing.")]
    public float timePerStage = 5f;

    [Header("Spawn Transform")]
    [Tooltip("World-space offset from this machine where stage objects appear.")]
    public Vector2 spawnOffset = Vector2.zero;
    [Tooltip("Z-axis rotation applied to every stage object at spawn. 0 = upright.")]
    [Range(-180f, 180f)] public float spawnRotationZ = 0f;
    [Tooltip("Uniform scale multiplier applied to every stage object. 1 = native prefab size.")]
    [Range(0.05f, 3f)]   public float stageScale = 1f;

    [Header("Placement Lock")]
    [Tooltip("Stage indices (0-based) that are pinned and cannot be retrieved by the player.\n" +
             "Unlisted stages can be clicked to abort and retrieve the current object.\n" +
             "Leave empty to allow retrieval at any stage.\n\n" +
             "The final output stage is never locked regardless of this list.")]
    public int[] lockedStageIndices = new int[0];

    [Tooltip("Distance a stage object must move from its spawn point to abort processing.\n" +
             "Set to 0 to disable displacement detection.")]
    [Min(0f)] public float interruptMoveThreshold = 0.15f;

    [Header("Audio")]
    [Tooltip("Loops from processing start until all stages complete. Stops on abort.")]
    public AudioClip processingLoopClip;
    [Range(0f, 1f)] public float processingVolume = 1f;
    [Tooltip("One-shot played only on successful completion. Never plays on abort.")]
    public AudioClip completionClip;

    // ── Private ───────────────────────────────────────────────────────────────

    private Collider2D   triggerCollider;
    private AudioSource  audioSource;
    private PlayerHand2D playerHand;

    private GameObject   currentStageObj;
    private Pickupable2D currentStagePick;
    private Rigidbody2D  currentStageRb;
    private Vector3      pinnedPosition;
    private Quaternion   pinnedRotation;
    private bool         stageLockActive;
    private int          currentStageIndex = -1;
    private bool         isProcessing;
    private Coroutine    processingCoroutine;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    void Awake()
    {
        triggerCollider = GetComponent<Collider2D>();
        audioSource     = GetComponent<AudioSource>();

        if (triggerCollider == null || !triggerCollider.isTrigger)
        { Debug.LogError($"[Processing2D] {name}: Collider2D must have 'Is Trigger' = true.", this); enabled = false; }
        if (inputPrefab == null)
        { Debug.LogError($"[Processing2D] {name}: Assign the input prefab.", this); enabled = false; }

        audioSource.playOnAwake = false;
        audioSource.loop        = false;
        audioSource.Stop();
    }

    void Start()
    {
        playerHand = FindObjectOfType<PlayerHand2D>();
        if (playerHand == null)
            Debug.LogWarning($"[Processing2D] {name}: No PlayerHand2D found.", this);
    }

    // ── Update — single LMB handler ───────────────────────────────────────────

    void Update()
    {
        // Displacement abort check — runs regardless of click
        if (isProcessing && currentStageObj != null && interruptMoveThreshold > 0f)
        {
            if (Vector3.Distance(currentStageObj.transform.position, pinnedPosition) > interruptMoveThreshold)
                AbortProcessing();
        }

        if (Mouse.current == null || playerHand == null) return;
        if (!Mouse.current.leftButton.wasPressedThisFrame) return;
        if (!triggerCollider.OverlapPoint(playerHand.WorldPosition)) return;

        Pickupable2D held = playerHand.GetHeldItem();

        // ── Path A: player holds nothing — retrieve stage if not locked ────────
        if (held == null)
        {
            if (!isProcessing || currentStageObj == null)    return;
            if (stageLockActive)                              return;  // stage is locked — cannot retrieve
            if (playerHand.IsHoldingItem)                    return;  // safety: already holding something

            RetrieveCurrentStage();
            playerHand.SuppressDropThisFrame();
            return;
        }

        // ── Path B: player holds ingredient — deposit and begin processing ─────
        if (isProcessing)                                            return;  // already running
        if (!held.IsHeld)                                            return;
        if (!held.gameObject.name.Contains(inputPrefab.name))       return;

        // Validate allowedDropTags — the machine's tag must be in the ingredient's list
        if (!playerHand.CanDropOnTag(gameObject.tag))
        {
            Debug.Log($"[Processing2D] '{held.name}' does not allow dropping on " +
                      $"tag '{gameObject.tag}'. Add it to Pickupable2D.allowedDropTags.");
            return;
        }

        playerHand.DropHeldItem();
        BeginProcessing(held.gameObject);
    }

    void LateUpdate()
    {
        if (!stageLockActive || currentStageObj == null) return;
        currentStageObj.transform.SetPositionAndRotation(pinnedPosition, pinnedRotation);
    }

    // ── Retrieval ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the current stage object to the player's hand and aborts processing.
    /// Only called when stageLockActive is false (stage is not in lockedStageIndices).
    /// </summary>
    private void RetrieveCurrentStage()
    {
        if (processingCoroutine != null) { StopCoroutine(processingCoroutine); processingCoroutine = null; }

        ReleaseLock();
        StopAudio();
        isProcessing = false;

        // Hand the stage object to the player
        if (currentStagePick != null)
        {
            _ = playerHand.ForcePickUp(currentStagePick);
        }

        // Clear machine state — the object is now in the player's hand
        currentStageObj   = null;
        currentStagePick  = null;
        currentStageRb    = null;
        currentStageIndex = -1;

        Debug.Log($"[Processing2D] {name}: Stage retrieved by player — processing aborted.");
    }

    // ── Processing ────────────────────────────────────────────────────────────

    private void BeginProcessing(GameObject inputObj)
    {
        isProcessing = true;
        SpawnCleanupManager.MarkAsHeld(inputObj);
        Destroy(inputObj);
        currentStageIndex = 0;
        SpawnStage();
        StartAudio();
        processingCoroutine = StartCoroutine(ProcessingSequence());
    }

    private IEnumerator ProcessingSequence()
    {
        while (currentStageIndex < processingStages.Length - 1)
        {
            yield return new WaitForSeconds(timePerStage);
            if (currentStageObj == null) { AbortProcessing(); yield break; }
            currentStageIndex++;
            SpawnStage();
        }

        ReleaseLock();
        StopAudio();
        if (completionClip != null) audioSource.PlayOneShot(completionClip, processingVolume);
        isProcessing        = false;
        processingCoroutine = null;
    }

    private void AbortProcessing()
    {
        if (processingCoroutine != null) { StopCoroutine(processingCoroutine); processingCoroutine = null; }
        ReleaseLock();
        StopAudio();
        isProcessing      = false;
        currentStageIndex = -1;
    }

    private void SpawnStage()
    {
        if (currentStageObj != null)
        {
            ReleaseLock();
            SpawnCleanupManager.MarkAsHeld(currentStageObj);
            Destroy(currentStageObj);
            currentStageObj  = null;
            currentStagePick = null;
            currentStageRb   = null;
        }

        if (currentStageIndex < 0 || currentStageIndex >= processingStages.Length) return;
        if (processingStages[currentStageIndex] == null) return;

        currentStageObj = Instantiate(
            processingStages[currentStageIndex],
            (Vector2)transform.position + spawnOffset,
            Quaternion.Euler(0f, 0f, spawnRotationZ));

        currentStageObj.transform.localScale *= stageScale;
        pinnedPosition   = currentStageObj.transform.position;
        pinnedRotation   = currentStageObj.transform.rotation;
        currentStagePick = currentStageObj.GetComponent<Pickupable2D>();
        currentStageRb   = currentStageObj.GetComponent<Rigidbody2D>();

        bool isFinal    = currentStageIndex == processingStages.Length - 1;
        bool shouldLock = !isFinal && IsStageIndexLocked(currentStageIndex);
        if (shouldLock) ApplyLock();
        else            stageLockActive = false;

        SpawnCleanupManager.RegisterSpawnedObject(currentStageObj);
    }

    private bool IsStageIndexLocked(int index)
    {
        if (lockedStageIndices == null) return false;
        foreach (int locked in lockedStageIndices)
            if (locked == index) return true;
        return false;
    }

    private void ApplyLock()
    {
        stageLockActive = true;
        if (currentStagePick != null) currentStagePick.SetProcessingLock(true);
        if (currentStageRb   != null) currentStageRb.bodyType = RigidbodyType2D.Static;
    }

    private void ReleaseLock()
    {
        stageLockActive = false;
        if (currentStagePick != null) currentStagePick.SetProcessingLock(false);
        if (currentStageRb   != null) currentStageRb.bodyType = RigidbodyType2D.Dynamic;
    }

    private void StartAudio()
    {
        if (processingLoopClip == null) return;
        audioSource.clip   = processingLoopClip;
        audioSource.loop   = true;
        audioSource.volume = processingVolume;
        audioSource.Play();
    }

    private void StopAudio()
    {
        audioSource.loop = false;
        audioSource.Stop();
    }

    void OnDestroy()
    {
        if (processingCoroutine != null) StopCoroutine(processingCoroutine);
        StopAudio();
        if (currentStageObj != null)
        {
            ReleaseLock();
            SpawnCleanupManager.MarkAsHeld(currentStageObj);
            Destroy(currentStageObj);
        }
    }
}