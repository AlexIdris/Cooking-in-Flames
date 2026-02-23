using System.Collections.Generic;
using UnityEngine;

public class FoodPlate : MonoBehaviour
{
    [Header("Stack Points (assign as many as needed)")]
    public Transform[] stackPoints;

    private Dictionary<IngredientType, GameObject> currentIngredients = new Dictionary<IngredientType, GameObject>();

    public void AddIngredient(GameObject ingredientObj)
    {
        Ingredient ing = ingredientObj.GetComponent<Ingredient>();
        if (ing == null) return;

        // store or replace ingredient
        currentIngredients[ing.type] = ingredientObj;

        UpdateVisualStack();
    }

    void UpdateVisualStack()
    {
        int index = 0;

        // sort ingredients by stackLayer
        List<Ingredient> sorted = new List<Ingredient>();
        foreach (var pair in currentIngredients)
        {
            Ingredient ing = pair.Value.GetComponent<Ingredient>();
            sorted.Add(ing);
        }

        sorted.Sort((a, b) => a.stackLayer.CompareTo(b.stackLayer));

        // position each ingredient
        foreach (var ing in sorted)
        {
            if (index >= stackPoints.Length) break;

            GameObject obj = ing.gameObject;

            // Snap position only, keep rotation
            obj.transform.position = stackPoints[index].position;
            // do NOT change obj.transform.rotation

            index++;
        }
    }
}