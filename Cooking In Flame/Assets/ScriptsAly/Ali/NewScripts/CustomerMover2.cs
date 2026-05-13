using UnityEngine;
using System.Collections;
using TMPro;

public class CustomerMover2 : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed  = 3f;
    public float scaleSpeed = 2f;

    [Header("Order")]
    public FoodType    orderedFood;
    public TextMeshPro orderText;

    [Header("Serving")]
    [Tooltip("Seconds the customer displays their reaction face before being destroyed.")]
    public float reactionDuration = 3f;

    [HideInInspector] public CustomerSpawner2               spawner;
    [HideInInspector] public CustomerSpawner2.CharacterData  currentCharacter;

    // ── Public state ──────────────────────────────────────────────────────────

    public float BaseScale => baseScale.x;

    /// <summary>True once LeaveAndDie() has been called.</summary>
    public bool IsLeaving { get; private set; }

    public bool hasReachedPoint { get; private set; }

    // ── Events ────────────────────────────────────────────────────────────────

    public delegate void OnReachPointHandler(CustomerMover2 mover);
    public event OnReachPointHandler OnReachPoint;

    // ── Private ───────────────────────────────────────────────────────────────

    private Transform      targetPoint;
    private float          targetScale;
    private bool           movingToPoint;
    private bool           hasFailed;
    private bool           wasServed;          // set to true only after food is received
    private bool           isEndOfDayLeave;    // true when dismissed by the day system
    private Vector3        baseScale;
    private SpriteRenderer sr;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        if (baseScale == Vector3.zero) baseScale = transform.localScale;
    }

    // ── Init ──────────────────────────────────────────────────────────────────

    public void Init(Transform newTarget, float startMultiplier, float targetMultiplier)
    {
        targetPoint          = newTarget;
        transform.localScale = Vector3.one * (baseScale.x * startMultiplier);
        targetScale          = baseScale.x * targetMultiplier;
        movingToPoint        = true;
        IsLeaving            = false;
        hasReachedPoint      = false;
        wasServed            = false;
        isEndOfDayLeave      = false;
        hasFailed            = false;
    }

    // ── Order ─────────────────────────────────────────────────────────────────

    public void SetOrder(FoodType food)
    {
        orderedFood = food;

        string label;
        switch (food)
        {
            case FoodType.Burger:             label = "Burger";              break;
            case FoodType.HealthyBurger:      label = "HealthyBurger";       break;
            case FoodType.LeafyCheeseBurger:  label = "LeafyCheeseBurger";   break;
            case FoodType.MexcianBurger:      label = "MexcianBurger";       break;
            case FoodType.OnionBurger:        label = "OnionBurger";         break;
            case FoodType.SussyCheeseBurger:  label = "SussyCheeseBurger";   break;
            case FoodType.SimplePatty:        label = "SimplePatty";           break;
            default:                          label = food.ToString();       break;
        }

        SetOrderText(label);
    }

    public void SetOrderText(string text)
    {
        if (orderText != null) orderText.text = text;
    }

    // ── Update ────────────────────────────────────────────────────────────────

    void Update()
    {
        if (movingToPoint && targetPoint != null)
        {
            transform.position = Vector3.MoveTowards(
                transform.position, targetPoint.position, moveSpeed * Time.deltaTime);

            float newScale = Mathf.MoveTowards(
                transform.localScale.x, targetScale, scaleSpeed * Time.deltaTime);
            transform.localScale = Vector3.one * newScale;

            if (Vector3.Distance(transform.position, targetPoint.position) < 0.05f)
            {
                movingToPoint   = false;
                hasReachedPoint = true;
                OnReachPoint?.Invoke(this);
            }
        }

        if (IsLeaving)
            transform.Translate(Vector3.left * moveSpeed * Time.deltaTime);
    }

    // ── Food delivery ─────────────────────────────────────────────────────────

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (spawner == null || spawner.customers.Count == 0 || spawner.customers[0] != this) return;
        if (IsLeaving) return;

        FoodItem food = other.GetComponent<FoodItem>();
        if (food == null) return;

        Pickupable2D pickup = other.GetComponent<Pickupable2D>();
        if (pickup != null && pickup.IsHeld) return;

        bool wasCorrect = food.foodType == orderedFood;
        SetFace(wasCorrect ? 1 : 2);

        SpawnCleanupManager.MarkAsHeld(other.gameObject);
        Destroy(other.gameObject);

        wasServed = true;   // food received — suppress RegisterMissed in LeaveAndDie

        // Notify quota — +1 for correct, -1 for wrong
        if (spawner != null)
        {
            CustomerQuotaManager quota = spawner.GetQuotaManager();
            quota?.RegisterDelivery(wasCorrect);
        }

        LeaveAndDie();
    }

    // ── Leave & fail ──────────────────────────────────────────────────────────

    /// <summary>
    /// Triggers walk-off and schedules destruction.
    ///
    /// <paramref name="endOfDayDismissal"/> — set true when called by
    /// DayNightCycle5Min or CustomerQuotaManager.DismissAllHappily so the
    /// departure is NOT counted as a missed order against the quota.
    ///
    /// When false (default) and the customer was never served, RegisterMissed()
    /// is called on the quota manager so the score drops by one.
    /// </summary>
    public void LeaveAndDie(bool endOfDayDismissal = false)
    {
        if (IsLeaving) return;

        IsLeaving       = true;
        isEndOfDayLeave = endOfDayDismissal;
        movingToPoint   = false;
        SetOrderText("");

        // If the customer leaves without ever receiving food AND this is not an
        // end-of-day dismissal, subtract one from the quota score.
        if (!wasServed && !endOfDayDismissal)
        {
            CustomerQuotaManager quota = spawner?.GetQuotaManager();
            quota?.RegisterMissed();
        }

        // Clear all ingredient plates when a served customer leaves so the
        // ingredients used to prepare their order are cleaned up automatically.
        // End-of-day dismissals are excluded — the day is ending anyway.
        if (wasServed && !endOfDayDismissal)
            IngredientMerger2D.ClearAllPlates();

        StartCoroutine(DieAfterSeconds(reactionDuration));
    }

    /// <summary>
    /// Shows sad face then triggers walk-off as a missed order.
    /// Called when a customer's order timer expires.
    /// </summary>
    public void FailOrder()
    {
        if (hasFailed || IsLeaving) return;
        hasFailed = true;
        SetFace(2);
        LeaveAndDie(endOfDayDismissal: false);   // counts as missed
    }

    private IEnumerator DieAfterSeconds(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        Destroy(gameObject);
    }

    // ── Face ──────────────────────────────────────────────────────────────────

    /// <summary>1 = happy  |  2 = sad  |  3 = neutral</summary>
    public void SetFace(int mood)
    {
        if (sr == null || currentCharacter == null) return;
        switch (mood)
        {
            case 1: sr.sprite = currentCharacter.happyFace;  break;
            case 2: sr.sprite = currentCharacter.sadFace;    break;
            case 3: sr.sprite = currentCharacter.normalFace; break;
        }
    }
}