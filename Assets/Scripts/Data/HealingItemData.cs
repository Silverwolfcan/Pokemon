using UnityEngine;

[CreateAssetMenu(fileName = "New HealingItem", menuName = "Pok�mon/Items/Healing Item")]
public class HealingItemData : ItemData
{
    public int restoreHP = 20;
    public bool revive = false;
}
