using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

public class BagPanel : MonoBehaviour
{
    [Header("Lista de objetos")]
    [SerializeField] private ScrollRect scrollView;
    [SerializeField] private RectTransform content;
    [SerializeField] private GameObject rowPrefab;

    [Header("Tabs (opcional)")]
    [SerializeField] private InventoryTabsUI tabs;

    [Header("Descripción (opcional)")]
    [SerializeField] private TMP_Text txtDescription;

    [Header("Party izquierda")]
    [SerializeField] private StorageGridUI partyGrid;

    [Header("Context")]
    [SerializeField] private BagContextMenuUI contextMenu;
    [SerializeField] private TMP_Text feedback;

    [Header("Opciones")]
    [SerializeField] private bool hideLockedOrZeroQty = true;

    [Header("Modo combate")]
    [SerializeField] private bool excludeBallsInCombat = true;
    [SerializeField] private bool requireUsableInBattle = true;
    [SerializeField] private bool openContextMenuOnLeftClickInCombat = true;

    private readonly List<RowRef> rows = new();
    private RowRef selectedRow;
    private ItemData lastSelectedItem;
    private PokemonInstance selectedPokemon;
    private ItemCategory currentCategory = ItemCategory.Healing;

    private bool combatMode = false;
    private System.Action<ItemData, PokemonInstance> combatUseCallback;
    private System.Action combatCloseCallback;

    private GraphicRaycaster raycaster;
    private EventSystem es;

    private InventoryManager IM => InventoryManager.Instance;
    private PokemonStorageManager SM => PokemonStorageManager.Instance;

    public void OpenForCombat(System.Action<ItemData, PokemonInstance> onUse, System.Action onClose)
    {
        combatMode = true;
        combatUseCallback = onUse;
        combatCloseCallback = onClose;
        gameObject.SetActive(true);
        AutoSelectFirstPokemon();
        SetFeedback("Elige un objeto y un Pokémon.");
    }

    public void Close()
    {
        combatMode = false;
        combatUseCallback = null;
        combatCloseCallback = null;
        gameObject.SetActive(false);
    }

    private void Awake()
    {
        es = EventSystem.current ?? FindAnyObjectByType<EventSystem>();
        raycaster = GetComponentInParent<GraphicRaycaster>() ?? FindAnyObjectByType<GraphicRaycaster>(FindObjectsInactive.Exclude);
        if (!scrollView && content) scrollView = content.GetComponentInParent<ScrollRect>(true);
    }

    private void OnEnable()
    {
        if (partyGrid)
        {
            partyGrid.SetMode(StorageGridUI.GridMode.Party);
            partyGrid.onPokemonClicked.RemoveAllListeners();
            partyGrid.onPokemonClicked.AddListener(p => selectedPokemon = p);
            partyGrid.Refresh();
        }

        if (tabs)
        {
            tabs.onTabChanged -= OnTabChanged;
            tabs.onTabChanged += OnTabChanged;
            currentCategory = TabIndexToCategory(tabs.GetCurrentIndex());
        }

        if (contextMenu)
        {
            contextMenu.OnUseClicked += OnUseFromMenu;
            contextMenu.OnGiveClicked += OnGiveFromMenu;
            contextMenu.OnExitClicked += () => contextMenu.Hide();
            contextMenu.OnRemoveClicked += OnRemoveHeldFromMenu;
            contextMenu.Hide();
        }

        if (IM) IM.OnInventoryChanged += RebuildList;

        RebuildList();
        AutoSelectFirstPokemon();
        SetFeedback("");
    }

    private void OnDisable()
    {
        if (tabs) tabs.onTabChanged -= OnTabChanged;
        if (IM) IM.OnInventoryChanged -= RebuildList;

        if (contextMenu)
        {
            contextMenu.OnUseClicked -= OnUseFromMenu;
            contextMenu.OnGiveClicked -= OnGiveFromMenu;
            contextMenu.OnExitClicked -= () => contextMenu.Hide();
            contextMenu.OnRemoveClicked -= OnRemoveHeldFromMenu;
            contextMenu.Hide();
        }

        selectedRow = null;
        selectedPokemon = null;
        lastSelectedItem = null;
        SetFeedback("");
    }

    private void Update()
    {
        if (Input.GetMouseButtonDown(1))
        {
            var hitRow = RaycastFirst<InventoryItemRowUI>();
            if (hitRow != null)
            {
                var rr = rows.FirstOrDefault(x => x.ui == hitRow);
                if (rr != null)
                {
                    SetSelectedRow(rr);
                    contextMenu?.Show(rr.rect, combatMode ? BagContextMenuUI.Mode.ItemActionsOnlyUse : BagContextMenuUI.Mode.ItemActions);
                    return;
                }
            }

            if (!combatMode)
            {
                var slot = RaycastFirst<StorageSlotUI>();
                if (slot != null && slot.Storage != null && slot.Storage.IsIndexValid(slot.Index))
                {
                    var p = slot.Storage.GetAt(slot.Index);
                    if (p != null && p.HasHeldItem)
                    {
                        selectedPokemon = p;
                        var anchor = slot.GetComponent<RectTransform>();
                        contextMenu?.Show(anchor, BagContextMenuUI.Mode.RemoveHeldItem);
                    }
                }
            }
        }
    }

    private void OnTabChanged(int tabIndex)
    {
        currentCategory = TabIndexToCategory(tabIndex);
        RebuildList();
    }

    private void RebuildList()
    {
        if (!content || !rowPrefab) return;

        foreach (var r in rows) if (r != null && r.go) Destroy(r.go);
        rows.Clear();

        var src = IM?.inventory ?? new List<ItemEntry>();
        IEnumerable<ItemEntry> filtered = src.Where(e => e != null && e.item != null && e.item.category == currentCategory);
        if (hideLockedOrZeroQty) filtered = filtered.Where(e => e.unlocked && e.quantity > 0);

        if (combatMode)
        {
            if (excludeBallsInCombat) filtered = filtered.Where(e => e.item.category != ItemCategory.Pokeball);
            if (requireUsableInBattle)
                filtered = filtered.Where(e => e.item is HealingItemData hi && hi.usableInBattle);
        }

        var list = filtered.ToList();
        list.Sort(InventorySortUtility.CompareEntries);

        foreach (var e in list)
        {
            var go = Instantiate(rowPrefab, content);
            var row = go.GetComponent<InventoryItemRowUI>();
            var rr = new RowRef(go, row, e);
            rows.Add(rr);
            row.Bind(e, _ => OnRowLeftClick(rr));
        }

        if (scrollView) scrollView.verticalNormalizedPosition = 1f;

        if (lastSelectedItem != null)
        {
            var found = rows.FirstOrDefault(r => r.entry != null && r.entry.item == lastSelectedItem);
            if (found != null) SetSelectedRow(found);
            else ClearSelectionUI();
        }
        else ClearSelectionUI();
    }

    private void OnRowLeftClick(RowRef rr)
    {
        SetSelectedRow(rr);
        if (combatMode && openContextMenuOnLeftClickInCombat && contextMenu != null)
            contextMenu.Show(rr.rect, BagContextMenuUI.Mode.ItemActionsOnlyUse);
    }

    private void SetSelectedRow(RowRef rr)
    {
        foreach (var r in rows) r.ui?.SetSelected(r == rr);
        selectedRow = rr;
        lastSelectedItem = rr?.entry?.item;
        if (txtDescription) txtDescription.text = rr?.entry?.item ? rr.entry.item.description : "";
    }

    private void ClearSelectionUI()
    {
        foreach (var r in rows) r.ui?.SetSelected(false);
        selectedRow = null;
        if (txtDescription) txtDescription.text = "";
    }

    private void OnUseFromMenu()
    {
        var entry = selectedRow?.entry;
        if (entry == null || entry.item == null) { SetFeedback("Selecciona un objeto."); contextMenu?.Hide(); return; }
        if (selectedPokemon == null) { SetFeedback("Selecciona un Pokémon del equipo."); contextMenu?.Hide(); return; }

        if (combatMode)
        {
            if (excludeBallsInCombat && entry.item.category == ItemCategory.Pokeball)
            { SetFeedback("Las Balls se usan desde Captura."); contextMenu?.Hide(); return; }

            if (requireUsableInBattle && entry.item is HealingItemData hi && !hi.usableInBattle)
            { SetFeedback("Este objeto no puede usarse en combate."); contextMenu?.Hide(); return; }

            if (!(entry.item is HealingItemData))
            { SetFeedback("Sólo curativos durante el combate."); contextMenu?.Hide(); return; }

            combatUseCallback?.Invoke(entry.item, selectedPokemon);
            contextMenu?.Hide();
            combatCloseCallback?.Invoke();
            return;
        }

        // fuera de combate
        if (entry.item is HealingItemData heal)
        {
            var res = ItemEffectsUtility.ApplyHealingItem(selectedPokemon, heal, -1); // ← nombre correcto
            if (res == ItemUseResult.Applied)
            {
                InventoryManager.Instance?.UseItem(heal);
                SetFeedback($"Usaste {entry.item.itemName} en {selectedPokemon.DisplayName}.");
                partyGrid?.Refresh();
                RebuildList();
            }
            else SetFeedback("No surte efecto.");
        }
        else SetFeedback("Este objeto no puede usarse aquí.");

        contextMenu?.Hide();
    }

    private void OnGiveFromMenu()
    {
        if (combatMode) { contextMenu?.Hide(); return; }
        var entry = selectedRow?.entry;
        if (entry == null || entry.item == null) { SetFeedback("Selecciona un objeto."); contextMenu?.Hide(); return; }
        if (selectedPokemon == null) { SetFeedback("Selecciona un Pokémon del equipo."); contextMenu?.Hide(); return; }

        if (InventoryManager.Instance && InventoryManager.Instance.GetQuantity(entry.item) <= 0)
        { SetFeedback("No te queda ninguna unidad."); contextMenu?.Hide(); return; }

        var previo = selectedPokemon.EquipItem(entry.item);
        if (previo != null) InventoryManager.Instance.AddItem(previo, 1, true);
        InventoryManager.Instance.UseItem(entry.item);

        SetFeedback($"{selectedPokemon.DisplayName} ahora lleva {entry.item.itemName}.");
        partyGrid?.Refresh();
        RebuildList();
        contextMenu?.Hide();
    }

    private void OnRemoveHeldFromMenu()
    {
        if (combatMode) { contextMenu?.Hide(); return; }
        var target = selectedPokemon;
        if (target == null || !target.HasHeldItem) { SetFeedback("Ese Pokémon no lleva objeto."); contextMenu?.Hide(); return; }

        var removed = target.TakeHeldItem();
        if (removed != null) InventoryManager.Instance?.AddItem(removed, 1, true);

        SetFeedback($"{target.DisplayName} soltó {removed?.itemName ?? "objeto"}.");
        partyGrid?.Refresh();
        RebuildList();
        contextMenu?.Hide();
    }

    private void AutoSelectFirstPokemon()
    {
        var party = SM ? SM.PlayerParty : null;
        if (party == null) return;
        for (int i = 0; i < party.MaxCapacity; i++)
        {
            var p = party.GetAt(i);
            if (p != null) { selectedPokemon = p; break; }
        }
    }

    private T RaycastFirst<T>() where T : Component
    {
        if (!raycaster || !es) return null;
        var ped = new PointerEventData(es) { position = Input.mousePosition };
        var results = new List<RaycastResult>();
        raycaster.Raycast(ped, results);
        foreach (var r in results)
        {
            var comp = r.gameObject.GetComponentInParent<T>();
            if (comp) return comp;
        }
        return null;
    }

    private void SetFeedback(string s) { if (feedback) feedback.text = s ?? ""; }

    private static ItemCategory TabIndexToCategory(int i) => i switch
    {
        0 => ItemCategory.Healing,
        1 => ItemCategory.Pokeball,
        2 => ItemCategory.Battle,
        3 => ItemCategory.Berries,
        4 => ItemCategory.GeneralGoods,
        5 => ItemCategory.TM,
        6 => ItemCategory.Treasure,
        7 => ItemCategory.KeyItem,
        _ => ItemCategory.Healing
    };

    private sealed class RowRef
    {
        public readonly GameObject go;
        public readonly RectTransform rect;
        public readonly InventoryItemRowUI ui;
        public readonly ItemEntry entry;
        public RowRef(GameObject go, InventoryItemRowUI ui, ItemEntry e)
        { this.go = go; this.ui = ui; this.entry = e; this.rect = go ? go.GetComponent<RectTransform>() : null; }
    }
}
