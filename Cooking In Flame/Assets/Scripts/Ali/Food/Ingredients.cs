using UnityEngine;

public enum IngredientType
{
    BottomBun,
    Patty,
    Lettuce,
    Tomato,
    TopBun,
    // add more types here later
}

public class Ingredient : MonoBehaviour
{
    [Header("Ingredient Settings")]
    public IngredientType type;
    [Tooltip("Lower = bottom, higher = top")]
    public int stackLayer;
}