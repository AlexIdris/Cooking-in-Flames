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

    [Tooltip("Minimum seconds between consecutive customer spawns (inclusive).")]
    public float minSpawnInterval = 1f;

    [Tooltip("Maximum seconds between consecutive customer spawns (inclusive).")]
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

    private static readonly FoodType[] allFoodTypes =
        (FoodType[])System.Enum.GetValues(typeof(FoodType));

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    void Start()
    {
        spawnCoroutine = StartCoroutine(SpawnLoop());
    }

    void Update()
    {
        // Debug / test keys
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
    /// Immediately stops the spawn loop so no new customers arrive.
    /// Called by DayNightCycle5Min when the end-of-day dismissal begins.
    /// </summary>
    public void StopSpawning()
    {
        if (!spawningEnabled) return;
        spawningEnabled = false;
        if (spawnCoroutine != null)
        {
            StopCoroutine(spawnCoroutine);
            spawnCoroutine = null;
        }
        Debug.Log("[CustomerSpawner2] Spawning stopped — end of day.");
    }

    /// <summary>
    /// Restarts the spawn loop from scratch, including the initial delay.
    /// Called by DayNightCycle5Min when the cycle resets back to 8 AM.
    /// </summary>
    public void ResumeSpawning()
    {
        if (spawningEnabled) return;
        spawningEnabled = true;
        nextIndex       = 0;
        customers.Clear();
        spawnCoroutine  = StartCoroutine(SpawnLoop());
        Debug.Log("[CustomerSpawner2] Spawning resumed — new day starting.");
    }

    // ── Spawn loop ────────────────────────────────────────────────────────────

    private IEnumerator SpawnLoop()
    {
        yield return new WaitForSeconds(initialSpawnDelay);

        while (spawningEnabled)
        {
            SpawnCustomer();

            float interval = Random.Range(minSpawnInterval, maxSpawnInterval);
            yield return new WaitForSeconds(interval);
        }
    }

    // ── Spawning ──────────────────────────────────────────────────────────────

    public void SpawnCustomer()
    {
        if (!spawningEnabled)           return;
        if (nextIndex >= orderPoints.Length) return;
        if (characters.Count == 0)          return;

        CharacterData chosen = GetRandomCharacter();

        GameObject     newCustomer = Instantiate(customerPrefab, spawnPoint.position, Quaternion.identity);
        CustomerMover2 mover       = newCustomer.GetComponent<CustomerMover2>();

        mover.spawner          = this;
        mover.currentCharacter = chosen;
        mover.SetFace(3);

        float targetMultiplier = GetScaleForIndex(nextIndex);
        mover.Init(orderPoints[nextIndex], 1f, targetMultiplier);

        customers.Add(mover);
        nextIndex++;

        FoodType randomFood = GetRandomOrder();
        mover.SetOrder(randomFood);

        CustomerOrderDisplay display = newCustomer.GetComponent<CustomerOrderDisplay>();
        if (display != null)
        {
            display.Init(this, mover);
            display.UpdateOrderDisplay();
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
            float targetMultiplier  = GetScaleForIndex(i);
            customers[i].Init(orderPoints[i], currentMultiplier, targetMultiplier);
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
        foreach (FoodType f in allFoodTypes)
            if (!recentOrders.Contains(f)) pool.Add(f);
        if (pool.Count == 0) pool = new List<FoodType>(allFoodTypes);

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