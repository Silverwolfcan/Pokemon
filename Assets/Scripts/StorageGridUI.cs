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

        // PC box actual
        return PokemonStorageManager.Instance.PcStorage.ActiveBox;
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
            var go = Instantiate(slotPrefab, content);
            var slot = go.GetComponent<StorageSlotUI>();
            if (!slot) slot = go.AddComponent<StorageSlotUI>();
            slots.Add(slot);
        }

        lastStorageRef = storage;
    }

    public void Refresh()
    {
        if (!content || !slotPrefab) return;

        var storage = GetStorage();

        // --- NUEVO: compactar la party antes de pintar (sin huecos intermedios)
        if (mode == GridMode.Party && storage is PokemonParty party)
        {
            // Compact() ya reordena el array moviendo nulls al final.
            party.Compact();
        }

        EnsureBuilt(storage);
        if (storage == null) return;

        for (int i = 0; i < slots.Count; i++)
            slots[i].SetContext(storage, i);
    }

    // llamado por StorageSlotUI
    public void OnSlotClicked(StorageSlotUI slotUI, PokemonInstance p)
    {
        onPokemonClicked?.Invoke(p);
    }

    // utilidad
    private static void ClearChildrenNow(Transform t)
    {
        if (!t) return;
        for (int i = t.childCount - 1; i >= 0; i--)
        {
            var c = t.GetChild(i);
            if (Application.isPlaying) Destroy(c.gameObject);
            else DestroyImmediate(c.gameObject);
        }
    }
}
