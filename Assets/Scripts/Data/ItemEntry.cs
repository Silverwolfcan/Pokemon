[System.Serializable]
public class ItemEntry
{
    public ItemData item;
    public int quantity;
    public bool unlocked;

    public ItemEntry(ItemData item, int quantity, bool unlocked = true)
    {
        this.item = item;
        this.quantity = quantity;
        this.unlocked = unlocked;
    }
}
