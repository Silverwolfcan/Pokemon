using UnityEngine;
using UnityEngine.UI;

public class InventoryTabsUI : MonoBehaviour
{
    [Header("8 iconos en orden:")]
    // 0 Botiquín(Healing), 1 Pokéballs, 2 Combate(Battle), 3 Bayas, 4 Varios, 5 MTs, 6 Tesoros, 7 Clave
    [SerializeField] private RectTransform[] tabIcons = new RectTransform[8];
    [SerializeField] private Button[] tabButtons = new Button[8];

    [Header("Escala")]
    [SerializeField] private float normalScale = 1f;
    [SerializeField] private float selectedScale = 1.15f;

    private int currentIndex = 0;
    public System.Action<int> onTabChanged;

    private void Awake()
    {
        for (int i = 0; i < tabButtons.Length; i++)
        {
            int idx = i;
            if (tabButtons[i] != null)
                tabButtons[i].onClick.AddListener(() => SetTab(idx));
        }
        ApplyScales();
    }

    public void SetTab(int index)
    {
        index = Mathf.Clamp(index, 0, 7);
        if (currentIndex == index) return;
        currentIndex = index;
        ApplyScales();
        onTabChanged?.Invoke(currentIndex);
    }

    public int GetCurrentIndex() => currentIndex;

    private void ApplyScales()
    {
        for (int i = 0; i < tabIcons.Length; i++)
        {
            if (!tabIcons[i]) continue;
            float s = (i == currentIndex) ? selectedScale : normalScale;
            tabIcons[i].localScale = new Vector3(s, s, 1f);
        }
    }
}
