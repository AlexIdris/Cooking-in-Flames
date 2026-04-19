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
    [Tooltip("How long the customer displays their reaction face before being destroyed.")]
    public float reactionDuration = 3f;

    [HideInInspector] public CustomerSpawner2              spawner;
    [HideInInspector] public CustomerSpawner2.CharacterData currentCharacter;

    /// <summary>Base prefab scale read by CustomerSpawner2 for queue scaling.</summary>
    public float BaseScale => baseScale.x;

    /// <summary>True once the customer has been served and is walking off screen.</summary>
    public bool IsLeaving { get; private set; }

    private Transform      targetPoint;
    private float          targetScale;
    private bool           movingToPoint;
    private Vector3        baseScale;
    private SpriteRenderer sr;

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        if (baseScale == Vector3.zero) baseScale = transform.localScale;
    }

    // ── Initialisation ────────────────────────────────────────────────────────

    public void Init(Transform newTarget, float startMultiplier, float targetMultiplier)
    {
        targetPoint = newTarget;
        transform.localScale = Vector3.one * (baseScale.x * startMultiplier);
        targetScale          = baseScale.x * targetMultiplier;
        movingToPoint        = true;
        IsLeaving            = false;
    }

    // ── Order ─────────────────────────────────────────────────────────────────

    public void SetOrder(FoodType food)
    {
        orderedFood = food;

        string label;
        switch (food)
        {
            case FoodType.BaconBurger:            label = "Bacon Burger";             break;
            case FoodType.BaconCheeseBurger:      label = "Bacon Cheese Burger";      break;
            case FoodType.BurgerWithTomato:       label = "Burger With Tomato";       break;
            case FoodType.CaseOhBurger:           label = "CaseOh Burger";            break;
            case FoodType.CheeseBurger:           label = "Cheese Burger";            break;
            case FoodType.CheeseLettuceBurger:    label = "Cheese Lettuce Burger";    break;
            case FoodType.CucumberBurger:         label = "Cucumber Burger";          break;
            case FoodType.CucumberCheeseBurger:   label = "Cucumber Cheese Burger";   break;
            case FoodType.MexicanBurger:          label = "Mexican Burger";           break;
            case FoodType.MexicanCheeseBurger:    label = "Mexican Cheese Burger";    break;
            case FoodType.MixBurgerNoCheese:      label = "Mix Burger No Cheese";     break;
            case FoodType.OGCheeseBurger:         label = "OG Cheese Burger";         break;
            case FoodType.OnionBurger:            label = "Onion Burger";             break;
            case FoodType.OnionCheeseBurger:      label = "Onion Cheese Burger";      break;
            case FoodType.SimplePatty:            label = "Simple Patty";             break;
            case FoodType.SimpleDoubleBurger:     label = "Simple Double Burger";     break;
            case FoodType.SimpleTripleBurger:     label = "Simple Triple Burger";     break;
            case FoodType.TripleAllMixBurger:     label = "Triple All Mix Burger";    break;
            case FoodType.TripleBurgerWithTomato: label = "Triple Burger With Tomato";break;
            default:                              label = food.ToString();            break;
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
                movingToPoint = false;
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

        // Guard: never consume an item that is still held by the player
        Pickupable2D pickup = other.GetComponent<Pickupable2D>();
        if (pickup != null && pickup.IsHeld) return;

        SetFace(food.foodType == orderedFood ? 1 : 2);

        SpawnCleanupManager.MarkAsHeld(other.gameObject);
        Destroy(other.gameObject);

        LeaveAndDie();
    }

    // ── Leave ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Triggers the walk-off animation and schedules destruction.
    /// Safe to call from DayNightCycle5Min, CustomerSpawner2 debug keys, or
    /// OnTriggerEnter2D — subsequent calls are ignored.
    /// </summary>
    public void LeaveAndDie()
    {
        if (IsLeaving) return;
        IsLeaving     = true;
        movingToPoint = false;
        SetOrderText("");
        StartCoroutine(DieAfterSeconds(reactionDuration));
    }

    private IEnumerator DieAfterSeconds(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        Destroy(gameObject);
    }

    // ── Face / mood ───────────────────────────────────────────────────────────

    /// <summary>
    /// 1 = happy  |  2 = sad  |  3 = neutral
    /// Called by OnTriggerEnter2D, debug keys, and DayNightCycle5Min.
    /// </summary>
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