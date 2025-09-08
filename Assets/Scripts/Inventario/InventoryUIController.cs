using System;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class InventoryUIController : MonoBehaviour
{
    [Header("Tabs + Lista + Descripción")]
    [SerializeField] private InventoryTabsUI tabs;
    [SerializeField] private Transform listContent;   // Content del Scroll
    [SerializeField] private GameObject rowPrefab;    // Prefab con InventoryItemRowUI
    [SerializeField] private TMP_Text txtDescription; // Cuadro de descripción

    /// <summary>Se emite SOLO cuando el usuario hace click sobre una fila (no al seleccionar por defecto).</summary>
    public event Action<ItemEntry, RectTransform> OnRowClicked;

    private readonly List<InventoryItemRowUI> rows = new List<InventoryItemRowUI>();
    private ItemEntry selected;

    void OnEnable()
    {
        if (tabs) tabs.onTabChanged += OnTabChanged;
        if (InventoryManager.Instance)
            InventoryManager.Instance.OnInventoryChanged += RefreshCurrentTab;

        if (tabs) tabs.SetTab(0);
        else OnTabChanged(0);
    }

    void OnDisable()
    {
        if (tabs) tabs.onTabChanged -= OnTabChanged;
        if (InventoryManager.Instance)
            InventoryManager.Instance.OnInventoryChanged -= RefreshCurrentTab;
    }

    void OnTabChanged(int tabIndex)
    {
        BuildList(TabToCategory(tabIndex));
    }

    void RefreshCurrentTab()
    {
        int idx = tabs ? tabs.GetCurrentIndex() : 0;
        BuildList(TabToCategory(idx));
    }

    static ItemCategory TabToCategory(int idx)
    {
        switch (idx)
        {
            case 0: return ItemCategory.Healing;
            case 1: return ItemCategory.Pokeball;
            case 2: return ItemCategory.Battle;
            case 3: return ItemCategory.Berries;
            case 4: return ItemCategory.GeneralGoods;
            case 5: return ItemCategory.TM;
            case 6: return ItemCategory.Treasure;
            case 7: return ItemCategory.KeyItem;
            default: return ItemCategory.Healing;
        }
    }

    void BuildList(ItemCategory cat)
    {
        // limpiar
        for (int i = listContent.childCount - 1; i >= 0; i--)
            Destroy(listContent.GetChild(i).gameObject);
        rows.Clear();
        selected = null;
        if (txtDescription) txtDescription.text = "";

        if (!rowPrefab || !listContent)
        {
            Debug.LogError("[InventoryUIController] Falta rowPrefab o listContent.");
            return;
        }

        var entries = InventoryManager.Instance
            ? InventoryManager.Instance.GetItemsByCategoryOrdered(cat)
            : new List<ItemEntry>();

        foreach (var e in entries)
        {
            var go = Instantiate(rowPrefab, listContent);
            if (!go.activeSelf) go.SetActive(true);

            var ui = go.GetComponent<InventoryItemRowUI>();
            if (!ui)
            {
                Debug.LogError("[InventoryUIController] El prefab no tiene InventoryItemRowUI.");
                continue;
            }

            // Cuando el usuario clickea la fila -> emitimos evento para menú contextual
            ui.Bind(e, OnUserClickedRow);
            rows.Add(ui);
        }

        // Selección por defecto SIN lanzar evento externo (no abre menú)
        if (rows.Count > 0)
            SelectRowWithoutContext(rows[0]);
    }

    void SelectRowWithoutContext(InventoryItemRowUI row)
    {
        if (row == null) return;
        selected = row.Bound;
        foreach (var r in rows) r.SetSelected(r == row);
        if (txtDescription) txtDescription.text = selected?.item?.description ?? "";
    }

    // Llamado por InventoryItemRowUI al hacer click (izquierdo)
    void OnUserClickedRow(ItemEntry entry)
    {
        // Actualiza selección
        selected = entry;
        foreach (var r in rows) r.SetSelected(r.Bound == selected);
        if (txtDescription) txtDescription.text = selected?.item?.description ?? "";

        // Notifica para que otro sistema (BagUseController) abra el menú contextual
        var row = rows.Find(r => r.Bound == selected);
        if (row) OnRowClicked?.Invoke(selected, row.transform as RectTransform);
    }

    public ItemEntry GetSelectedEntry() => selected;
}
