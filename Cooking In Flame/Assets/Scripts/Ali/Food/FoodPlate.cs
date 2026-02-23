using System.Collections.Generic;
using UnityEngine;

public class FoodPlate : MonoBehaviour
{
    [Header("Stack Points (assign in order from bottom to top)")]
    public Transform[] stackPoints;

    public Dictionary<IngredientType, GameObject> currentIngredients = new Dictionary<IngredientType, GameObject>();

    public void AddIngredient(GameObject ingredientObj)
    {
        Ingredient ing = ingredientObj.GetComponent<Ingredient>();
        if (ing == null) return;

        currentIngredients[ing.type] = ingredientObj;
        UpdateVisualStack();
    }

    void UpdateVisualStack()
    {
        int index = 0;
        List<Ingredient> sorted = new List<Ingredient>();
        foreach (var pair in currentIngredients)
        {
            Ingredient ing = pair.Value.GetComponent<Ingredient>();
            sorted.Add(ing);
        }

        sorted.Sort((a, b) => a.stackLayer.CompareTo(b.stackLayer));

        foreach (var ing in sorted)
        {
            if (index >= stackPoints.Length) break;
            GameObject obj = ing.gameObject;
            obj.transform.position = stackPoints[index].position;
            // keep rotation as-is
            index++;
        }
    }

    public void ClearPlate()
    {
        currentIngredients.Clear();
    }
}