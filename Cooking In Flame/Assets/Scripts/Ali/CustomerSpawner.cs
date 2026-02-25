using UnityEngine;
using System.Collections.Generic;

public class CustomerSpawner : MonoBehaviour
{
    public GameObject customerPrefab;

    public Transform spawnPoint;

    private List<CustomerMover> customers = new List<CustomerMover>();

    public Transform[] orderPoints;
    private int nextIndex = 0;


    void Start()
    {
        InvokeRepeating(nameof(SpawnCustomer), 0f, 5f);

    }

    void Update()
    {
        // if first customer is gone, shift line
        if (customers.Count > 0 && customers[0] == null)
        {
            customers.RemoveAt(0);
            nextIndex--;

            MoveQueueForward();
        }
    }

    public void SpawnCustomer()
    {
            if (nextIndex >= orderPoints.Length) return;

        GameObject newCustomer = Instantiate(customerPrefab, spawnPoint.position, Quaternion.identity);
        CustomerMover mover = newCustomer.GetComponent<CustomerMover>();

        // calculate scale based on position in line
        float startScale = 2f;

        float targetScale;
        switch (nextIndex)
        {
            case 0: targetScale = 4f; break;     // front
            case 1: targetScale = 3.5f; break;
            case 2: targetScale = 3.2f; break;
            case 3: targetScale = 2.9f; break;
            case 4: targetScale = 2.6f; break;
            default: targetScale = 2.3f; break;
        }



        mover.Init(orderPoints[nextIndex], startScale, targetScale);

            customers.Add(mover);

            nextIndex++;
     }

    void MoveQueueForward()
    {
        for (int i = 0; i < customers.Count; i++)
        {
            if (customers[i] == null) continue;

            float startScale = customers[i].transform.localScale.x;

            float targetScale;
            switch (i)
            {
                case 0: targetScale = 4f; break;
                case 1: targetScale = 3.5f; break;
                case 2: targetScale = 3.2f; break;
                case 3: targetScale = 2.9f; break;
                case 4: targetScale = 2.6f; break;
                default: targetScale = 2.3f; break;
            }

            customers[i].Init(orderPoints[i], startScale, targetScale);
        }
    }
}
