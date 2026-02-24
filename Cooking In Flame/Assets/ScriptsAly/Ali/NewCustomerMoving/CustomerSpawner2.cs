using UnityEngine;
using System.Collections.Generic;

public class CustomerSpawner2 : MonoBehaviour
{
    public GameObject customerPrefab;
    public Transform spawnPoint;

    [HideInInspector] public List<CustomerMover2> customers = new List<CustomerMover2>();
    public Transform[] orderPoints;

    private int nextIndex = 0;

    void Start()
    {
        InvokeRepeating(nameof(SpawnCustomer), 0f, 5f);
    }

    void Update()
    {
        // Press 4 to serve first customer
        if (Input.GetKeyDown(KeyCode.Alpha4))
        {
            if (customers.Count > 0 && customers[0] != null)
            {
                customers[0].LeaveAndDie();
            }
        }

        // Clean nulls + shift queue
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
        CustomerMover2 mover = newCustomer.GetComponent<CustomerMover2>();

        float startScale = 2f;
        float targetScale = GetScaleForIndex(nextIndex);

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
            float targetScale = GetScaleForIndex(i);

            customers[i].Init(orderPoints[i], startScale, targetScale);
        }
    }

    float GetScaleForIndex(int i)
    {
        switch (i)
        {
            case 0: return 4f;
            case 1: return 3.5f;
            case 2: return 3.2f;
            case 3: return 2.9f;
            case 4: return 2.6f;
            default: return 2.3f;
        }
    }
}