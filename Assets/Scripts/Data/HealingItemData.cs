using UnityEngine;

[CreateAssetMenu(fileName = "New HealingItem", menuName = "Pokémon/Items/Healing Item")]
public class HealingItemData : ItemData
{
    public int restoreHP = 20;
    public bool revive = false;
}
