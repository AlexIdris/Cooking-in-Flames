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

        if (customers.Count > 0 && customers[0] != null)
        {
            if (Input.GetKeyDown(KeyCode.Alpha1))
            {
                customers[0].SetFace(1); // happy
            }
            if (Input.GetKeyDown(KeyCode.Alpha2))
            {
                customers[0].SetFace(2); // sad
            }
            if (Input.GetKeyDown(KeyCode.Alpha3))
            {
                customers[0].SetFace(3); // normal
            }
        }

        // If first customer is destroyed, shift queue
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

        float startMultiplier = 1f;
        float targetMultiplier = GetScaleForIndex(nextIndex);

        mover.Init(orderPoints[nextIndex], startMultiplier, targetMultiplier);

        customers.Add(mover);
        nextIndex++;
    }

    void MoveQueueForward()
    {
        for (int i = 0; i < customers.Count; i++)
        {
            if (customers[i] == null) continue;

            float currentMultiplier = customers[i].transform.localScale.x / customers[i].BaseScale;

            float targetMultiplier = GetScaleForIndex(i);

            customers[i].Init(orderPoints[i], currentMultiplier, targetMultiplier);
        }
    }

    float GetScaleForIndex(int i)
    {
        switch (i)
        {
            case 0: return 1.4f;  // front (biggest)
            case 1: return 1.25f;
            case 2: return 1.15f;
            case 3: return 1.05f;
            case 4: return 0.95f;
            default: return 0.85f;
        }
    }
}