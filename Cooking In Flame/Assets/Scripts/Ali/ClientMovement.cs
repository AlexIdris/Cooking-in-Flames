using UnityEngine;

public class ClientMovement : MonoBehaviour
{
    public Transform targetPoint;
    public float moveSpeed = 3f;

    public float bounceHeight = 0.1f;
    public float bounceSpeed = 8f;

    void Update()
    {
        if (targetPoint == null) return;

        // Move toward target
        transform.position = Vector3.MoveTowards(
            transform.position,
            targetPoint.position,
            moveSpeed * Time.deltaTime
        );

        // Bounce effect
        float distance = Vector3.Distance(transform.position, targetPoint.position);

        if (distance > 0.05f)
        {
            float bounce = Mathf.Sin(Time.time * bounceSpeed) * bounceHeight;
            transform.position = new Vector3(
                transform.position.x,
                targetPoint.position.y + bounce,
                transform.position.z
            );
        }
        else
        {
            transform.position = targetPoint.position;
        }
    }
}
