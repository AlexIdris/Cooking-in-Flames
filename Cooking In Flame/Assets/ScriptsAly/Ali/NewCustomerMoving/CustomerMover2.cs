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

    private Vector3 baseScale;
    public float BaseScale => baseScale.x;

    public Sprite normalFace;
    public Sprite happyFace;
    public Sprite sadFace;

    private SpriteRenderer sr;

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
    }

    public void Init(Transform newTarget, float startMultiplier, float targetMultiplier)
    {
        targetPoint = newTarget;

        // store original prefab scale ONCE
        if (baseScale == Vector3.zero)
            baseScale = transform.localScale;

        float startScale = baseScale.x * startMultiplier;
        float endScale = baseScale.x * targetMultiplier;

        transform.localScale = Vector3.one * startScale;
        targetScale = endScale;

        movingToPoint = true;
        leaving = false;
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

    public void SetFace(int mood)
    {
        if (sr == null) return;

        switch (mood)
        {
            case 1:
                sr.sprite = happyFace;
                break;

            case 2:
                sr.sprite = sadFace;
                break;

            case 3:
                sr.sprite = normalFace;
                break;
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