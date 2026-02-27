using UnityEngine;
using System.Collections.Generic;

public class CustomerSpawner2 : MonoBehaviour
{
    [System.Serializable]
    public class CharacterData
    {
        public string characterName;  // optional, just for clarity
        public Sprite normalFace;
        public Sprite happyFace;
        public Sprite sadFace;
    }

    public GameObject customerPrefab;
    public Transform spawnPoint;
    public Transform[] orderPoints;

    public List<CharacterData> characters = new List<CharacterData>();

    [HideInInspector] public List<CustomerMover2> customers = new List<CustomerMover2>();
    private int nextIndex = 0;

    void Start()
    {
        InvokeRepeating(nameof(SpawnCustomer), 0f, 5f);
    }

    void Update()
    {
        // Press 4 to serve front customer
        if (customers.Count > 0 && customers[0] != null && Input.GetKeyDown(KeyCode.Alpha4))
        {
            customers[0].LeaveAndDie();
        }

        // Press 1/2/3 to change face of front customer
        if (customers.Count > 0 && customers[0] != null)
        {
            if (Input.GetKeyDown(KeyCode.Alpha1)) customers[0].SetFace(1); // happy
            if (Input.GetKeyDown(KeyCode.Alpha2)) customers[0].SetFace(2); // sad
            if (Input.GetKeyDown(KeyCode.Alpha3)) customers[0].SetFace(3); // normal
        }

        // Shift queue if first customer destroyed
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
        if (characters.Count == 0) return;

        // Pick random character
        CharacterData chosen = characters[Random.Range(0, characters.Count)];

        // Spawn customer
        GameObject newCustomer = Instantiate(customerPrefab, spawnPoint.position, Quaternion.identity);
        CustomerMover2 mover = newCustomer.GetComponent<CustomerMover2>();

        // <-- assign spawner reference here
        mover.spawner = this;

        // Assign chosen character data and default face
        mover.currentCharacter = chosen;
        mover.SetFace(3); // normal by default

        // Set scale
        float startMultiplier = 1f;
        float targetMultiplier = GetScaleForIndex(nextIndex);
        mover.Init(orderPoints[nextIndex], startMultiplier, targetMultiplier);

        // Add to queue
        customers.Add(mover);
        nextIndex++;

        // Pick random food and assign to customer
        FoodType randomFood = (FoodType)Random.Range(0, 3);
        mover.SetOrder(randomFood);
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
            case 0: return 1.4f;
            case 1: return 1.25f;
            case 2: return 1.15f;
            case 3: return 1.05f;
            case 4: return 0.95f;
            default: return 0.85f;
        }
    }
}