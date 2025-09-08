// UI/InventoryItemRowUI.cs
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class InventoryItemRowUI : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private Image imgIcon;
    [SerializeField] private TMP_Text txtName;
    [SerializeField] private TMP_Text txtCount;

    [Header("Background (opcional)")]
    [SerializeField] private Image imgBackground;   // Fondo a cambiar
    [SerializeField] private Sprite bgUnselected;   // Sprite no seleccionado
    [SerializeField] private Sprite bgSelected;     // Sprite seleccionado

    private ItemEntry bound;
    private System.Action<ItemEntry> onClick;

    public ItemEntry Bound => bound;

    private void Awake()
    {
        WireButton();
    }

    private void WireButton()
    {
        var btn = GetComponent<Button>();
        if (btn == null) btn = gameObject.AddComponent<Button>();
        btn.onClick.RemoveAllListeners();
        btn.onClick.AddListener(OnClickRow);
        btn.interactable = true;
    }

    public void Bind(ItemEntry entry, System.Action<ItemEntry> onClick)
    {
        bound = entry;
        this.onClick = onClick;

        if (imgIcon)
        {
            imgIcon.sprite = entry?.item?.icon;
            imgIcon.enabled = (imgIcon.sprite != null);
            imgIcon.preserveAspect = true;
        }

        if (txtName) txtName.text = entry?.item?.itemName ?? "";
        if (txtCount) txtCount.text = entry != null ? $"x{Mathf.Max(0, entry.quantity)}" : "";

        SetSelected(false);
        WireButton();
    }

    public void SetSelected(bool selected)
    {
        if (!imgBackground) return;

        var target = selected ? bgSelected : bgUnselected;
        if (target != null)
        {
            imgBackground.sprite = target;
            imgBackground.enabled = true;
            imgBackground.preserveAspect = false;
        }
        // Si no hay sprite para ese estado, no se toca el fondo.
    }

    public void OnClickRow()
    {
        if (bound != null) onClick?.Invoke(bound);
    }
}
