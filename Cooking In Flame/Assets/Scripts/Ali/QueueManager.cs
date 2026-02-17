using UnityEngine;
using System.Collections.Generic;

public class QueueManager : MonoBehaviour
{
    public static QueueManager Instance; // singleton reference

    public List<Transform> queuePoints = new List<Transform>();

    private List<GameObject> clientsInQueue = new List<GameObject>();

    void Awake()
    {
        // Set singleton instance
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);

        // Initialize queue slots
        for (int i = 0; i < queuePoints.Count; i++)
            clientsInQueue.Add(null);
    }

    // Assign a free queue spot to a client
    public Transform GetFreePoint(GameObject client)
    {
        for (int i = 0; i < clientsInQueue.Count; i++)
        {
            if (clientsInQueue[i] == null)
            {
                clientsInQueue[i] = client;
                return queuePoints[i];
            }
        }

        return null; // queue full
    }

    // Remove client from queue (when leaving)
    public void RemoveClient(GameObject client)
    {
        int index = clientsInQueue.IndexOf(client);
        if (index == -1) return;

        clientsInQueue[index] = null;

        ShiftQueueForward();
    }

    // Shift everyone forward when spots open
    void ShiftQueueForward()
    {
        for (int i = 0; i < clientsInQueue.Count - 1; i++)
        {
            if (clientsInQueue[i] == null && clientsInQueue[i + 1] != null)
            {
                GameObject client = clientsInQueue[i + 1];
                clientsInQueue[i] = client;
                clientsInQueue[i + 1] = null;

                // Move client to new queue point
                ClientMovement move = client.GetComponent<ClientMovement>();
                move.targetPoint = queuePoints[i];
            }
        }
    }
}
