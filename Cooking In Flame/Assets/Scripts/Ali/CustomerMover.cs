using UnityEngine;
using TMPro;

public class CustomerMover : MonoBehaviour
{
    public Transform targetPoint;

    public float moveSpeed = 2f;

    public float startScale = 2f;
    public float targetScale = 4f;

    private Vector3 startPos;
    private float journeyLength;
    private float startTime;

    public TextMeshPro orderText;

    string[] possibleOrders = new string[]
    {
    "Cheese Burger",
    "Fries Only",
    "Chicken Burger",
    "Fish Nuggets",
    "Double Burger"
    };

    bool orderShown = false;

    void Start()
    {
        Destroy(gameObject, 10f);
    }

    public void Init(Transform target, float startS, float targetS)
    {
        targetPoint = target;
        startScale = startS;
        targetScale = targetS;

        startPos = transform.position;
        startTime = Time.time;
        journeyLength = Vector3.Distance(startPos, targetPoint.position);

        transform.localScale = Vector3.one * startScale;

        // Reset order text so it can show again if this customer becomes front
        orderShown = false;
        if (orderText != null)
            orderText.gameObject.SetActive(false);
    }

    void Update()
    {
        if (targetPoint == null) return;

        float distCovered = (Time.time - startTime) * moveSpeed;
        float fraction = distCovered / journeyLength;

        transform.position = Vector3.Lerp(startPos, targetPoint.position, fraction);

        float scale = Mathf.Lerp(startScale, targetScale, fraction);
        transform.localScale = Vector3.one * scale;

        if (!orderShown && Vector3.Distance(transform.position, targetPoint.position) < 0.1f)
        {
            // only show if this customer is at index 0 (front of line)
            if (targetScale == 4f)
            {
                ShowRandomOrder();
            }

            orderShown = true;
        }
    }

    void ShowRandomOrder()
    {
        if (orderText == null) return;

        int index = Random.Range(0, possibleOrders.Length);
        orderText.text = possibleOrders[index];
        orderText.gameObject.SetActive(true);
    }
}
