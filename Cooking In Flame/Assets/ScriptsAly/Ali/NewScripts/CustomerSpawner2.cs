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

    // keep track of last 9 spawned characters
    private Queue<CharacterData> recentCharacters = new Queue<CharacterData>();
    public int CustomerTypeRecentLimit = 9;

    //keep track of last 2 orders
    private Queue<FoodType> recentOrders = new Queue<FoodType>();
    public int orderHistoryLimit = 2;

    public GameObject customerPrefab;
    public Transform spawnPoint;
    public Transform[] orderPoints;

    public List<CharacterData> characters = new List<CharacterData>();

    [System.Serializable]
    public class FoodIconData
    {
        public FoodType type;
        public Sprite icon;
    }

    public List<FoodIconData> foodIcons; // assign in inspector

    public Sprite GetFoodIcon(FoodType type)
    {
        foreach (var f in foodIcons)
        {
            if (f.type == type)
                return f.icon;
        }
        return null;
    }

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
        CharacterData chosen = GetRandomCharacter();

        // Spawn customer
        GameObject newCustomer = Instantiate(customerPrefab, spawnPoint.position, Quaternion.identity);
        CustomerMover2 mover = newCustomer.GetComponent<CustomerMover2>();

        // <-- assign spawner reference here
        mover.spawner = this;

        CustomerOrderDisplay display = newCustomer.GetComponent<CustomerOrderDisplay>();
        if (display != null)
            display.Init(this, mover);

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
        FoodType randomFood = GetRandomOrder();
        mover.SetOrder(randomFood);

        if (display != null)
            display.UpdateOrderDisplay();
    }

    CharacterData GetRandomCharacter()
    {
        List<CharacterData> available = new List<CharacterData>();

        // build a list of characters that are NOT in recent history
        foreach (var c in characters)
        {
            if (!recentCharacters.Contains(c))
                available.Add(c);
        }

        // if all characters are in recent list (edge case), allow all again
        if (available.Count == 0)
            available = new List<CharacterData>(characters);

        // pick random from available
        CharacterData chosen = available[Random.Range(0, available.Count)];

        // add to recent queue
        recentCharacters.Enqueue(chosen);

        // keep only last N (9)
        if (recentCharacters.Count > CustomerTypeRecentLimit)
            recentCharacters.Dequeue();

        return chosen;
    }

    FoodType GetRandomOrder()
    {
        List<FoodType> allFoods = new List<FoodType>()
    {
        FoodType.NormalBurger,
        FoodType.TomatoBurger,
        FoodType.PattyOnly,
        FoodType.BaconBurger
    };

        List<FoodType> available = new List<FoodType>();

        // exclude recent ones
        foreach (var f in allFoods)
        {
            if (!recentOrders.Contains(f))
                available.Add(f);
        }

        // if everything got excluded (edge case), allow all again
        if (available.Count == 0)
            available = new List<FoodType>(allFoods);

        // pick random
        FoodType chosen = available[Random.Range(0, available.Count)];

        // push to history
        recentOrders.Enqueue(chosen);

        if (recentOrders.Count > orderHistoryLimit)
            recentOrders.Dequeue();

        return chosen;
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