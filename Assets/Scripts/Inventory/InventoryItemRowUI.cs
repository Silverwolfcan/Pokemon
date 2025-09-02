using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class InventoryItemRowUI : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private Image imgIcon;
    [SerializeField] private TMP_Text txtName;
    [SerializeField] private TMP_Text txtCount;
    [SerializeField] private GameObject selectedHighlight; // hijo dedicado (NO el root)

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
        if (btn)
        {
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(OnClickRow);
            btn.interactable = true;
        }
    }

    public void Bind(ItemEntry entry, System.Action<ItemEntry> onClick)
    {
        bound = entry;
        this.onClick = onClick;

        if (imgIcon)
        {
            imgIcon.sprite = entry?.item?.icon;
            imgIcon.enabled = (imgIcon.sprite != null);
        }

        if (txtName) txtName.text = entry?.item?.itemName ?? "";
        if (txtCount) txtCount.text = entry != null ? $"x{Mathf.Max(0, entry.quantity)}" : "";

        SetSelected(false);
        WireButton(); // por si el prefab se instanció sin botón enlazado
    }

    public void SetSelected(bool selected)
    {
        if (selectedHighlight && selectedHighlight != gameObject)
            selectedHighlight.SetActive(selected);
    }

    public void OnClickRow()
    {
        if (bound != null) onClick?.Invoke(bound);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (selectedHighlight == gameObject)
        {
            selectedHighlight = null;
            Debug.LogWarning($"[InventoryItemRowUI] 'Selected Highlight' no puede ser el objeto raíz. Asigna un hijo dedicado en {name}.", this);
        }
    }
#endif
}
