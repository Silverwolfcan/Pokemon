using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

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

    // Guardamos el último storage para detectar cambios de caja
    private IPokemonStorage lastStorageRef;

    public void SetMode(GridMode newMode) { mode = newMode; }

    private IPokemonStorage GetStorage()
    {
        if (PokemonStorageManager.Instance == null) return null;

        if (mode == GridMode.Party)
            return PokemonStorageManager.Instance.PlayerParty;

        // PC box actual. Si tienes GetActiveBox(), cámbialo aquí sin problema.
        return PokemonStorageManager.Instance.PcStorage.ActiveBox;
        // return PokemonStorageManager.Instance.PcStorage.GetActiveBox();
    }

    private int ExpectedCount(IPokemonStorage st)
    {
        if (st != null) return st.MaxCapacity;
        return mode == GridMode.Party ? 6 : 30;
    }

    /// <summary>
    /// Reconstruye los slots si:
    /// - No hay storage.
    /// - Cambió la referencia de storage (p. ej. siguiente caja).
    /// - Cambió la capacidad esperada.
    /// En otro caso, reusa los existentes.
    /// </summary>
    private void EnsureBuilt(IPokemonStorage storage)
    {
        int expected = ExpectedCount(storage);

        bool needsRebuild =
            storage == null ||
            storage != lastStorageRef ||
            content.childCount != expected ||
            slots.Count != expected;

        if (!needsRebuild) return;

        // Limpiar duro: reparentamos antes de destruir para que el contenedor
        // quede vacío en el mismo frame (evita que se “acumulen” visualmente).
        ClearChildrenNow(content);
        slots.Clear();

        if (storage == null) { lastStorageRef = null; return; }

        // Crear exactamente expected slots
        for (int i = 0; i < expected; i++)
        {
            var go = Instantiate(slotPrefab, content);
            var slot = go.GetComponent<StorageSlotUI>();
            if (!slot) slot = go.AddComponent<StorageSlotUI>();
            slots.Add(slot);
        }

        lastStorageRef = storage;
    }

    public void Refresh()
    {
        if (!content || !slotPrefab)
            return;

        var storage = GetStorage();

        // Asegura estructura correcta según el storage actual
        EnsureBuilt(storage);

        // Si no hay storage (p.e. al cargar escena) salimos
        if (storage == null) return;

        // Re-vincula/repinta todos los slots
        for (int i = 0; i < slots.Count; i++)
        {
            slots[i].SetContext(storage, i);
        }
    }

    // llamado por StorageSlotUI
    public void OnSlotClicked(StorageSlotUI slotUI, PokemonInstance p)
    {
        onPokemonClicked?.Invoke(p);
    }

    // utilidad pública (p. ej. panel cambia de caja)
    public void ForceRebuildAndRefresh()
    {
        lastStorageRef = null; // fuerza rebuild
        Refresh();
    }

    private void OnEnable() { lastStorageRef = null; Refresh(); }
    private void OnDisable() { /* nada */ }

    // -------- helpers --------
    private static void ClearChildrenNow(Transform parent)
    {
        // Reparent + Destroy para que el contenedor quede vacío inmediatamente
        while (parent.childCount > 0)
        {
            var child = parent.GetChild(0);
            child.SetParent(null, false);
            if (Application.isPlaying) Object.Destroy(child.gameObject);
            else Object.DestroyImmediate(child.gameObject);
        }
    }
}
