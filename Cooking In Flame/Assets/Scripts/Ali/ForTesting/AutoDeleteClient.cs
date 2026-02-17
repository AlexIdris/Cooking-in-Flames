using UnityEngine;

public class AutoDeleteClient : MonoBehaviour
{
    public float lifeTime = 5f;

    void Start()
    {
        Invoke(nameof(RemoveSelf), lifeTime);
    }

    void RemoveSelf()
    {
        if (QueueManager.Instance != null)
        {
            QueueManager.Instance.RemoveClient(gameObject);
        }

        Destroy(gameObject);
    }
}
