using UnityEngine;
using System.IO;
using System.Collections.Generic;

//Commands to modify json file
// Resetting the Inventory:    InventoryManager.Instance.ResetInventory();
// Modifying Item Count:       InventoryManager.Instance.SetItemCount("Gold", 10);



public class InventoryManager : MonoBehaviour
{
    public static InventoryManager Instance { get; private set; }

    private string savePath;
    private InventoryData inventoryData = new InventoryData();

    void Awake()
    {
        // Singleton pattern
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            savePath = Path.Combine(Application.persistentDataPath, "inventory.json");
            LoadInventory();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void AddItem(string type)
    {
        // Find if the item type already exists
        InventoryEntry entry = inventoryData.items.Find(i => i.type == type);
        if (entry != null)
        {
            entry.count++;
        }
        else
        {
            // Create a new entry if it doesn't exist
            inventoryData.items.Add(new InventoryEntry { type = type, count = 1 });
        }

        SaveInventory();
        Debug.Log($"<color=green>[Inventory]</color> Collected: <b>{type}</b>. Total count: {GetItemCount(type)}");
    }

    public void SetItemCount(string type, int count)
    {
        InventoryEntry entry = inventoryData.items.Find(i => i.type == type);
        if (entry != null)
        {
            entry.count = count;
        }
        else
        {
            inventoryData.items.Add(new InventoryEntry { type = type, count = count });
        }
        SaveInventory();
        Debug.Log($"<color=orange>[Inventory]</color> Set <b>{type}</b> to: {count}");
    }

    public void ResetInventory()
    {
        inventoryData.items.Clear();
        SaveInventory();
        Debug.Log("<color=red>[Inventory]</color> Inventory has been reset.");
    }

    public int GetItemCount(string type)
    {
        InventoryEntry entry = inventoryData.items.Find(i => i.type == type);
        return entry != null ? entry.count : 0;
    }

    private void SaveInventory()
    {
        string json = JsonUtility.ToJson(inventoryData, true);
        File.WriteAllText(savePath, json);
        Debug.Log($"Inventory saved to: {savePath}");
    }

    private void LoadInventory()
    {
        if (File.Exists(savePath))
        {
            string json = File.ReadAllText(savePath);
            inventoryData = JsonUtility.FromJson<InventoryData>(json);
            Debug.Log("Inventory loaded successfully.");
        }
        else
        {
            inventoryData = new InventoryData();
            Debug.Log("No existing inventory file found. Created a new one.");
        }
    }
}
