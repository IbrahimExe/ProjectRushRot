using UnityEngine;

public class Collectible : MonoBehaviour
{
    [Header("Collectible Settings")]
    [Tooltip("The name/type of this item (e.g., Gold, Health, Key)")]
    public string collectibleType = "DefaultItem";

    private void OnTriggerEnter(Collider other)
    {
        // Check if the colliding object has the tag "Player"
        if (other.CompareTag("Player"))
        {
            Collect();
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        // Fallback for non-trigger colliders
        if (collision.gameObject.CompareTag("Player"))
        {
            Collect();
        }
    }

    private void Collect()
    {
        // Add the item to the inventory via the manager
        if (InventoryManager.Instance != null)
        {
            InventoryManager.Instance.AddItem(collectibleType);
        }
        else
        {
            Debug.LogError($"[Inventory] Missing InventoryManager in scene! Cannot collect {collectibleType}.");
        }

        // Destroy the collectible object
        Destroy(gameObject);
    }
}
