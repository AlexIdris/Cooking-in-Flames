using UnityEngine;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// Tracks successful and unsuccessful customer deliveries each day and displays
/// the running count as "X / Y" on a Canvas TextMeshPro label.
///
/// COUNTING RULES
/// ───────────────
/// +1  Customer received the CORRECT food item (happy face).
/// -1  Customer received the WRONG food item (sad face), or left without being
///     served at all (e.g. order timer expired, FailOrder called).
///  0  Customer was dismissed at end-of-day (DayNightCycle5Min dismissal or
///     DismissAllHappily quota completion) — these never affect the score.
///
/// The counter is clamped to [0, customersPerDay] — it cannot go negative.
///
/// QUOTA REACHED
/// ──────────────
/// When successfulDeliveries reaches customersPerDay:
///   1. CustomerSpawner2 stops spawning new customers immediately.
///   2. Every remaining customer gets a happy face and LeaveAndDie(endOfDay:true).
///   3. DayNightCycle5Min.TriggerEarlyEnd() ends the day.
///
/// SETUP
/// ──────
/// 1. Add this component to any persistent GameObject (e.g. GameManager).
/// 2. Assign quotaLabel (TextMeshProUGUI), spawner, and dayNightCycle, or leave
///    blank to auto-find.
/// 3. Set customersPerDay in the Inspector.
/// </summary>
public class CustomerQuotaManager : MonoBehaviour
{
    [Header("Quota Settings")]
    [Tooltip("Number of CORRECT deliveries (happy customers) required to complete the day.\n" +
             "Once reached, remaining customers leave happily and the day ends immediately.")]
    [Min(1)] public int customersPerDay = 5;

    [Header("UI")]
    [Tooltip("TextMeshProUGUI that displays the current score — e.g. '3 / 5'.")]
    public TextMeshProUGUI quotaLabel;

    [Tooltip("Label colour when the quota is full.")]
    public Color quotaFullColor   = new Color(1f, 0.8f, 0.2f, 1f);

    [Tooltip("Label colour during normal play.")]
    public Color quotaNormalColor = Color.white;

    [Tooltip("Label colour when the score drops below zero (penalty state).")]
    public Color quotaPenaltyColor = new Color(1f, 0.3f, 0.3f, 1f);

    [Header("Scene References (auto-found if left blank)")]
    public CustomerSpawner2  spawner;
    public DayNightCycle5Min dayNightCycle;

    // ── Runtime state ─────────────────────────────────────────────────────────

    private int successfulDeliveries = 0;

    /// <summary>Current score (happy deliveries minus missed/wrong ones).</summary>
    public int SuccessfulDeliveries => successfulDeliveries;

    /// <summary>True once the required score has been reached.</summary>
    public bool IsQuotaFull => successfulDeliveries >= customersPerDay;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    void Start()
    {
        if (spawner       == null) spawner       = FindObjectOfType<CustomerSpawner2>();
        if (dayNightCycle == null) dayNightCycle = FindObjectOfType<DayNightCycle5Min>();

        if (spawner == null)
            Debug.LogWarning("[CustomerQuotaManager] No CustomerSpawner2 found.", this);
        if (dayNightCycle == null)
            Debug.LogWarning("[CustomerQuotaManager] No DayNightCycle5Min found.", this);

        if (spawner != null) spawner.SetQuotaManager(this);

        RefreshUI();
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Called by CustomerMover2 when a food item is received.
    /// +1 for correct order, -1 for wrong order. End-of-day dismissals never call this.
    /// </summary>
    public void RegisterDelivery(bool wasCorrectOrder)
    {
        if (IsQuotaFull) return;

        if (wasCorrectOrder)
        {
            successfulDeliveries++;
            Debug.Log($"[CustomerQuotaManager] +1 happy delivery: {successfulDeliveries} / {customersPerDay}");
        }
        else
        {
            successfulDeliveries = Mathf.Max(0, successfulDeliveries - 1);
            Debug.Log($"[CustomerQuotaManager] -1 wrong order: {successfulDeliveries} / {customersPerDay}");
        }

        RefreshUI();
        CheckQuota();
    }

    /// <summary>
    /// Called by CustomerMover2 when a customer leaves without receiving any food.
    /// Subtracts one from the score (clamped to 0).
    /// Only called for genuine missed orders — NOT for end-of-day dismissals.
    /// </summary>
    public void RegisterMissed()
    {
        if (IsQuotaFull) return;

        successfulDeliveries = Mathf.Max(0, successfulDeliveries - 1);
        Debug.Log($"[CustomerQuotaManager] -1 missed customer: {successfulDeliveries} / {customersPerDay}");

        RefreshUI();
        // A missed customer does NOT trigger quota completion — only happy ones do.
    }

    /// <summary>
    /// Resets the counter for a new day. Called by DayNightCycle5Min.StartDay().
    /// </summary>
    public void ResetForNewDay()
    {
        successfulDeliveries = 0;
        RefreshUI();
        Debug.Log("[CustomerQuotaManager] Quota reset for new day.");
    }

    // ── Quota check ───────────────────────────────────────────────────────────

    private void CheckQuota()
    {
        if (!IsQuotaFull) return;

        Debug.Log("[CustomerQuotaManager] Quota reached — ending day early.");
        spawner?.StopSpawning();
        DismissAllHappily();
        dayNightCycle?.TriggerEarlyEnd();
    }

    // ── Dismissal ─────────────────────────────────────────────────────────────

    private void DismissAllHappily()
    {
        if (spawner == null) return;

        List<CustomerMover2> snapshot = new List<CustomerMover2>(spawner.customers);
        foreach (CustomerMover2 customer in snapshot)
        {
            if (customer == null || customer.IsLeaving) continue;
            customer.SetFace(1);
            customer.LeaveAndDie(endOfDayDismissal: true);   // exempt from RegisterMissed
        }
    }

    // ── UI ────────────────────────────────────────────────────────────────────

    private void RefreshUI()
    {
        if (quotaLabel == null) return;
        quotaLabel.text = $"{successfulDeliveries} / {customersPerDay}";

        if (IsQuotaFull)
            quotaLabel.color = quotaFullColor;
        else if (successfulDeliveries <= 0)
            quotaLabel.color = quotaPenaltyColor;
        else
            quotaLabel.color = quotaNormalColor;
    }
}