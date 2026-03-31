using UnityEngine;
using System.Collections;

/// <summary>
/// Place on a machine with a trigger Collider2D and AudioSource.
/// Accepts a specific input prefab, runs through a sequence of stage prefabs
/// with configurable timing, and produces a final pickupable output.
/// Moving the active stage object aborts the sequence.
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
    [Tooltip("Pins each intermediate stage to its spawn pose and blocks player pickup.\n" +
             "Released automatically when processing completes.")]
    public bool lockPlacementDuringProcessing = true;
    [Tooltip("Distance a stage object must move from its spawn point to abort processing.\n" +
             "Set to 0 to disable displacement detection.")]
    [Min(0f)] public float interruptMoveThreshold = 0.15f;

    [Header("Audio")]
    [Tooltip("Loops from processing start until all stages complete. Stops on abort.")]
    public AudioClip processingLoopClip;
    [Range(0f, 1f)] public float processingVolume = 1f;
    [Tooltip("One-shot played only on successful completion. Never plays on abort.")]
    public AudioClip completionClip;

    private Collider2D   triggerCollider;
    private AudioSource  audioSource;
    private GameObject   currentStageObj;
    private Pickupable2D currentStagePick;
    private Rigidbody2D  currentStageRb;
    private Vector3      pinnedPosition;
    private Quaternion   pinnedRotation;
    private bool         stageLockActive;
    private int          currentStageIndex = -1;
    private bool         isProcessing;
    private Coroutine    processingCoroutine;

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

    void Update()
    {
        if (!isProcessing || currentStageObj == null || interruptMoveThreshold <= 0f) return;
        if (Vector3.Distance(currentStageObj.transform.position, pinnedPosition) > interruptMoveThreshold)
            AbortProcessing();
    }

    void LateUpdate()
    {
        if (!stageLockActive || currentStageObj == null) return;
        currentStageObj.transform.SetPositionAndRotation(pinnedPosition, pinnedRotation);
    }

    void OnTriggerEnter2D(Collider2D other) { if (!isProcessing && other != null) TryStart(other.gameObject); }
    void OnTriggerStay2D(Collider2D other)  { if (!isProcessing && other != null) TryStart(other.gameObject); }

    private void TryStart(GameObject obj)
    {
        if (!obj.name.Contains(inputPrefab.name)) return;
        Pickupable2D p = obj.GetComponent<Pickupable2D>();
        if (p != null && p.IsHeld) return;
        BeginProcessing(obj);
    }

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

        bool isFinal = currentStageIndex == processingStages.Length - 1;
        if (lockPlacementDuringProcessing && !isFinal) ApplyLock();
        else stageLockActive = false;

        SpawnCleanupManager.RegisterSpawnedObject(currentStageObj);
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