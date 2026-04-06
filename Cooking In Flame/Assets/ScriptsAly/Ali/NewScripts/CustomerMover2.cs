using UnityEngine;
using System.Collections;
using TMPro;

public class CustomerMover2 : MonoBehaviour
{
    bool hasFailed = false;

    public bool hasReachedPoint = false;

    public float moveSpeed = 3f;
    public float scaleSpeed = 2f;

    public FoodType orderedFood;

    public TextMeshPro orderText;

    private Transform targetPoint;
    private float targetScale;
    private bool movingToPoint = false;
    private bool leaving = false;

    private Vector3 baseScale;
    public float BaseScale => baseScale.x;

    [HideInInspector] public CustomerSpawner2 spawner;
    [HideInInspector] public CustomerSpawner2.CharacterData currentCharacter;

    private SpriteRenderer sr;

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        if (baseScale == Vector3.zero) baseScale = transform.localScale;
    }

    public void Init(Transform newTarget, float startMultiplier, float targetMultiplier)
    {
        targetPoint = newTarget;

        float startScale = baseScale.x * startMultiplier;
        float endScale = baseScale.x * targetMultiplier;

        transform.localScale = Vector3.one * startScale;
        targetScale = endScale;

        movingToPoint = true;
        leaving = false;

        hasReachedPoint = false;
    }

    public void SetOrder(FoodType food)
    {
        orderedFood = food;

        // convert enum to readable text
        string label = "";

        switch (food)
        {

            case FoodType.BaconBurger:
                label = "Bacon Burger";
                break;

            case FoodType.BaconCheeseBurger:
                label = "Bacon Cheese Burger";
                break;

            case FoodType.BurgerWithTomato:
                label = "Burger With Tomato";
                break;

            case FoodType.CaseOhBurger:
                label = "CaseOh Burger";
                break;

            case FoodType.CheeseBurger:
                label = "Cheese Burger";
                break;



            case FoodType.CheeseLettuceBurger:
                label = "Cheese Lettuce Burger";
                break;
            case FoodType.CucumberBurger:
                label = "Cucumber Burger";
                break;
            case FoodType.CucumberCheeseBurger:
                label = "Cucumber Cheese Burger";
                break;
            case FoodType.MexicanBurger:
                label = "Mexican Burger";
                break;
            case FoodType.MexicanCheeseBurger:
                label = "Mexican Cheese Burger";
                break;
            case FoodType.MixBurgerNoCheese:
                label = "Mix Burger No Cheese";
                break;
            case FoodType.OGCheeseBurger:
                label = "OG Cheese Burger";
                break;
            case FoodType.OnionBurger:
                label = "Onion Burger";
                break;
            case FoodType.OnionCheeseBurger:
                label = "Onion Cheese Burger";
                break;
            case FoodType.SimplePatty:
                label = "Simple Patty";
                break;
            case FoodType.SimpleDoubleBurger:
                label = "Simple Double Burger";
                break;
            case FoodType.SimpleTripleBurger:
                label = "Simple Triple Burger";
                break;
            case FoodType.TripleAllMixBurger:
                label = "Triple All Mix Burger";
                break;
            case FoodType.TripleBurgerWithTomato:
                label = "Triple Burger With Tomato";
                break;

        }

        SetOrderText(label);
    }

    

    void Update()
    {
        // Move to queue position
        if (movingToPoint && targetPoint != null)
        {
            transform.position = Vector3.MoveTowards(
                transform.position,
                targetPoint.position,
                moveSpeed * Time.deltaTime
            );

            float current = transform.localScale.x;
            float newScale = Mathf.MoveTowards(current, targetScale, scaleSpeed * Time.deltaTime);
            transform.localScale = Vector3.one * newScale;

            if (Vector3.Distance(transform.position, targetPoint.position) < 0.05f)
            {
                movingToPoint = false;
                hasReachedPoint = true;
            }
        }

        // Leaving behaviour
        if (leaving)
        {
            transform.Translate(Vector3.left * moveSpeed * Time.deltaTime);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (spawner.customers[0] != this) return; // ignore if not front

        if (leaving) return; // ignore if already leaving

        FoodItem food = other.GetComponent<FoodItem>();
        if (food == null) return; // ignore non-food objects

        // check if correct
        if (food.foodType == orderedFood)
        {
            SetFace(1); // happy
        }
        else
        {
            SetFace(2); // sad
        }

        // destroy the food after serving
        Destroy(other.gameObject);

        // leave after reaction
        LeaveAndDie();
    }

    public void SetOrderText(string text)
    {
        if (orderText != null)
        {
            orderText.text = text;
        }
    }

    public void LeaveAndDie()
    {
        if (leaving) return;

        leaving = true;
        movingToPoint = false;

        StartCoroutine(DieAfterSeconds(3f));
    }

    public void FailOrder()
    {
        if (hasFailed) return;

        hasFailed = true;

        SetFace(2); // sad face (based on your setup)

        LeaveAndDie(); // you already made this earlier
    }

    IEnumerator DieAfterSeconds(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        Destroy(gameObject);
    }

    public void SetFace(int mood)
    {
        if (sr == null || currentCharacter == null) return;

        switch (mood)
        {
            case 1: sr.sprite = currentCharacter.happyFace; break;
            case 2: sr.sprite = currentCharacter.sadFace; break;
            case 3: sr.sprite = currentCharacter.normalFace; break;
        }
    }
}