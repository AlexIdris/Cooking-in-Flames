using UnityEngine;

public class ClientSpawner : MonoBehaviour
{
    public GameObject clientPrefab;
    public Transform spawnPoint;
    public float spawnInterval = 5f;

    void Start()
    {
        InvokeRepeating(nameof(SpawnClient), 1f, spawnInterval);
    }

    void SpawnClient()
    {
        GameObject client = Instantiate(clientPrefab, spawnPoint.position, Quaternion.identity);

        Transform freeSpot = QueueManager.Instance.GetFreePoint(client);

        if (freeSpot == null)
        {
            Debug.Log("Queue full!");
            Destroy(client);
            return;
        }

        ClientMovement movement = client.GetComponent<ClientMovement>();
        movement.targetPoint = freeSpot;
    }
}
