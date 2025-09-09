using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

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

    public void SetMode(GridMode newMode) { mode = newMode; }
    private void OnEnable() => Refresh();

    private IPokemonStorage GetStorage()
    {
        if (PokemonStorageManager.Instance == null) return null;
        return mode == GridMode.Party
            ? PokemonStorageManager.Instance.PlayerParty
            : PokemonStorageManager.Instance.PcStorage.ActiveBox;
    }

    private int ExpectedCount(IPokemonStorage st) => st != null ? st.MaxCapacity : (mode == GridMode.Party ? 6 : 30);

    private void EnsureBuilt(IPokemonStorage storage)
    {
        int expected = ExpectedCount(storage);
        bool needsRebuild = storage == null || storage != lastStorageRef || content.childCount != expected || slots.Count != expected;
        if (!needsRebuild) return;

        ClearChildrenNow(content);
        slots.Clear();
        if (storage == null) { lastStorageRef = null; return; }

        for (int i = 0; i < expected; i++)
        {
            var go = Object.Instantiate(slotPrefab, content);
            var slot = go.GetComponent<StorageSlotUI>() ?? go.AddComponent<StorageSlotUI>();
            slots.Add(slot);

            WireSlotClick(i, go); // ← cablear click del botón del slot
        }
        lastStorageRef = storage;
    }

    public void Refresh()
    {
        if (!content || !slotPrefab) return;

        var storage = GetStorage();

        if (mode == GridMode.Party && storage is PokemonParty party)
            party.Compact();

        EnsureBuilt(storage);
        if (storage == null) return;

        for (int i = 0; i < slots.Count; i++)
        {
            slots[i].SetContext(storage, i);
            // Asegurar que el botón sigue cableado tras cambios de prefab/carga
            WireSlotClick(i, slots[i].gameObject);
        }
    }

    // Click hacia fuera
    public void OnSlotClicked(StorageSlotUI slotUI, PokemonInstance p) => onPokemonClicked?.Invoke(p);

    private void WireSlotClick(int index, GameObject slotGO)
    {
        var btn = slotGO.GetComponentInChildren<Button>(true);
        if (btn == null) return;

        btn.onClick.RemoveAllListeners();
        btn.onClick.AddListener(() =>
        {
            var storage = GetStorage();
            PokemonInstance p = null;
            if (storage != null && storage.IsIndexValid(index))
                p = storage.GetAt(index);

            onPokemonClicked?.Invoke(p);
        });
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
