using UnityEngine;
using UnityEngine.UI;

public class InventoryTabsUI : MonoBehaviour
{
    [Header("8 iconos en orden:")]
    // 0 Botiquín, 1 Pokéballs, 2 Combate, 3 Bayas, 4 Varios, 5 MT, 6 Tesoros, 7 Clave
    [SerializeField] private RectTransform[] tabIcons = new RectTransform[8];
    [SerializeField] private Button[] tabButtons = new Button[8];

    [Header("Escala")]
    [SerializeField] private float normalScale = 1f;
    [SerializeField] private float selectedScale = 1.15f;

    [Header("Reglas")]
    [Tooltip("Oculta/inhabilita la pestaña de Pokéballs cuando hay combate activo.")]
    public bool hidePokeballsInCombat = true;
    [SerializeField] private int pokeballsIndex = 1;

    private int currentIndex = 0;

    // Evento público esperado por código existente
    public System.Action<int> onTabChanged;

    // ---- Compatibilidad con código antiguo ----
    public int CurrentTab => currentIndex;
    public void SetTab(int index) => SetIndex(index, true);
    public void SetTab(int index, bool notify) => SetIndex(index, notify);
    public void SelectTab(int index) => SetIndex(index, true);
    public int GetTab() => GetCurrentIndex();
    // -------------------------------------------

    void Awake()
    {
        for (int i = 0; i < tabButtons.Length; i++)
        {
            int idx = i;
            if (tabButtons[i] != null)
                tabButtons[i].onClick.AddListener(() => SetIndex(idx, true));
        }
        ApplyScales();
    }

    void OnEnable()
    {
        GameEventBus.EncounterStateChanged += OnEncounterStateChanged;
        OnEncounterStateChanged(ServiceLocator.Get<CombatStateService>()?.IsActive ?? false);
    }

    void OnDisable()
    {
        GameEventBus.EncounterStateChanged -= OnEncounterStateChanged;
    }

    void OnEncounterStateChanged(bool active)
    {
        if (!hidePokeballsInCombat) return;

        if (pokeballsIndex >= 0 && pokeballsIndex < tabButtons.Length && tabButtons[pokeballsIndex] != null)
            tabButtons[pokeballsIndex].interactable = !active;

        if (pokeballsIndex >= 0 && pokeballsIndex < tabIcons.Length && tabIcons[pokeballsIndex] != null)
            tabIcons[pokeballsIndex].gameObject.SetActive(!active);

        if (active && currentIndex == pokeballsIndex)
            SetIndex(0, true);
    }

    public void SetIndex(int index, bool notify = false)
    {
        index = Mathf.Clamp(index, 0, tabIcons.Length - 1);

        bool combat = ServiceLocator.Get<CombatStateService>()?.IsActive ?? false;
        if (hidePokeballsInCombat && combat && index == pokeballsIndex)
            index = 0;

        if (currentIndex == index)
        {
            if (notify) onTabChanged?.Invoke(currentIndex);
            return;
        }

        currentIndex = index;
        ApplyScales();
        if (notify) onTabChanged?.Invoke(currentIndex);
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
