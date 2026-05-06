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
    [Tooltip("AudioSource that plays the ambient loop during the day (8 AM - 8 PM).\n" +
             "Starts playing immediately when the shop button is clicked, fading in\n" +
             "over audioCrossfadeDuration. Fades out when the day ends.")]
    public AudioSource dayAmbientSource;

    [Tooltip("AudioSource that plays the end-of-day ambient (8 PM onward).\n" +
             "Fades in when the day ends, fades out when the shop opens again.")]
    public AudioSource endOfDayAudioSource;

    [Tooltip("Seconds for the audio crossfade in both directions.\n" +
             "Both sources fade simultaneously so the transition is smooth.")]
    [Range(0.1f, 10f)]
    public float audioCrossfadeDuration = 2.5f;

    [Tooltip("Peak volume the day ambient source fades up to when the shop opens.\n" +
             "0 = silent, 1 = full.")]
    [Range(0f, 1f)]
    public float dayAmbientMaxVolume = 1f;

    [Tooltip("Peak volume the end-of-day source fades up to when the day ends.\n" +
             "0 = silent, 1 = full.")]
    [Range(0f, 1f)]
    public float endOfDayMaxVolume = 1f;

    [Header("End-of-Day Customer Dismissal")]
    [Tooltip("Seconds before the final stage at which customers begin to be dismissed.\n" +
             "Set to 0 to disable.")]
    [Min(0f)]
    public float customerWarningLeadTime = 2f;

    [Tooltip("Seconds between each successive customer dismissal (back-to-front).")]
    [Min(0f)]
    public float dismissalStagger = 1f;

    [System.Serializable]
    public class BackgroundStage
    {
        public string stageName  = "Morning";
        public Sprite backgroundSprite;
        [Range(8f, 20f)] public float startHour = 8f;
        [Range(8f, 20f)] public float endHour   = 14f;
    }

    private float            currentTime          = 0f;
    private bool             isFinalStage         = false;
    private bool             customerWarningFired = false;
    private bool             dayRunning           = false;
    private Sprite           currentBackgroundSprite;
    private CustomerSpawner2     spawner;
    private ShopToggle           shopToggle;
    private CustomerQuotaManager quotaManager;
    private Coroutine            crossfadeCoroutine;

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

        // Both sources start silent — crossfade drives all volume changes.
        InitAudioSource(dayAmbientSource);
        InitAudioSource(endOfDayAudioSource);

        spawner      = FindObjectOfType<CustomerSpawner2>();
        quotaManager = FindObjectOfType<CustomerQuotaManager>();
        if (spawner == null)
            Debug.LogWarning("[DayNightCycle5Min] No CustomerSpawner2 found in scene.", this);
    }

    private void InitAudioSource(AudioSource src)
    {
        if (src == null) return;
        src.loop        = true;
        src.playOnAwake = false;
        src.volume      = 0f;
        src.Stop();
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Called by ShopToggle the moment the button is clicked and the label reads "Open".
    /// The day ambient begins fading in immediately on the same frame.
    /// </summary>
    public void StartDay(ShopToggle toggle)
    {
        if (dayRunning) return;

        shopToggle  = toggle;
        dayRunning  = true;
        currentTime = 0f;

        quotaManager?.ResetForNewDay();
        spawner?.ResumeSpawning();

        // Fade day ambient IN and end-of-day source OUT — starts immediately.
        Crossfade(fadeIn: dayAmbientSource, fadeOut: endOfDayAudioSource);

        Debug.Log("[DayNightCycle5Min] Day started — day ambient fading in.");
    }

    /// <summary>Called by CustomerQuotaManager when the daily quota is reached early.</summary>
    public void TriggerEarlyEnd()
    {
        if (isFinalStage || !dayRunning) return;
        currentTime = realSecondsForDay;
    }

    // ── Update ────────────────────────────────────────────────────────────────

    void Update()
    {
        if (!dayRunning || isFinalStage) return;

        currentTime += Time.deltaTime;

        if (!customerWarningFired && customerWarningLeadTime > 0f &&
            realSecondsForDay - currentTime <= customerWarningLeadTime)
        {
            customerWarningFired = true;
            spawner?.StopSpawning();
            StartCoroutine(DismissAllCustomersStaggered());
        }

        if (currentTime >= realSecondsForDay) { EnterFinalStage(); return; }

        UpdateBackgroundAndClock();
    }

    // ── Customer dismissal ────────────────────────────────────────────────────

    private IEnumerator DismissAllCustomersStaggered()
    {
        List<CustomerMover2> toDismiss = new List<CustomerMover2>();

        if (spawner != null)
        {
            for (int i = spawner.customers.Count - 1; i >= 0; i--)
            {
                CustomerMover2 c = spawner.customers[i];
                if (c != null && !c.IsLeaving) toDismiss.Add(c);
            }
        }
        else
        {
            CustomerMover2[] all = FindObjectsOfType<CustomerMover2>();
            for (int i = all.Length - 1; i >= 0; i--)
                if (all[i] != null && !all[i].IsLeaving) toDismiss.Add(all[i]);
        }

        int count = toDismiss.Count;
        for (int i = 0; i < count; i++)
        {
            CustomerMover2 customer = toDismiss[i];
            if (customer == null) { yield return null; continue; }

            customer.SetFace(2);
            customer.LeaveAndDie(endOfDayDismissal: true);

            if (i < count - 1 && dismissalStagger > 0f)
                yield return new WaitForSeconds(dismissalStagger);
        }

        Debug.Log($"[DayNightCycle5Min] Dismissal complete — {count} customer(s) sent home.");
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
        dayRunning   = false;

        spawner?.StopSpawning();

        if (!customerWarningFired)
            StartCoroutine(DismissAllCustomersStaggered());

        if (finalEndOfDayBackground != null && backgroundRenderer != null)
            StartCoroutine(FadeBackground(finalEndOfDayBackground, finalFadeDuration));

        if (clockText != null)
            clockText.text = "08:00 PM";

        // Fade end-of-day source IN and day ambient OUT.
        Crossfade(fadeIn: endOfDayAudioSource, fadeOut: dayAmbientSource);

        shopToggle?.SetClosed();

        isFinalStage         = false;
        customerWarningFired = false;
        currentTime          = 0f;

        Debug.Log("[DayNightCycle5Min] Day complete (8 PM) — shop closed.");
    }

    // ── Background fade ───────────────────────────────────────────────────────

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

    // ── Audio crossfade ───────────────────────────────────────────────────────

    private void Crossfade(AudioSource fadeIn, AudioSource fadeOut)
    {
        if (crossfadeCoroutine != null) StopCoroutine(crossfadeCoroutine);
        crossfadeCoroutine = StartCoroutine(CrossfadeRoutine(fadeIn, fadeOut, audioCrossfadeDuration));
    }

    private IEnumerator CrossfadeRoutine(AudioSource fadeIn, AudioSource fadeOut, float duration)
    {
        // Resolve target volume from the matching inspector field.
        float targetVol = (fadeIn == dayAmbientSource) ? dayAmbientMaxVolume : endOfDayMaxVolume;

        // Start the incoming source from the beginning at silent volume.
        if (fadeIn != null && fadeIn.clip != null)
        {
            fadeIn.volume = 0f;
            fadeIn.Stop();
            fadeIn.loop = true;
            fadeIn.Play();
        }

        float startOutVol = fadeOut != null ? fadeOut.volume : 0f;
        float elapsed     = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t  = elapsed / duration;

            if (fadeIn  != null) fadeIn.volume  = Mathf.Lerp(0f,          targetVol, t);
            if (fadeOut != null) fadeOut.volume = Mathf.Lerp(startOutVol, 0f,        t);

            yield return null;
        }

        if (fadeIn  != null) fadeIn.volume  = targetVol;
        if (fadeOut != null) { fadeOut.volume = 0f; fadeOut.Stop(); }

        crossfadeCoroutine = null;
    }

    // ── Debug tools ───────────────────────────────────────────────────────────

    [ContextMenu("Jump to 8 PM")]
    void JumpToEndDebug()
    {
        currentTime = realSecondsForDay - 0.1f;
        UpdateBackgroundAndClock();
    }

    [ContextMenu("Force Start Day")]
    void ForceStartDayDebug()
    {
        StartDay(FindObjectOfType<ShopToggle>());
    }
}