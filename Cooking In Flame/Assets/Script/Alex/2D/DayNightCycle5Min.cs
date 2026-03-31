using UnityEngine;
using TMPro;
using System.Collections;
using System.Collections.Generic;

public class DayNightCycle5Min : MonoBehaviour
{
    [Header("Cycle Timing")]
    [Tooltip("Real seconds for the active day (8 AM – 8 PM)")]
    public float realSecondsForDay = 300f; // 5 minutes = 300 seconds

    [Header("Background Stages – Add as many as you want")]
    [Tooltip("Define exact start/end hours (8–20). End of one = start of next.")]
    public List<BackgroundStage> stages = new List<BackgroundStage>();

    [Header("Final Stage (after 20:00)")]
    public Sprite finalEndOfDayBackground;

    [Header("Scene References")]
    public SpriteRenderer backgroundRenderer;

    [Tooltip("TextMeshPro child of the alarm clock showing time")]
    public TextMeshPro clockText;

    [Header("Fade Durations")]
    [Tooltip("Fade time when changing between main day stages")]
    [Range(0.1f, 3f)]
    public float stageFadeDuration = 1.2f;

    [Tooltip("Fade time when entering the final end-of-day stage")]
    [Range(0.5f, 6f)]
    public float finalFadeDuration = 2.5f;

    [Header("Audio")]
    public AudioSource dayAmbientSource;
    public AudioClip endOfDaySound;

    [System.Serializable]
    public class BackgroundStage
    {
        public string stageName = "Morning";
        public Sprite backgroundSprite;
        [Range(8f, 20f)] public float startHour = 8f;
        [Range(8f, 20f)] public float endHour = 14f;
    }

    private float currentTime = 0f;
    private bool isFinalStage = false;
    private Sprite currentBackgroundSprite;

    void Start()
    {
        if (backgroundRenderer == null) Debug.LogError("Background Renderer not assigned!", this);
        if (clockText == null) Debug.LogError("Clock TextMeshPro not assigned!", this);
        if (stages.Count == 0) Debug.LogError("No background stages defined!", this);

        // Sort stages by start hour
        stages.Sort((a, b) => a.startHour.CompareTo(b.startHour));

        currentBackgroundSprite = stages.Count > 0 ? stages[0].backgroundSprite : null;
        if (backgroundRenderer != null && currentBackgroundSprite != null)
            backgroundRenderer.sprite = currentBackgroundSprite;

        UpdateBackgroundAndClock();

        // Start day ambient loop
        if (dayAmbientSource != null && dayAmbientSource.clip != null)
        {
            dayAmbientSource.loop = true;
            dayAmbientSource.Play();
        }
    }

    void Update()
    {
        if (isFinalStage) return;

        currentTime += Time.deltaTime;

        // Day ends at 8 PM (12 in-game hours)
        if (currentTime >= realSecondsForDay)
        {
            EnterFinalStage();
            return;
        }

        UpdateBackgroundAndClock();
    }

    private void UpdateBackgroundAndClock()
    {
        float dayProgress = currentTime / realSecondsForDay;
        float inGameHours = 8f + (dayProgress * 12f); // 8 AM → 8 PM

        // Find current stage
        BackgroundStage activeStage = null;
        foreach (var stage in stages)
        {
            if (inGameHours >= stage.startHour && inGameHours < stage.endHour)
            {
                activeStage = stage;
                break;
            }
        }

        // Fallback: last stage if past last end hour
        if (activeStage == null && stages.Count > 0)
        {
            activeStage = stages[stages.Count - 1];
        }

        // Change background with fade if needed
        if (activeStage != null && activeStage.backgroundSprite != currentBackgroundSprite)
        {
            StartCoroutine(FadeBackground(activeStage.backgroundSprite, stageFadeDuration));
            currentBackgroundSprite = activeStage.backgroundSprite;
        }

        // Clock – only time (no day count)
        int hours = Mathf.FloorToInt(inGameHours) % 24;
        int minutes = Mathf.FloorToInt((inGameHours - hours) * 60f);

        string ampm = hours < 12 ? "AM" : "PM";
        int displayHours = hours % 12;
        if (displayHours == 0) displayHours = 12;

        string timeString = $"{displayHours:00}:{minutes:00} {ampm}";

        if (clockText != null)
        {
            clockText.text = timeString;
        }
    }

    private void EnterFinalStage()
    {
        isFinalStage = true;

        if (finalEndOfDayBackground != null && backgroundRenderer != null)
        {
            StartCoroutine(FadeBackground(finalEndOfDayBackground, finalFadeDuration));
        }

        // Freeze clock at 08:00 PM
        if (clockText != null)
        {
            clockText.text = "08:00 PM";
        }

        // End-of-day sound
        if (endOfDaySound != null)
        {
            AudioSource.PlayClipAtPoint(endOfDaySound, Camera.main.transform.position);
        }

        // Fade out day ambient
        if (dayAmbientSource != null && dayAmbientSource.isPlaying)
        {
            StartCoroutine(FadeOutAudio(dayAmbientSource, finalFadeDuration));
        }

        Debug.Log("Day period complete (8 PM) – timer stopped, final background active.");
    }

    private IEnumerator FadeBackground(Sprite newSprite, float duration)
    {
        SpriteRenderer rend = backgroundRenderer;
        if (rend == null) yield break;

        Color startColor = rend.color;
        float elapsed = 0f;

        // Fade out current
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            Color c = startColor;
            c.a = Mathf.Lerp(startColor.a, 0f, t);
            rend.color = c;
            yield return null;
        }

        // Switch sprite and fade in
        rend.sprite = newSprite;
        rend.color = new Color(1f, 1f, 1f, 0f);

        elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            rend.color = new Color(1f, 1f, 1f, Mathf.Lerp(0f, 1f, t));
            yield return null;
        }
    }

    private IEnumerator FadeOutAudio(AudioSource source, float duration)
    {
        float startVol = source.volume;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            source.volume = Mathf.Lerp(startVol, 0f, elapsed / duration);
            yield return null;
        }

        source.Stop();
    }

    // Debug tools
    [ContextMenu("Jump to 8 PM")]
    void JumpToEndDebug()
    {
        currentTime = realSecondsForDay - 0.1f;
        UpdateBackgroundAndClock();
    }

    [ContextMenu("Reset Cycle")]
    void ResetCycle()
    {
        currentTime = 0f;
        isFinalStage = false;
        if (dayAmbientSource != null && !dayAmbientSource.isPlaying)
            dayAmbientSource.Play();
        UpdateBackgroundAndClock();
    }
}