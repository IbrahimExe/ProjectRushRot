using System;
using System.Collections.Generic;

[Serializable]
public class InventoryEntry
{
    public string type;
    public int count;
}

[Serializable]
public class InventoryData
{
    public List<InventoryEntry> items = new List<InventoryEntry>();
}
