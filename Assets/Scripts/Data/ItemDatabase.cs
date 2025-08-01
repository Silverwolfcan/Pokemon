using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "ItemDatabase", menuName = "Pokémon/Items/Item Database")]
public class ItemDatabase : ScriptableObject
{
    public List<ItemData> allItems;

    public List<ItemData> GetItemsByCategory(ItemCategory category)
    {
        return allItems.FindAll(item => item.category == category);
    }

    public ItemData GetItemByName(string itemName)
    {
        return allItems.Find(item => item.itemName == itemName);
    }
}
