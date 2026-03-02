using UnityEngine;
using TMPro;                            // ← Required for TextMeshPro
using System.Collections;

public class DayNightCycle5Min : MonoBehaviour
{
    [Header("Day Cycle Settings")]
    [Tooltip("Full in-game period (8:00 AM → 8:00 PM) = 5 minutes real time (300 seconds)")]
    public float realSecondsPerDay = 300f;

    [Header("In-Game Time Range")]
    [Tooltip("Start of the in‑game period (hours, 24h)")]
    public float startHour = 8f;   // 8:00 AM
    [Tooltip("End of the in‑game period (hours, 24h)")]
    public float endHour = 20f;    // 20:00 (8 PM)

    [Header("Background Stages (evenly over time)")]
    [Tooltip("Sprites will be used in order from start to end time")]
    public Sprite[] backgroundStages;

    [Header("Scene References")]
    [Tooltip("The SpriteRenderer showing the background (large sprite/quad)")]
    public SpriteRenderer backgroundRenderer;

    [Header("Alarm Clock in World")]
    [Tooltip("The TextMeshPro component on the clock face (world-space, NOT UI)")]
    public TextMeshPro clockText;       // TextMeshPro (not TextMeshProUGUI)

    [Tooltip("Optional: Clock face sprite to tint at night (last stage)")]
    public SpriteRenderer clockFaceRenderer;

    public Color dayClockColor = Color.white;
    public Color nightClockColor = new Color(0.9f, 0.9f, 1f, 1f);

    // Internal tracking
    private float gameTimeSeconds = 0f;

    void Start()
    {
        if (backgroundRenderer == null)
        {
            Debug.LogError("BackgroundRenderer not assigned!", this);
        }

        if (clockText == null)
        {
            Debug.LogError("ClockText (TextMeshPro) not assigned! " +
                           "Drag the TextMeshPro component from your alarm clock GameObject.", this);
        }
        else
        {
            clockText.alignment = TextAlignmentOptions.Center;
            clockText.fontSize = 2; // adjust in Inspector if needed
            clockText.color = dayClockColor;
        }

        gameTimeSeconds = 0f;  // start at 8:00 AM
        UpdateBackgroundAndClock();
    }

    void Update()
    {
        // Advance real time
        gameTimeSeconds += Time.deltaTime;

        UpdateBackgroundAndClock();
    }

    private void UpdateBackgroundAndClock()
    {
        if (realSecondsPerDay <= 0f)
        {
            Debug.LogWarning("realSecondsPerDay must be > 0");
            return;
        }

        // 0 → 1 over the 5-minute period, then clamp so it stops at the end
        float dayProgress = Mathf.Clamp01(gameTimeSeconds / realSecondsPerDay);

        // Map 0–1 to 8:00 → 20:00 (8 PM)
        float inGameHours = Mathf.Lerp(startHour, endHour, dayProgress);

        // -------------------------
        // BACKGROUND SPRITE CHANGE
        // -------------------------
        if (backgroundRenderer != null && backgroundStages != null && backgroundStages.Length > 0)
        {
            int spriteCount = backgroundStages.Length;

            // Even ranges across the whole period
            int stageIndex = Mathf.FloorToInt(dayProgress * spriteCount);
            stageIndex = Mathf.Clamp(stageIndex, 0, spriteCount - 1);

            backgroundRenderer.sprite = backgroundStages[stageIndex];

            bool isLastStage = (stageIndex == spriteCount - 1);

            // -------------------------
            // CLOCK TEXT (12H + AM/PM)
            // -------------------------
            int hours24 = Mathf.FloorToInt(inGameHours);
            int minutes = Mathf.FloorToInt((inGameHours - hours24) * 60f);

            // Convert to 12‑hour format
            string ampm = (hours24 >= 12) ? "PM" : "AM";
            int displayHour = hours24 % 12;
            if (displayHour == 0) displayHour = 12;

            string timeString = $"{displayHour:00}:{minutes:00} {ampm}";

            if (clockText != null)
            {
                clockText.text = timeString;
                clockText.color = isLastStage ? nightClockColor : dayClockColor;
            }

            // Optional: tint clock face only on last stage
            if (clockFaceRenderer != null)
            {
                clockFaceRenderer.color = isLastStage ? nightClockColor : dayClockColor;
            }
        }
        else
        {
            // If no backgrounds, still update time in 12h format
            int hours24 = Mathf.FloorToInt(inGameHours);
            int minutes = Mathf.FloorToInt((inGameHours - hours24) * 60f);

            string ampm = (hours24 >= 12) ? "PM" : "AM";
            int displayHour = hours24 % 12;
            if (displayHour == 0) displayHour = 12;

            string timeString = $"{displayHour:00}:{minutes:00} {ampm}";

            if (clockText != null)
            {
                clockText.text = timeString;
            }
        }
    }

    // Debug helper: skip to end (right-click component → Skip To 8 PM)
    [ContextMenu("Skip To 8 PM")]
    void SkipToEndDebug()
    {
        gameTimeSeconds = realSecondsPerDay;
        UpdateBackgroundAndClock();
    }
}