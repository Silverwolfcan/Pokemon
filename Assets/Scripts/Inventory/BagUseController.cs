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
    [Tooltip("Asigna aquí tu panel de equipo (PanelPokemonTeamController / PokemonTeamPanelController).")]
    [SerializeField] private MonoBehaviour teamPanel;
    [SerializeField] private TMP_Text feedback; // opcional

    // Cache del Pokémon seleccionado por la UI de party (se actualiza vía evento)
    private PokemonInstance cachedSelectedPokemon;

    GraphicRaycaster _raycaster;
    EventSystem _eventSystem;

    void Awake()
    {
        _raycaster = GetComponentInParent<GraphicRaycaster>();
        if (_raycaster == null) _raycaster = FindObjectOfType<GraphicRaycaster>();

        _eventSystem = EventSystem.current;
        if (_eventSystem == null) _eventSystem = FindObjectOfType<EventSystem>();
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
        // Cerrar menú si se hace clic fuera de un item/menú/pokémon
        if (contextMenu && contextMenu.gameObject.activeSelf)
        {
            if (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1))
            {
                if (!PointerIsOverAllowedUI())
                    contextMenu.Hide();
            }
        }
    }

    // ---- API para enlazar desde StorageGridUI (evento de selección/click) ----
    public void OnPartyPokemonClicked(PokemonInstance p)
    {
        cachedSelectedPokemon = p;
        // Debug.Log($"[BagUse] Seleccionado {p?.DisplayName ?? "(null)"}");
    }

    // ---- Menú contextual ----
    void OnRowClicked(ItemEntry entry, RectTransform anchor)
    {
        if (entry == null) return;
        if (contextMenu) contextMenu.Show(anchor);
    }

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

    void OnGive()
    {
        var entry = inventoryUI ? inventoryUI.GetSelectedEntry() : null;
        if (entry == null || entry.item == null) { Msg("Selecciona un objeto."); return; }

        var target = GetSelectedPokemon();
        if (target == null) { Msg("Selecciona un Pokémon del equipo."); contextMenu?.Hide(); return; }

        if (InventoryManager.Instance && InventoryManager.Instance.GetQuantity(entry.item) <= 0)
        {
            Msg("No te queda ninguna unidad.");
            contextMenu?.Hide();
            return;
        }

        var previo = target.EquipItem(entry.item);
        if (previo != null) InventoryManager.Instance.AddItem(previo, 1, true);
        InventoryManager.Instance.UseItem(entry.item);

        Msg($"{target.DisplayName} ahora lleva {entry.item.itemName}.");
        contextMenu?.Hide();
    }

    void OnExit() => contextMenu?.Hide();

    // --------------------- Resolución del Pokémon seleccionado ---------------------
    PokemonInstance GetSelectedPokemon()
    {
        // Prioridad 1: lo que nos envía la UI (evento)
        if (cachedSelectedPokemon != null)
            return cachedSelectedPokemon;

        // Prioridad 2: intentar leer del panel por reflexión (por compatibilidad)
        if (teamPanel)
        {
            var t = teamPanel.GetType();

            var pSel = t.GetProperty("SelectedPokemon",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (pSel != null && pSel.PropertyType == typeof(PokemonInstance))
            {
                var val = pSel.GetValue(teamPanel, null) as PokemonInstance;
                if (val != null) return val;
            }

            var mGet = t.GetMethod("GetSelectedPokemon",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (mGet != null && mGet.ReturnType == typeof(PokemonInstance) && mGet.GetParameters().Length == 0)
            {
                var val = mGet.Invoke(teamPanel, null) as PokemonInstance;
                if (val != null) return val;
            }
        }

        // Último recurso: primero de la party
        return FirstPartyPokemonOrNull();
    }

    // Fallback compatible con tu PokemonParty (sin Count ni indexador).
    PokemonInstance FirstPartyPokemonOrNull()
    {
        var party = PokemonStorageManager.Instance ? PokemonStorageManager.Instance.PlayerParty : null;
        if (party == null) return null;

        // Preferimos ToList si existe
        try
        {
            var list = party.ToList();
            if (list != null)
            {
                for (int i = 0; i < list.Count; i++)
                    if (list[i] != null) return list[i];
                return null;
            }
        }
        catch { /* si no existe, caemos a GetAt */ }

        int max = party.MaxCapacity;
        for (int i = 0; i < max; i++)
        {
            var p = party.GetAt(i);
            if (p != null) return p;
        }
        return null;
    }

    // --------------------- Utilidades UI ---------------------
    bool PointerIsOverAllowedUI()
    {
        if (_raycaster == null || _eventSystem == null) return false;

        var ped = new PointerEventData(_eventSystem) { position = Input.mousePosition };
        var results = new List<RaycastResult>();
        _raycaster.Raycast(ped, results);

        foreach (var r in results)
        {
            if (!r.gameObject) continue;

            if (r.gameObject.GetComponentInParent<BagContextMenuUI>() != null)
                return true;

            if (r.gameObject.GetComponentInParent<InventoryItemRowUI>() != null)
                return true;

            // Permitir clic sobre slots de pokémon sin cerrar el menú
            if (r.gameObject.GetComponentInParent<StorageSlotUI>() != null)
                return true;
        }
        return false;
    }

    void Msg(string text)
    {
        if (feedback) feedback.text = text;
        Debug.Log($"[BagUse] {text}");
    }
}
