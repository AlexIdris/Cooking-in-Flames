using UnityEngine;

public class CustomerMover : MonoBehaviour
{
    public Transform targetPoint;

    public float moveSpeed = 2f;

    public float startScale = 2f;
    public float targetScale = 4f;

    private Vector3 startPos;
    private float journeyLength;
    private float startTime;

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
    }

    void Update()
    {
        if (targetPoint == null) return;

        float distCovered = (Time.time - startTime) * moveSpeed;
        float fraction = distCovered / journeyLength;

        transform.position = Vector3.Lerp(startPos, targetPoint.position, fraction);

        float scale = Mathf.Lerp(startScale, targetScale, fraction);
        transform.localScale = Vector3.one * scale;
    }
}
