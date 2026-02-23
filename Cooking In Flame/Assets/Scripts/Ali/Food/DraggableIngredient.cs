using UnityEngine;

public class DraggableIngredient : MonoBehaviour
{
    private Camera cam;
    private bool isDragging = false;
    private float zOffset;

    void Start()
    {
        cam = Camera.main;
        zOffset = transform.position.z; // keep Z fixed
    }

    void Update()
    {
        // start dragging
        if (Input.GetMouseButtonDown(0))
        {
            Ray ray = cam.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                if (hit.transform == transform)
                {
                    isDragging = true;
                }
            }
        }

        // dragging
        if (isDragging)
        {
            Vector3 mousePos = Input.mousePosition;
            mousePos.z = cam.WorldToScreenPoint(transform.position).z; // distance to camera
            Vector3 worldPos = cam.ScreenToWorldPoint(mousePos);
            worldPos.z = zOffset; // keep original Z
            transform.position = worldPos;
        }

        // release
        if (isDragging && Input.GetMouseButtonUp(0))
        {
            isDragging = false;

            // optional: snap to plate if touching
            Ray ray = cam.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                FoodPlate plate = hit.collider.GetComponent<FoodPlate>();
                if (plate != null)
                {
                    plate.AddIngredient(gameObject);
                    return;
                }
            }

            // optional: destroy if released elsewhere
            Destroy(gameObject);
        }
    }
}