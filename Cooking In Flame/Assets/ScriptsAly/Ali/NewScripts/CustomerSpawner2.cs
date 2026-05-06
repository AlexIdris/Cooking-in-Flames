using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class CustomerSpawner2 : MonoBehaviour
{
    [System.Serializable]
    public class CharacterData
    {
        public string characterName;
        public Sprite normalFace;
        public Sprite happyFace;
        public Sprite sadFace;
    }

    [System.Serializable]
    public class FoodIconData
    {
        public FoodType type;
        public Sprite   icon;
    }

    [Header("Spawn Settings")]
    public GameObject  customerPrefab;
    public Transform   spawnPoint;
    public Transform[] orderPoints;

    [Tooltip("Delay in seconds before the very first customer spawns after the scene loads.")]
    public float initialSpawnDelay = 5f;

    [Tooltip("Minimum seconds between consecutive customer spawns.")]
    public float minSpawnInterval = 1f;

    [Tooltip("Maximum seconds between consecutive customer spawns.")]
    public float maxSpawnInterval = 5f;

    [Header("Characters")]
    public List<CharacterData> characters           = new List<CharacterData>();
    public int                 CustomerTypeRecentLimit = 9;

    [Header("Food Icons")]
    public List<FoodIconData> foodIcons;

    [Header("Order History")]
    public int orderHistoryLimit = 2;

    [HideInInspector] public List<CustomerMover2> customers = new List<CustomerMover2>();

    private int                  nextIndex        = 0;
    private Queue<CharacterData> recentCharacters = new Queue<CharacterData>();
    private Queue<FoodType>      recentOrders     = new Queue<FoodType>();
    private Coroutine            spawnCoroutine;
    private bool                 spawningEnabled  = true;
    private CustomerQuotaManager quotaManager;

    // Active food pool — mirrors the uploaded script's enabled list.
    // Add or remove entries here to control which orders appear.
    private static readonly FoodType[] activeFoodTypes =
    {
        FoodType.Burger,
        FoodType.HealthyBurger,
        FoodType.LeafyCheeseBurger,
        FoodType.MexcianBurger,
        FoodType.OnionBurger,
        FoodType.SussyCheeseBurger,
        FoodType.Coffee,
        FoodType.MegaComboBurger
    };

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    void Start()
    {
        // Spawning does NOT auto-start on scene load.
        // It is started by DayNightCycle5Min.StartDay(), which is triggered by
        // ShopToggle when the player clicks the "Close" button to open the shop.
        spawningEnabled = false;
    }

    void Update()
    {
        // Debug keys
        if (customers.Count > 0 && customers[0] != null)
        {
            if (Input.GetKeyDown(KeyCode.Alpha4)) customers[0].LeaveAndDie();
            if (Input.GetKeyDown(KeyCode.Alpha1)) customers[0].SetFace(1);
            if (Input.GetKeyDown(KeyCode.Alpha2)) customers[0].SetFace(2);
            if (Input.GetKeyDown(KeyCode.Alpha3)) customers[0].SetFace(3);
        }

        // Shift queue when the front customer has been destroyed
        if (customers.Count > 0 && customers[0] == null)
        {
            customers.RemoveAt(0);
            nextIndex = Mathf.Max(0, nextIndex - 1);
            MoveQueueForward();
        }
    }

    // ── Spawn control (called by DayNightCycle5Min) ───────────────────────────

    /// <summary>
    /// Stops the spawn loop immediately.
    /// Called by DayNightCycle5Min at end-of-day to prevent new customers arriving.
    /// </summary>
    public void StopSpawning()
    {
        if (!spawningEnabled) return;
        spawningEnabled = false;
        if (spawnCoroutine != null) { StopCoroutine(spawnCoroutine); spawnCoroutine = null; }
        Debug.Log("[CustomerSpawner2] Spawning stopped.");
    }

    /// <summary>
    /// Restarts the spawn loop from scratch with initial delay.
    /// Called by DayNightCycle5Min when the day cycle resets to 8 AM.
    /// </summary>
    public void ResumeSpawning()
    {
        if (spawningEnabled) return;
        spawningEnabled = true;
        nextIndex       = 0;
        customers.Clear();
        spawnCoroutine  = StartCoroutine(SpawnLoop());
        Debug.Log("[CustomerSpawner2] Spawning resumed.");
    }

    // ── Quota manager link (set by CustomerQuotaManager.Start()) ─────────────

    /// <summary>Called once by CustomerQuotaManager so customers can notify it.</summary>
    public void SetQuotaManager(CustomerQuotaManager manager) => quotaManager = manager;

    /// <summary>Returns the quota manager so CustomerMover2 can call RegisterServed.</summary>
    public CustomerQuotaManager GetQuotaManager() => quotaManager;

    // ── Spawn loop ────────────────────────────────────────────────────────────

    private IEnumerator SpawnLoop()
    {
        yield return new WaitForSeconds(initialSpawnDelay);

        while (spawningEnabled)
        {
            SpawnCustomer();
            yield return new WaitForSeconds(Random.Range(minSpawnInterval, maxSpawnInterval));
        }
    }

    // ── Spawning ──────────────────────────────────────────────────────────────

    public void SpawnCustomer()
    {
        if (!spawningEnabled)                return;
        if (nextIndex >= orderPoints.Length) return;
        if (characters.Count == 0)           return;

        // Stop spawning if the daily customer quota has already been reached
        if (quotaManager != null && quotaManager.IsQuotaFull)
        {
            StopSpawning();
            return;
        }

        CharacterData chosen     = GetRandomCharacter();
        GameObject    newCustomer = Instantiate(customerPrefab, spawnPoint.position, Quaternion.identity);
        CustomerMover2 mover     = newCustomer.GetComponent<CustomerMover2>();

        mover.spawner          = this;
        mover.currentCharacter = chosen;
        mover.SetFace(3);
        mover.Init(orderPoints[nextIndex], 1f, GetScaleForIndex(nextIndex));

        customers.Add(mover);
        nextIndex++;

        FoodType randomFood = GetRandomOrder();
        mover.SetOrder(randomFood);

        CustomerOrderDisplay display = newCustomer.GetComponent<CustomerOrderDisplay>();
        if (display != null)
        {
            display.Init(this, mover);
            display.DisplayOrderTextLetterByLetter(mover);
        }
    }

    // ── Food icon lookup ──────────────────────────────────────────────────────

    public Sprite GetFoodIcon(FoodType type)
    {
        foreach (FoodIconData f in foodIcons)
            if (f.type == type) return f.icon;
        return null;
    }

    // ── Queue movement ────────────────────────────────────────────────────────

    private void MoveQueueForward()
    {
        for (int i = 0; i < customers.Count; i++)
        {
            if (customers[i] == null) continue;
            float currentMultiplier = customers[i].transform.localScale.x / customers[i].BaseScale;
            customers[i].Init(orderPoints[i], currentMultiplier, GetScaleForIndex(i));
        }
    }

    // ── Random helpers ────────────────────────────────────────────────────────

    private CharacterData GetRandomCharacter()
    {
        List<CharacterData> pool = new List<CharacterData>();
        foreach (CharacterData c in characters)
            if (!recentCharacters.Contains(c)) pool.Add(c);
        if (pool.Count == 0) pool = new List<CharacterData>(characters);

        CharacterData chosen = pool[Random.Range(0, pool.Count)];
        recentCharacters.Enqueue(chosen);
        while (recentCharacters.Count > CustomerTypeRecentLimit)
            recentCharacters.Dequeue();
        return chosen;
    }

    private FoodType GetRandomOrder()
    {
        List<FoodType> pool = new List<FoodType>();
        foreach (FoodType f in activeFoodTypes)
            if (!recentOrders.Contains(f)) pool.Add(f);
        if (pool.Count == 0) pool = new List<FoodType>(activeFoodTypes);

        FoodType chosen = pool[Random.Range(0, pool.Count)];
        recentOrders.Enqueue(chosen);
        while (recentOrders.Count > orderHistoryLimit)
            recentOrders.Dequeue();
        return chosen;
    }

    // ── Scale table ───────────────────────────────────────────────────────────

    private float GetScaleForIndex(int i)
    {
        switch (i)
        {
            case 0:  return 1.40f;
            case 1:  return 1.25f;
            case 2:  return 1.15f;
            case 3:  return 1.05f;
            case 4:  return 0.95f;
            default: return 0.85f;
        }
    }
}