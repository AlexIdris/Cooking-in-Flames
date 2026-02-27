using UnityEngine;
using System.Collections;
using TMPro;

public class CustomerMover2 : MonoBehaviour
{
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
    }

    public void SetOrder(FoodType food)
    {
        orderedFood = food;

        // convert enum to readable text
        string label = "";

        switch (food)
        {
            case FoodType.NormalBurger:
                label = "Normal Burger";
                break;

            case FoodType.TomatoBurger:
                label = "Burger w/ Tomato";
                break;

            case FoodType.PattyOnly:
                label = "Patty Only";
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
            }
        }

        // Leaving behaviour
        if (leaving)
        {
            transform.Translate(Vector3.left * moveSpeed * Time.deltaTime);
        }
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