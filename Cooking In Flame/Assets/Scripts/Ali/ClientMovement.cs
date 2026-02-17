using UnityEngine;

public class ClientMovement : MonoBehaviour
{
    public Transform targetPoint;
    public float moveSpeed = 3f;

    public float bounceHeight = 0.1f;
    public float bounceSpeed = 8f;

    private Vector3 startPos;

    void Start()
    {
        startPos = transform.position;
    }

    void Update()
    {
        if (targetPoint == null) return;

        // Move forward
        transform.position = Vector3.MoveTowards(
            transform.position,
            targetPoint.position,
            moveSpeed * Time.deltaTime
        );

        // Bounce effect (only while moving)
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
            // Snap to exact target when arrived
            transform.position = targetPoint.position;
            OnArrived();
        }
    }

    void OnArrived()
    {
        Debug.Log("Client reached order point!");
        enabled = false;
    }
}
