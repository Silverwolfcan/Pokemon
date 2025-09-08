using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

[System.Serializable] public class PokemonEvent : UnityEvent<PokemonInstance> { }

public class StorageGridUI : MonoBehaviour
{
    public enum GridMode { Party, PCBox }

    [Header("Config")]
    public GridMode mode = GridMode.Party;
    [SerializeField] private Transform content;
    [SerializeField] private GameObject slotPrefab;

    [Header("Eventos")]
    public PokemonEvent onPokemonClicked = new PokemonEvent();

    private readonly List<StorageSlotUI> slots = new List<StorageSlotUI>();
    private IPokemonStorage lastStorageRef;
    private PokemonRosterService _rosterSvc;

    // -------- Ciclo de vida --------
    private void OnEnable()
    {
        _rosterSvc = ServiceLocator.Get<PokemonRosterService>();
        if (_rosterSvc != null)
            _rosterSvc.OnSelectedChanged += HandleRosterSelectedChanged;

        if (PokemonStorageManager.Instance != null)
        {
            PokemonStorageManager.Instance.OnPartyChanged += OnStorageChanged;
            PokemonStorageManager.Instance.OnPcBoxChanged += OnStorageChanged;
        }
        GameEventBus.PartyChanged += OnStorageChanged;

        // Limpia cualquier marca antigua y pinta
        StorageSlotUI.ClearGlobalSelectionVisuals();
        Refresh();
        SyncSelectionVisual(); // aplica selección global actual
    }

    private void OnDisable()
    {
        if (_rosterSvc != null)
            _rosterSvc.OnSelectedChanged -= HandleRosterSelectedChanged;
        _rosterSvc = null;

        if (PokemonStorageManager.Instance != null)
        {
            PokemonStorageManager.Instance.OnPartyChanged -= OnStorageChanged;
            PokemonStorageManager.Instance.OnPcBoxChanged -= OnStorageChanged;
        }
        GameEventBus.PartyChanged -= OnStorageChanged;
    }

    // -------- API pública --------
    public void SetMode(GridMode newMode)
    {
        mode = newMode;
        lastStorageRef = null; // forzar rebuild
        Refresh();
        SyncSelectionVisual();
    }

    public void Refresh()
    {
        if (!content || !slotPrefab) return;

        var storage = GetStorage();
        if (mode == GridMode.Party && storage is PokemonParty party)
            party.Compact(); // sin huecos

        EnsureBuilt(storage);
        if (storage == null) return;

        for (int i = 0; i < slots.Count; i++)
        {
            slots[i].SetContext(storage, i);
            // Limpieza local por seguridad al refrescar
            slots[i].ForceSetSelectedVisual(false);
        }
    }

    // Click desde StorageSlotUI
    public void OnSlotClicked(StorageSlotUI slotUI, PokemonInstance p)
    {
        onPokemonClicked?.Invoke(p);

        if (mode == GridMode.Party && _rosterSvc != null)
        {
            int idx = slots.IndexOf(slotUI);
            if (idx >= 0 && p != null) _rosterSvc.Select(idx);
        }
    }

    // Selección programática
    public void SelectIndex(int index)
    {
        if (index < 0 || index >= slots.Count) return;
        var slot = slots[index];
        if (slot == null) return;

        var ev = new PointerEventData(EventSystem.current);
        slot.OnPointerClick(ev);
    }

    // -------- Internos --------
    private void OnStorageChanged()
    {
        Refresh();
        SyncSelectionVisual();
    }

    private void HandleRosterSelectedChanged(int index, PokemonInstance p)
    {
        if (mode != GridMode.Party) return;
        if (index < 0 || p == null) return;

        // Limpieza local antes de seleccionar
        for (int i = 0; i < slots.Count; i++) slots[i].ForceSetSelectedVisual(false);
        SelectIndex(index);
    }

    private IPokemonStorage GetStorage()
    {
        var mgr = PokemonStorageManager.Instance;
        if (mgr == null) return null;
        return mode == GridMode.Party ? mgr.PlayerParty : mgr.PcStorage.ActiveBox;
    }

    private int ExpectedCount(IPokemonStorage st)
    {
        if (st != null) return st.MaxCapacity;
        return mode == GridMode.Party ? 6 : 30;
    }

    private void EnsureBuilt(IPokemonStorage storage)
    {
        int expected = ExpectedCount(storage);
        bool needsRebuild =
            storage == null ||
            storage != lastStorageRef ||
            content.childCount != expected ||
            slots.Count != expected;

        if (!needsRebuild) return;

        ClearChildrenNow(content);
        slots.Clear();

        if (storage == null) { lastStorageRef = null; return; }

        for (int i = 0; i < expected; i++)
        {
            var go = Object.Instantiate(slotPrefab, content);
            var slot = go.GetComponent<StorageSlotUI>();
            if (!slot) slot = go.AddComponent<StorageSlotUI>();
            slots.Add(slot);
        }

        lastStorageRef = storage;
    }

    private void SyncSelectionVisual()
    {
        if (mode != GridMode.Party || _rosterSvc == null) return;
        int idx = _rosterSvc.GetSelectedIndex();
        if (idx >= 0)
        {
            for (int i = 0; i < slots.Count; i++) slots[i].ForceSetSelectedVisual(false);
            SelectIndex(idx);
        }
    }

    private static void ClearChildrenNow(Transform t)
    {
        if (!t) return;
        for (int i = t.childCount - 1; i >= 0; i--)
        {
            var c = t.GetChild(i);
            if (Application.isPlaying) Object.Destroy(c.gameObject);
            else Object.DestroyImmediate(c.gameObject);
        }
    }
}
