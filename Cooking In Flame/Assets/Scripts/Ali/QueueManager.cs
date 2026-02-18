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
        for (int i = 0; i < clientsInQueue.Count; i++)
        {
            if (clientsInQueue[i] == null)
            {
                for (int j = i + 1; j < clientsInQueue.Count; j++)
                {
                    if (clientsInQueue[j] != null)
                    {
                        GameObject client = clientsInQueue[j];
                        clientsInQueue[i] = client;
                        clientsInQueue[j] = null;

                        ClientMovement move = client.GetComponent<ClientMovement>();
                        move.targetPoint = queuePoints[i]; // ← this is the line that makes customer 2 walk to customer 1’s spot

                        break;
                    }
                }
            }
        }
    }

}
