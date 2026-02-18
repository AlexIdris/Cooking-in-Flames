using UnityEngine;

public class ClientMovement : MonoBehaviour
{
    public Transform targetPoint;
    public float moveSpeed = 3f;

    public float bounceHeight = 0.1f;
    public float bounceSpeed = 8f;

    private Vector3 previousPosition;

    void Start()
    {
        previousPosition = transform.position;
    }

    void Update()
    {
        if (targetPoint == null) return;

        // Calculate distance to target
        float distance = Vector3.Distance(transform.position, targetPoint.position);

        if (distance > 0.01f)
        {
            // Move toward target
            transform.position = Vector3.MoveTowards(
                transform.position,
                targetPoint.position,
                moveSpeed * Time.deltaTime
            );

            // Bounce effect only while moving
            float bounce = Mathf.Sin(Time.time * bounceSpeed) * bounceHeight;
            transform.position = new Vector3(
                transform.position.x,
                targetPoint.position.y + bounce,
                transform.position.z
            );
        }
        else
        {
            // Arrived → stop bouncing
            transform.position = targetPoint.position;

            // Assign order when reaching cashier
            if (QueueManager.Instance != null &&
                targetPoint == QueueManager.Instance.queuePoints[0]) // first point = cashier
            {
                CustomerOrder order = GetComponent<CustomerOrder>();
                if (order != null && string.IsNullOrEmpty(order.currentOrder))
                {
                    order.AssignOrder(); // spawn the text above head
                }
            }
        }
    }
}
