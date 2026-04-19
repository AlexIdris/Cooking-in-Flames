using UnityEngine;
using TMPro;
using System.Collections;
using System.Collections.Generic;

public class DayNightCycle5Min : MonoBehaviour
{
    [Header("Cycle Timing")]
    [Tooltip("Real seconds for the active day (8 AM to 8 PM).")]
    public float realSecondsForDay = 300f;

    [Header("Background Stages")]
    [Tooltip("Define exact start/end hours (8-20). End of one = start of next.")]
    public List<BackgroundStage> stages = new List<BackgroundStage>();

    [Header("Final Stage (after 20:00)")]
    public Sprite finalEndOfDayBackground;

    [Header("Scene References")]
    public SpriteRenderer backgroundRenderer;
    [Tooltip("TextMeshPro child of the alarm clock showing time.")]
    public TextMeshPro clockText;

    [Header("Fade Durations")]
    [Range(0.1f, 3f)]
    [Tooltip("Fade time when changing between main day stages.")]
    public float stageFadeDuration = 1.2f;
    [Range(0.5f, 6f)]
    [Tooltip("Fade time when entering the final end-of-day stage.")]
    public float finalFadeDuration = 2.5f;

    [Header("Audio")]
    public AudioSource dayAmbientSource;
    public AudioClip   endOfDaySound;

    [Header("End-of-Day Customer Dismissal")]
    [Tooltip("Seconds before the final stage at which customers begin to be dismissed.\n" +
             "Each customer shows a sad face then walks off, staggered by dismissalStagger.\n" +
             "Set to 0 to disable the pre-warning entirely (customers are dismissed\n" +
             "instantly when the final stage is entered).")]
    [Min(0f)]
    public float customerWarningLeadTime = 2f;

    [Tooltip("Seconds between each successive customer dismissal.\n" +
             "Customers are dismissed back-to-front so the last in queue leaves first\n" +
             "and the front customer (being served) leaves last.\n\n" +
             "Example with 3 customers and stagger = 1:\n" +
             "  t+0s  back customer   → sad + LeaveAndDie\n" +
             "  t+1s  middle customer → sad + LeaveAndDie\n" +
             "  t+2s  front customer  → sad + LeaveAndDie")]
    [Min(0f)]
    public float dismissalStagger = 1f;

    [System.Serializable]
    public class BackgroundStage
    {
        public string stageName      = "Morning";
        public Sprite backgroundSprite;
        [Range(8f, 20f)] public float startHour = 8f;
        [Range(8f, 20f)] public float endHour   = 14f;
    }

    private float           currentTime          = 0f;
    private bool            isFinalStage         = false;
    private bool            customerWarningFired = false;
    private Sprite          currentBackgroundSprite;
    private CustomerSpawner2 spawner;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    void Start()
    {
        if (backgroundRenderer == null) Debug.LogError("Background Renderer not assigned!", this);
        if (clockText          == null) Debug.LogError("Clock TextMeshPro not assigned!",   this);
        if (stages.Count       == 0)   Debug.LogError("No background stages defined!",      this);

        stages.Sort((a, b) => a.startHour.CompareTo(b.startHour));

        currentBackgroundSprite = stages.Count > 0 ? stages[0].backgroundSprite : null;
        if (backgroundRenderer != null && currentBackgroundSprite != null)
            backgroundRenderer.sprite = currentBackgroundSprite;

        UpdateBackgroundAndClock();

        if (dayAmbientSource != null && dayAmbientSource.clip != null)
        {
            dayAmbientSource.loop = true;
            dayAmbientSource.Play();
        }

        // Find the spawner once — used to stop/resume customer spawning
        spawner = FindObjectOfType<CustomerSpawner2>();
        if (spawner == null)
            Debug.LogWarning("[DayNightCycle5Min] No CustomerSpawner2 found in scene.", this);
    }

    void Update()
    {
        if (isFinalStage) return;

        currentTime += Time.deltaTime;

        // Customer warning fires once, customerWarningLeadTime seconds before the end
        if (!customerWarningFired && customerWarningLeadTime > 0f)
        {
            if (realSecondsForDay - currentTime <= customerWarningLeadTime)
            {
                customerWarningFired = true;

                // Stop spawner immediately so no new customers arrive during dismissal
                spawner?.StopSpawning();

                StartCoroutine(DismissAllCustomersStaggered());
            }
        }

        if (currentTime >= realSecondsForDay)
        {
            EnterFinalStage();
            return;
        }

        UpdateBackgroundAndClock();
    }

    // ── Customer dismissal ────────────────────────────────────────────────────

    /// <summary>
    /// Stops the spawner, then dismisses every active customer back-to-front
    /// with a stagger of dismissalStagger seconds between each one.
    /// Back-to-front order means the customer closest to the counter (index 0)
    /// is the last to leave, giving the illusion that they are finishing their
    /// transaction while the queue behind them clears out first.
    /// </summary>
    private IEnumerator DismissAllCustomersStaggered()
    {
        // Build a snapshot of the current queue back-to-front
        // (spawner.customers is ordered front=0, back=last)
        List<CustomerMover2> toDissmiss = new List<CustomerMover2>();

        if (spawner != null)
        {
            for (int i = spawner.customers.Count - 1; i >= 0; i--)
            {
                CustomerMover2 c = spawner.customers[i];
                if (c != null && !c.IsLeaving)
                    toDissmiss.Add(c);
            }
        }
        else
        {
            // No spawner reference — fall back to scene-wide search
            CustomerMover2[] all = FindObjectsOfType<CustomerMover2>();
            for (int i = all.Length - 1; i >= 0; i--)
                if (all[i] != null && !all[i].IsLeaving)
                    toDissmiss.Add(all[i]);
        }

        int count = toDissmiss.Count;
        for (int i = 0; i < count; i++)
        {
            CustomerMover2 customer = toDissmiss[i];
            if (customer == null) { yield return null; continue; }

            customer.SetFace(2);    // sad face
            customer.LeaveAndDie(); // begin walking off screen

            if (i < count - 1 && dismissalStagger > 0f)
                yield return new WaitForSeconds(dismissalStagger);
        }

        Debug.Log($"[DayNightCycle5Min] End-of-day dismissal complete — " +
                  $"{count} customer(s) dismissed (stagger: {dismissalStagger}s).");
    }

    // ── Clock & background ────────────────────────────────────────────────────

    private void UpdateBackgroundAndClock()
    {
        float dayProgress = currentTime / realSecondsForDay;
        float inGameHours = 8f + (dayProgress * 12f);

        BackgroundStage activeStage = null;
        foreach (BackgroundStage stage in stages)
        {
            if (inGameHours >= stage.startHour && inGameHours < stage.endHour)
            { activeStage = stage; break; }
        }
        if (activeStage == null && stages.Count > 0)
            activeStage = stages[stages.Count - 1];

        if (activeStage != null && activeStage.backgroundSprite != currentBackgroundSprite)
        {
            StartCoroutine(FadeBackground(activeStage.backgroundSprite, stageFadeDuration));
            currentBackgroundSprite = activeStage.backgroundSprite;
        }

        int    hours        = Mathf.FloorToInt(inGameHours) % 24;
        int    minutes      = Mathf.FloorToInt((inGameHours - Mathf.Floor(inGameHours)) * 60f);
        string ampm         = hours < 12 ? "AM" : "PM";
        int    displayHours = hours % 12;
        if (displayHours == 0) displayHours = 12;

        if (clockText != null)
            clockText.text = $"{displayHours:00}:{minutes:00} {ampm}";
    }

    // ── Final stage ───────────────────────────────────────────────────────────

    private void EnterFinalStage()
    {
        isFinalStage = true;

        // Safety: stop spawner and dismiss any customers that slipped through
        spawner?.StopSpawning();

        if (!customerWarningFired)
            StartCoroutine(DismissAllCustomersStaggered());

        if (finalEndOfDayBackground != null && backgroundRenderer != null)
            StartCoroutine(FadeBackground(finalEndOfDayBackground, finalFadeDuration));

        if (clockText != null)
            clockText.text = "08:00 PM";

        if (endOfDaySound != null)
            AudioSource.PlayClipAtPoint(endOfDaySound, Camera.main.transform.position);

        if (dayAmbientSource != null && dayAmbientSource.isPlaying)
            StartCoroutine(FadeOutAudio(dayAmbientSource, finalFadeDuration));

        Debug.Log("[DayNightCycle5Min] Day complete (8 PM) — final background active.");
    }

    // ── Coroutines ────────────────────────────────────────────────────────────

    private IEnumerator FadeBackground(Sprite newSprite, float duration)
    {
        if (backgroundRenderer == null) yield break;

        Color startColor = backgroundRenderer.color;
        float elapsed    = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            Color c  = startColor;
            c.a      = Mathf.Lerp(startColor.a, 0f, elapsed / duration);
            backgroundRenderer.color = c;
            yield return null;
        }

        backgroundRenderer.sprite = newSprite;
        backgroundRenderer.color  = new Color(1f, 1f, 1f, 0f);
        elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            backgroundRenderer.color = new Color(1f, 1f, 1f, Mathf.Lerp(0f, 1f, elapsed / duration));
            yield return null;
        }
    }

    private IEnumerator FadeOutAudio(AudioSource source, float duration)
    {
        float startVol = source.volume;
        float elapsed  = 0f;
        while (elapsed < duration)
        {
            elapsed      += Time.deltaTime;
            source.volume = Mathf.Lerp(startVol, 0f, elapsed / duration);
            yield return null;
        }
        source.Stop();
    }

    // ── Debug tools ───────────────────────────────────────────────────────────

    [ContextMenu("Jump to 8 PM")]
    void JumpToEndDebug()
    {
        currentTime = realSecondsForDay - 0.1f;
        UpdateBackgroundAndClock();
    }

    [ContextMenu("Reset Cycle")]
    void ResetCycle()
    {
        currentTime          = 0f;
        isFinalStage         = false;
        customerWarningFired = false;
        spawner?.ResumeSpawning();
        if (dayAmbientSource != null && !dayAmbientSource.isPlaying)
            dayAmbientSource.Play();
        UpdateBackgroundAndClock();
    }
}