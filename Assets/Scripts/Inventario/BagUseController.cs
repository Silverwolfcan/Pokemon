using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

public class BagUseController : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private InventoryUIController inventoryUI;
    [SerializeField] private BagContextMenuUI contextMenu;
    [Tooltip("Asigna PanelPokemonTeamController (o variante). Se usa por reflexión si hace falta.")]
    [SerializeField] private MonoBehaviour teamPanel;
    [SerializeField] private TMP_Text feedback; // opcional

    private PokemonInstance cachedSelectedPokemon;

    GraphicRaycaster _raycaster;
    EventSystem _eventSystem;

    void Awake()
    {
        _raycaster = GetComponentInParent<GraphicRaycaster>() ?? FindAnyObjectByType<GraphicRaycaster>();
        _eventSystem = EventSystem.current ?? FindAnyObjectByType<EventSystem>();
    }

    void OnEnable()
    {
        if (inventoryUI) inventoryUI.OnRowClicked += OnRowClicked;
        if (contextMenu)
        {
            contextMenu.OnUseClicked += OnUse;
            contextMenu.OnGiveClicked += OnGive;
            contextMenu.OnExitClicked += OnExit;
        }
    }

    void OnDisable()
    {
        if (inventoryUI) inventoryUI.OnRowClicked -= OnRowClicked;
        if (contextMenu)
        {
            contextMenu.OnUseClicked -= OnUse;
            contextMenu.OnGiveClicked -= OnGive;
            contextMenu.OnExitClicked -= OnExit;
        }
    }

    void Update()
    {
        if (contextMenu && contextMenu.gameObject.activeSelf)
        {
            if (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1))
            {
                if (!PointerIsOverAllowedUI())
                    contextMenu.Hide();
            }
        }
    }

    // ---- Evento desde StorageGridUI (party) ----
    public void OnPartyPokemonClicked(PokemonInstance p) => cachedSelectedPokemon = p;

    // ---- Fila clicada en inventario ----
    void OnRowClicked(ItemEntry entry, RectTransform anchor)
    {
        if (entry == null) return;
        contextMenu?.Show(anchor);
    }

    // ---- Botón Usar ----
    void OnUse()
    {
        var entry = inventoryUI ? inventoryUI.GetSelectedEntry() : null;
        if (entry == null || entry.item == null) { Msg("Selecciona un objeto."); return; }

        var target = GetSelectedPokemon();
        if (target == null) { Msg("Selecciona un Pokémon del equipo."); contextMenu?.Hide(); return; }

        if (entry.item is HealingItemData heal)
        {
            var result = ItemEffectsUtility.ApplyHealingItem(target, heal, -1);
            if (result == ItemUseResult.Applied)
            {
                InventoryManager.Instance?.UseItem(heal);
                Msg($"Usaste {entry.item.itemName} en {target.DisplayName}.");
            }
            else
            {
                Msg("No surte efecto.");
            }
        }
        else
        {
            Msg("Este objeto no puede usarse desde la mochila.");
        }

        contextMenu?.Hide();
    }

    // ---- Botón Dar ----
    void OnGive()
    {
        var entry = inventoryUI ? inventoryUI.GetSelectedEntry() : null;
        if (entry == null || entry.item == null) { Msg("Selecciona un objeto."); return; }

        var target = GetSelectedPokemon();
        if (target == null) { Msg("Selecciona un Pokémon del equipo."); return; }

        var prev = target.EquipItem(entry.item);
        InventoryManager.Instance?.UseItem(entry.item);
        if (prev != null) InventoryManager.Instance?.AddItem(prev, 1, true);

        Msg($"Diste {entry.item.itemName} a {target.DisplayName}.");
        contextMenu?.Hide();
    }

    // ---- Botón Salir ----
    void OnExit() => contextMenu?.Hide();

    // ---- Utilidades ----
    private PokemonInstance GetSelectedPokemon()
    {
        if (cachedSelectedPokemon != null) return cachedSelectedPokemon;

        if (teamPanel != null)
        {
            var t = teamPanel.GetType();
            var m = t.GetMethod("GetSelectedPokemon",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
            if (m != null)
            {
                var obj = m.Invoke(teamPanel, null) as PokemonInstance;
                if (obj != null) return obj;
            }
        }
        return null;
    }

    private bool PointerIsOverAllowedUI()
    {
        if (_raycaster == null || _eventSystem == null) return false;

        var ped = new PointerEventData(_eventSystem) { position = Input.mousePosition };
        var results = new List<RaycastResult>();
        _raycaster.Raycast(ped, results);

        foreach (var r in results)
        {
            if (r.gameObject == contextMenu.gameObject) return true;
            if (r.gameObject.GetComponentInParent<InventoryUIController>() != null) return true;
            if (r.gameObject.GetComponentInParent<PanelPokemonTeamController>() != null) return true;
        }
        return false;
    }

    private void Msg(string s)
    {
        if (feedback != null) feedback.text = s;
        Debug.Log($"[BagUse] {s}");
    }
}
