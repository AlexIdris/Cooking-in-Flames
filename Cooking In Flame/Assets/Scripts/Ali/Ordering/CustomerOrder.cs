using UnityEngine;
using TMPro;

public class CustomerOrder : MonoBehaviour
{
    public string currentOrder;

    [Header("UI")]
    public GameObject orderUIPrefab; // World Space Canvas prefab
    public Transform textAnchor;     // empty object above head

    private GameObject spawnedUI;

    public void AssignOrder()
    {
        currentOrder = OrderManager.Instance.GetRandomOrder();
        ShowOrderUI();
    }

    void ShowOrderUI()
    {
        if (orderUIPrefab == null || textAnchor == null) return;

        spawnedUI = Instantiate(orderUIPrefab, textAnchor.position, Quaternion.identity);
        spawnedUI.transform.SetParent(textAnchor);

        TextMeshProUGUI text = spawnedUI.GetComponentInChildren<TextMeshProUGUI>();
        text.text = currentOrder;
    }
}
