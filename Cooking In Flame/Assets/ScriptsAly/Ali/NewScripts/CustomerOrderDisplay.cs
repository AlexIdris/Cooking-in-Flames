using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CustomerOrderDisplay : MonoBehaviour
{
    public TextMeshPro orderText; // assign in prefab
    public Image orderIcon;           // assign in prefab (optional, for future)

    private CustomerSpawner2 spawner;
    private CustomerMover2 mover;

    // Link this customer to its spawner and mover
    public void Init(CustomerSpawner2 s, CustomerMover2 m)
    {
        spawner = s;
        mover = m;
    }

    // Call this whenever the order changes
    public void UpdateOrderDisplay()
    {
        if (mover == null) return;

        // Set the text
        if (orderText != null)
            orderText.text = mover.orderedFood.ToString();

        // If you later want icons, you can do:
        
        if (orderIcon != null && spawner != null)
        {
            Sprite icon = spawner.GetFoodIcon(mover.orderedFood);
            orderIcon.sprite = icon;
            orderIcon.enabled = icon != null;
        }
        
    }

    // Optional: clear display
    public void ClearDisplay()
    {
        if (orderText != null)
            orderText.text = "";

        if (orderIcon != null)
            orderIcon.enabled = false;
    }
}