using UnityEngine;
using System.Collections.Generic;

public class InventoryManager : MonoBehaviour
{
    public static InventoryManager Instance;
    public ItemDatabase itemDatabase;



    public List<ItemEntry> inventory = new List<ItemEntry>();

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }
    void Start()
    {
        foreach (var item in itemDatabase.allItems)
        {
            bool unlocked = true;

            if (item is PokeballData ball)
            {
                unlocked = ball.unlockedByDefault;
            }

            AddItem(item, 5, unlocked); // o 0 si lo deseas
        }
    }


    public void AddItem(ItemData item, int amount, bool unlocked = true)
    {
        var entry = inventory.Find(e => e.item == item);
        if (entry != null)
        {
            entry.quantity += amount;
            entry.unlocked = entry.unlocked || unlocked; // mantener desbloqueo si ya lo estaba
        }
        else
        {
            inventory.Add(new ItemEntry(item, amount, unlocked));
        }
    }


    public bool UseItem(ItemData item)
    {
        var entry = inventory.Find(e => e.item.itemName == item.itemName);
        if (entry != null && entry.quantity > 0)
        {
            entry.quantity--;
            return true;
        }
        return false;
    }

    public int GetQuantity(ItemData item)
    {
        var entry = inventory.Find(e => e.item.itemName == item.itemName);
        return entry != null ? entry.quantity : 0;
    }

    

    public List<ItemEntry> GetItemsByCategory(ItemCategory category)
    {
        return inventory.FindAll(e => e.item.category == category);
    }

    public void UnlockItem(ItemData item)
    {
        var entry = inventory.Find(e => e.item == item);
        if (entry != null)
        {
            entry.unlocked = true;
        }
        else
        {
            inventory.Add(new ItemEntry(item, 0, true)); // desbloqueado pero sin cantidad aún
        }
    }

    public void LockItem(ItemData item)
    {
        var entry = inventory.Find(e => e.item == item);
        if (entry != null)
        {
            entry.unlocked = false;
        }
    }


}
