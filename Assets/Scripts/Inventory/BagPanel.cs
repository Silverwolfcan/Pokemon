// UI/BagPanel.cs
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
    [SerializeField] private GameObject rowPrefab; // con InventoryItemRowUI

    [Header("Tabs (opcional)")]
    [SerializeField] private InventoryTabsUI tabs;

    [Header("Descripción (opcional)")]
    [SerializeField] private TMP_Text txtDescription;

    [Header("Party (lado izquierdo)")]
    [SerializeField] private StorageGridUI partyGrid;

    [Header("Menú contextual")]
    [SerializeField] private BagContextMenuUI contextMenu;
    [SerializeField] private TMP_Text feedback;

    [Header("Opciones")]
    [SerializeField] private bool autoSelectFirstPokemonOnOpen = true;
    [SerializeField] private bool hideLockedOrZeroQty = true;

    // Estado
    private readonly List<RowRef> rows = new();
    private RowRef selectedRow;
    private ItemData lastSelectedItem;
    private PokemonInstance selectedPokemon;
    private ItemCategory currentCategory = ItemCategory.Healing;

    private GraphicRaycaster raycaster;
    private EventSystem es;
    private Coroutine feedbackRoutine;

    private InventoryManager IM => InventoryManager.Instance;
    private PokemonStorageManager SM => PokemonStorageManager.Instance;

    private void Awake()
    {
        es = EventSystem.current ?? FindAnyObjectByType<EventSystem>();
        raycaster = GetComponentInParent<GraphicRaycaster>() ?? FindAnyObjectByType<GraphicRaycaster>(FindObjectsInactive.Exclude);
        if (!scrollView && content) scrollView = content.GetComponentInParent<ScrollRect>(true);
    }

    private void OnEnable()
    {
        if (!rowPrefab) Debug.LogError("[Bag] Falta rowPrefab.");
        if (!content) Debug.LogError("[Bag] Falta Content del ScrollView.");
        if (!partyGrid) Debug.LogError("[Bag] Falta partyGrid.");

        partyGrid.SetMode(StorageGridUI.GridMode.Party);
        partyGrid.onPokemonClicked.RemoveAllListeners();
        partyGrid.onPokemonClicked.AddListener(OnPartyClick);
        partyGrid.Refresh();

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
        if (SM) SM.OnPartyChanged += () => partyGrid?.Refresh();

        RebuildList();
        if (autoSelectFirstPokemonOnOpen) AutoSelectFirstPokemon();
        SetFeedback("");
    }

    private void OnDisable()
    {
        partyGrid.onPokemonClicked.RemoveAllListeners();
        if (tabs) tabs.onTabChanged -= OnTabChanged;
        if (IM) IM.OnInventoryChanged -= RebuildList;
        if (SM) SM.OnPartyChanged -= () => partyGrid?.Refresh();

        if (contextMenu)
        {
            contextMenu.OnUseClicked -= OnUseFromMenu;
            contextMenu.OnGiveClicked -= OnGiveFromMenu;
            contextMenu.OnExitClicked -= () => contextMenu.Hide();
            contextMenu.OnRemoveClicked -= OnRemoveHeldFromMenu;
            contextMenu.Hide();
        }

        if (feedbackRoutine != null) { StopCoroutine(feedbackRoutine); feedbackRoutine = null; }
        SetFeedback("");

        StorageSlotUI.ClearGlobalSelectionVisuals();
        selectedPokemon = null;
        selectedRow = null;
        lastSelectedItem = null;
    }

    private void Update()
    {
        // Click derecho: abrir menú contextual
        if (Input.GetMouseButtonDown(1))
        {
            var hitRow = RaycastFirst<InventoryItemRowUI>();
            if (hitRow != null)
            {
                var rr = rows.FirstOrDefault(x => x.ui == hitRow);
                if (rr != null)
                {
                    SetSelectedRow(rr);
                    contextMenu?.Show(rr.rect, BagContextMenuUI.Mode.ItemActions);
                    return;
                }
            }

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

        if (contextMenu && contextMenu.gameObject.activeSelf && Input.GetMouseButtonDown(0))
            if (!PointerOverAllowedUI()) contextMenu.Hide();
    }

    // ---------- Party ----------
    private void OnPartyClick(PokemonInstance p) { selectedPokemon = p; }

    private void AutoSelectFirstPokemon()
    {
        if (selectedPokemon != null) return;
        var party = SM ? SM.PlayerParty : null;
        if (party == null) return;
        for (int i = 0; i < party.MaxCapacity; i++)
        {
            var p = party.GetAt(i);
            if (p != null) { selectedPokemon = p; break; }
        }
    }

    // ---------- Tabs / Lista ----------
    private void OnTabChanged(int tabIndex)
    {
        currentCategory = TabIndexToCategory(tabIndex);
        RebuildList();
    }

    private void RebuildList()
    {
        if (!content || IM == null) return;

        foreach (var r in rows) if (r != null && r.go) Destroy(r.go);
        rows.Clear();

        var src = IM.inventory ?? new List<ItemEntry>();
        var filtered = src.Where(e => e != null && e.item != null && e.item.category == currentCategory);
        if (hideLockedOrZeroQty) filtered = filtered.Where(e => e.unlocked && e.quantity > 0);

        var list = filtered.ToList();
        list.Sort(InventorySortUtility.CompareEntries); // grupo asc, calidad asc, nombre asc

        foreach (var e in list)
        {
            var go = Instantiate(rowPrefab, content);
            var row = go.GetComponent<InventoryItemRowUI>();
            if (!row) { Debug.LogError("[Bag] rowPrefab sin InventoryItemRowUI.", go); continue; }

            var rr = new RowRef(go, row, e);
            rows.Add(rr);
            row.Bind(e, _ => SetSelectedRow(rr)); // LMB: seleccionar
        }

        if (scrollView) scrollView.verticalNormalizedPosition = 1f;

        if (lastSelectedItem != null)
        {
            var found = rows.FirstOrDefault(r => r.entry != null && r.entry.item == lastSelectedItem);
            if (found != null) SetSelectedRow(found);
            else ClearSelectionUI();
        }
        else
        {
            ClearSelectionUI();
        }
    }

    private void SetSelectedRow(RowRef rr)
    {
        for (int i = 0; i < rows.Count; i++)
            if (rows[i].ui) rows[i].ui.SetSelected(rows[i] == rr);

        selectedRow = rr;
        lastSelectedItem = rr?.entry?.item;

        if (txtDescription)
        {
            var item = rr?.entry?.item;
            txtDescription.text = item != null ? item.description : "";
        }
    }

    private void ClearSelectionUI()
    {
        for (int i = 0; i < rows.Count; i++)
            if (rows[i].ui) rows[i].ui.SetSelected(false);

        selectedRow = null;
        if (txtDescription) txtDescription.text = "";
    }

    // ---------- Acciones ----------
    private void OnUseFromMenu()
    {
        var entry = selectedRow?.entry;
        if (entry == null || entry.item == null) { SetFeedback("Selecciona un objeto."); contextMenu?.Hide(); return; }
        var target = selectedPokemon;
        if (target == null) { SetFeedback("Selecciona un Pokémon del equipo."); contextMenu?.Hide(); return; }

        if (entry.item is HealingItemData heal)
        {
            var res = ItemEffectsUtility.ApplyHealingItem(target, heal, -1);
            if (res == ItemUseResult.Applied)
            {
                InventoryManager.Instance?.UseItem(heal);
                SetFeedback($"Usaste {entry.item.itemName} en {target.DisplayName}.", 5f);
                partyGrid?.Refresh();
                RebuildList();
            }
            else SetFeedback("No surte efecto.");
        }
        else
        {
            SetFeedback("Este objeto no puede usarse desde la mochila.");
        }

        contextMenu?.Hide();
    }

    private void OnGiveFromMenu()
    {
        var entry = selectedRow?.entry;
        if (entry == null || entry.item == null) { SetFeedback("Selecciona un objeto."); contextMenu?.Hide(); return; }
        var target = selectedPokemon;
        if (target == null) { SetFeedback("Selecciona un Pokémon del equipo."); contextMenu?.Hide(); return; }

        if (InventoryManager.Instance && InventoryManager.Instance.GetQuantity(entry.item) <= 0)
        {
            SetFeedback("No te queda ninguna unidad.");
            contextMenu?.Hide();
            return;
        }

        var previo = target.EquipItem(entry.item);
        if (previo != null) InventoryManager.Instance.AddItem(previo, 1, true);
        InventoryManager.Instance.UseItem(entry.item);

        SetFeedback($"{target.DisplayName} ahora lleva {entry.item.itemName}.", 5f);
        partyGrid?.Refresh();
        RebuildList();
        contextMenu?.Hide();
    }

    private void OnRemoveHeldFromMenu()
    {
        var target = selectedPokemon;
        if (target == null || !target.HasHeldItem) { SetFeedback("Ese Pokémon no lleva objeto."); contextMenu?.Hide(); return; }

        var removed = target.TakeHeldItem();
        if (removed != null) InventoryManager.Instance?.AddItem(removed, 1, true);

        SetFeedback($"{target.DisplayName} soltó {removed?.itemName ?? "objeto"}.", 5f);
        partyGrid?.Refresh();
        RebuildList();
        contextMenu?.Hide();
    }

    // ---------- Util ----------
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

    private bool PointerOverAllowedUI()
    {
        if (!raycaster || !es) return false;
        var ped = new PointerEventData(es) { position = Input.mousePosition };
        var results = new List<RaycastResult>();
        raycaster.Raycast(ped, results);
        foreach (var r in results)
        {
            if (!r.gameObject) continue;
            if (r.gameObject.GetComponentInParent<BagContextMenuUI>() != null) return true;
            if (r.gameObject.GetComponentInParent<InventoryItemRowUI>() != null) return true;
            if (r.gameObject.GetComponentInParent<StorageSlotUI>() != null) return true;
            if (r.gameObject.transform.IsChildOf(content)) return true;
        }
        return false;
    }

    private void SetFeedback(string s, float autoHideSeconds = 0f)
    {
        if (feedback) feedback.text = s ?? "";
        if (!string.IsNullOrEmpty(s)) Debug.Log($"[Bag] {s}");

        if (feedbackRoutine != null) { StopCoroutine(feedbackRoutine); feedbackRoutine = null; }
        if (autoHideSeconds > 0f) feedbackRoutine = StartCoroutine(ClearFeedbackAfter(autoHideSeconds));
    }

    private System.Collections.IEnumerator ClearFeedbackAfter(float seconds)
    {
        yield return new WaitForSecondsRealtime(seconds);
        if (feedback) feedback.text = "";
        feedbackRoutine = null;
    }

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
        {
            this.go = go; this.ui = ui; this.entry = e;
            this.rect = go ? go.GetComponent<RectTransform>() : null;
        }
    }
}
