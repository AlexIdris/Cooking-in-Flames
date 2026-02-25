using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class OrderValidator : MonoBehaviour
{
    [Header("Plate & CustomerSpawner")]
    public FoodPlate plate;
    public CustomerSpawner spawner;

    [Header("Check Interval")]
    public float checkInterval = 0.5f;
    private float nextCheckTime = 0f;

    private Dictionary<string, List<IngredientType>> orderToIngredients = new Dictionary<string, List<IngredientType>>()
    {
        { "Cheese Burger", new List<IngredientType> { IngredientType.BottomBun, IngredientType.Patty, IngredientType.TopBun } },
        { "Fries Only", new List<IngredientType> { IngredientType.Patty } }, // placeholder
        { "Chicken Burger", new List<IngredientType> { IngredientType.BottomBun, IngredientType.Patty, IngredientType.TopBun } },
        { "Fish Nuggets", new List<IngredientType> { IngredientType.Patty } }, // placeholder
        { "Double Burger", new List<IngredientType> { IngredientType.BottomBun, IngredientType.Patty, IngredientType.Patty, IngredientType.TopBun } }
    };

    void Update()
    {
        if (spawner == null || plate == null) return;

        CustomerMover frontCustomer = null;
        if (spawner.customers.Count > 0)
            frontCustomer = spawner.customers[0];

        if (frontCustomer == null) return;
        if (frontCustomer.targetScale != 4f) return;

        if (Time.time >= nextCheckTime)
        {
            nextCheckTime = Time.time + checkInterval;
            ValidateOrder(frontCustomer);
        }
    }

    void ValidateOrder(CustomerMover customer)
    {
        if (customer.orderText == null) return;

        string order = customer.orderText.text;
        if (!orderToIngredients.ContainsKey(order)) return;

        List<IngredientType> expected = orderToIngredients[order];
        List<IngredientType> plateIngredients = GetPlateIngredientTypes();

        if (IsPlateCorrect(expected, plateIngredients))
        {
            ServeCustomer(customer, true); // happy
            plate.ClearPlate();
        }
        else
        {
            ServeCustomer(customer, false); // sad
            plate.ClearPlate();
        }
    }

    List<IngredientType> GetPlateIngredientTypes()
    {
        List<IngredientType> plateList = new List<IngredientType>();
        foreach (var pair in plate.currentIngredients)
        {
            if (pair.Value != null)
            {
                Ingredient ing = pair.Value.GetComponent<Ingredient>();
                if (ing != null)
                    plateList.Add(ing.type);
            }
        }
        return plateList;
    }

    bool IsPlateCorrect(List<IngredientType> expected, List<IngredientType> actual)
    {
        List<IngredientType> tempActual = new List<IngredientType>(actual);
        foreach (var ing in expected)
        {
            if (tempActual.Contains(ing))
            {
                tempActual.Remove(ing);
            }
            else
            {
                return false;
            }
        }
        return true;
    }

    void ServeCustomer(CustomerMover customer, bool happy)
    {
        if (customer.orderText != null)
            customer.orderText.text = happy ? "Happy!" : "Sad!";

        customer.targetPoint = null;
        StartCoroutine(MoveCustomerLeftThenDestroy(customer));
    }

    IEnumerator MoveCustomerLeftThenDestroy(CustomerMover customer)
    {
        float timer = 0f;
        float duration = 3f;
        Vector3 startPos = customer.transform.position;
        Vector3 endPos = startPos + Vector3.left * 2f;

        while (timer < duration)
        {
            timer += Time.deltaTime;
            float t = timer / duration;
            customer.transform.position = Vector3.Lerp(startPos, endPos, t);

            if (customer.orderText != null)
                customer.orderText.transform.position = customer.transform.position + Vector3.up * 2f;

            yield return null;
        }

        spawner.customers.Remove(customer);
        Destroy(customer.gameObject);
    }
}