using UnityEngine;
using System.Collections.Generic;
using System;

public class InventoryManager : MonoBehaviour
{
    public static InventoryManager Instance;
    public ItemDatabase itemDatabase;

    public List<ItemEntry> inventory = new List<ItemEntry>();

    public event Action OnInventoryChanged;

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
        // Ejemplo: poblar con 5 unidades de cada ítem de la DB (si lo deseas)
        if (itemDatabase && itemDatabase.allItems != null)
        {
            foreach (var item in itemDatabase.allItems)
            {
                bool unlocked = true;
                AddItem(item, 5, unlocked);
            }
        }
    }

    public void AddItem(ItemData item, int amount, bool unlocked = true)
    {
        if (item == null || amount == 0) return;

        var entry = inventory.Find(e => e.item == item);
        if (entry != null)
        {
            entry.quantity += amount;
            entry.quantity = Mathf.Max(0, entry.quantity);
            entry.unlocked = entry.unlocked || unlocked;
        }
        else
        {
            inventory.Add(new ItemEntry(item, Mathf.Max(0, amount), unlocked));
        }

        OnInventoryChanged?.Invoke();
    }

    public bool UseItem(ItemData item)
    {
        if (item == null) return false;

        var entry = inventory.Find(e => e.item == item);
        if (entry != null && entry.quantity > 0)
        {
            entry.quantity--;
            OnInventoryChanged?.Invoke();
            return true;
        }
        return false;
    }

    public int GetQuantity(ItemData item)
    {
        var entry = inventory.Find(e => e.item == item);
        return entry != null ? entry.quantity : 0;
    }

    public List<ItemEntry> GetItemsByCategory(ItemCategory category)
    {
        return inventory.FindAll(e => e.item != null && e.item.category == category);
    }

    /// <summary>
    /// Lista ordenada con criterio por categoría:
    /// - Curativos: por tipo (Pociones vs Revivires) y por potencia creciente (Poción < Super < Híper < Máxima < Restaurar todo).
    /// - Resto: alfabético.
    /// </summary>
    public List<ItemEntry> GetItemsByCategoryOrdered(ItemCategory category)
    {
        var list = GetItemsByCategory(category);
        list.Sort(InventorySortUtility.CompareEntries);
        return list;
    }

    public void UnlockItem(ItemData item)
    {
        if (item == null) return;

        var entry = inventory.Find(e => e.item == item);
        if (entry != null) entry.unlocked = true;
        else inventory.Add(new ItemEntry(item, 0, true));

        OnInventoryChanged?.Invoke();
    }

    public void LockItem(ItemData item)
    {
        if (item == null) return;

        var entry = inventory.Find(e => e.item == item);
        if (entry != null) entry.unlocked = false;

        OnInventoryChanged?.Invoke();
    }
}
