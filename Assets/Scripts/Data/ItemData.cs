using UnityEngine;

public enum ItemCategory
{
    Pokeball,
    Healing,
    Battle,
    KeyItem
}

public abstract class ItemData : ScriptableObject
{
    public string itemName;
    public Sprite icon;
    public ItemCategory category;
    [TextArea]
    public string description;
}
