using UnityEngine;

public class ClientSpawner : MonoBehaviour
{
    public GameObject clientPrefab;
    public Transform spawnPoint;
    public Transform orderPoint;

    public float spawnInterval = 5f;

    void Start()
    {
        InvokeRepeating(nameof(SpawnClient), 1f, spawnInterval);
    }

    void SpawnClient()
    {
        GameObject client = Instantiate(clientPrefab, spawnPoint.position, Quaternion.identity);

        ClientMovement movement = client.GetComponent<ClientMovement>();
        movement.targetPoint = orderPoint;
    }
}
