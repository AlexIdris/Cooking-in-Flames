using UnityEngine;
using System.Collections.Generic;

public class OrderManager : MonoBehaviour
{
    public static OrderManager Instance;

    public List<string> possibleOrders = new List<string>()
    {
        "Chicken Burger",
        "Burger + Fries",
        "Cheese Burger",
        "Double Burger",
        "Fries Only"
    };

    void Awake()
    {
        Instance = this;
    }

    public string GetRandomOrder()
    {
        int index = Random.Range(0, possibleOrders.Count);
        return possibleOrders[index];
    }
}
