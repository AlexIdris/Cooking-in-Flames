using UnityEngine;
using System.Collections;

public class CustomerMover2 : MonoBehaviour
{
    public float moveSpeed = 3f;
    public float scaleSpeed = 2f;

    private Transform targetPoint;
    private float targetScale;
    private bool movingToPoint = false;
    private bool leaving = false;

    public void Init(Transform newTarget, float startScale, float newTargetScale)
    {
        targetPoint = newTarget;
        targetScale = newTargetScale;

        transform.localScale = Vector3.one * startScale;

        movingToPoint = true;
        leaving = false;
    }

    void Update()
    {
        if (movingToPoint && targetPoint != null)
        {
            // Move towards order point
            transform.position = Vector3.MoveTowards(
                transform.position,
                targetPoint.position,
                moveSpeed * Time.deltaTime
            );

            // Scale up gradually
            float currentScale = transform.localScale.x;
            float newScale = Mathf.MoveTowards(currentScale, targetScale, scaleSpeed * Time.deltaTime);
            transform.localScale = Vector3.one * newScale;

            // Stop when reached
            if (Vector3.Distance(transform.position, targetPoint.position) < 0.05f)
            {
                movingToPoint = false;
            }
        }

        if (leaving)
        {
            // move left
            transform.Translate(Vector3.left * moveSpeed * Time.deltaTime);
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
}