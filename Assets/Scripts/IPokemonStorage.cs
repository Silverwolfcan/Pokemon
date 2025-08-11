using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Contrato común para “colecciones de Pokémon”
public interface IPokemonStorage
{
    int MaxCapacity { get; }
    int CountNonNull { get; }
    PokemonInstance GetAt(int index);
    PokemonInstance RemoveAt(int index);
    bool TryAdd(PokemonInstance p); // añade al primer hueco libre
    bool TryInsertAt(int index, PokemonInstance p, out PokemonInstance displaced); // coloca en índice; si hay alguien, lo “desplaza”
    bool IsIndexValid(int index);
}

// ---------------- PARTY (slots fijos con nulls) ----------------
public class PokemonParty : IPokemonStorage
{
    public int MaxCapacity => 6;

    // 6 ranuras fijas; null = vacío
    private readonly PokemonInstance[] slots = new PokemonInstance[6];

    public int CountNonNull => slots.Count(p => p != null);
    public bool IsFull => CountNonNull >= MaxCapacity;
    public bool IsIndexValid(int index) => index >= 0 && index < MaxCapacity;

    public PokemonInstance GetAt(int index)
    {
        if (!IsIndexValid(index)) return null;
        return slots[index];
    }

    public PokemonInstance RemoveAt(int index)
    {
        if (!IsIndexValid(index)) return null;
        var p = slots[index];
        slots[index] = null;
        return p;
    }

    public bool TryAdd(PokemonInstance p)
    {
        for (int i = 0; i < MaxCapacity; i++)
            if (slots[i] == null) { slots[i] = p; return true; }
        return false;
    }

    public bool TryInsertAt(int index, PokemonInstance p, out PokemonInstance displaced)
    {
        displaced = null;
        if (!IsIndexValid(index)) return false;
        displaced = slots[index];
        slots[index] = p;
        return true;
    }

    // Swap interno (no crea huecos)
    public void Swap(int a, int b)
    {
        if (!IsIndexValid(a) || !IsIndexValid(b)) return;
        (slots[a], slots[b]) = (slots[b], slots[a]);
    }

    // Cargar desde lista (admite nulls)
    public void SetFromList(List<PokemonInstance> list)
    {
        for (int i = 0; i < MaxCapacity; i++)
            slots[i] = (list != null && i < list.Count) ? Sanitize(list[i]) : null;
    }

    // Cargar desde máscara de ocupación
    public void SetFromMasked(List<PokemonInstance> list, List<bool> occupied)
    {
        for (int i = 0; i < MaxCapacity; i++)
        {
            bool occ = (occupied != null && i < occupied.Count) ? occupied[i] : (list != null && i < list.Count && list[i] != null);
            slots[i] = occ ? Sanitize(list != null && i < list.Count ? list[i] : null) : null;
        }
    }

    // **NUEVO**: compacta manteniendo el orden relativo; devuelve true si movió algo
    public bool Compact()
    {
        int write = 0;
        bool changed = false;
        for (int read = 0; read < MaxCapacity; read++)
        {
            var p = slots[read];
            if (p != null)
            {
                if (read != write) { slots[write] = p; slots[read] = null; changed = true; }
                write++;
            }
        }
        return changed;
    }

    public List<PokemonInstance> ToList() => new List<PokemonInstance>(slots);

    private static PokemonInstance Sanitize(PokemonInstance p)
    {
        if (p == null || p.species == null) return null;
        return p;
    }
}

// ---------------- PC BOX ----------------
public class PCBox : IPokemonStorage
{
    public int MaxCapacity => 30;
    private readonly PokemonInstance[] slots;

    public PCBox() { slots = new PokemonInstance[MaxCapacity]; }
    public PCBox(IEnumerable<PokemonInstance> data)
    {
        slots = new PokemonInstance[MaxCapacity];
        if (data == null) return;
        int i = 0;
        foreach (var p in data)
        {
            if (i >= MaxCapacity) break;
            slots[i++] = p;
        }
    }

    public int CountNonNull => slots.Count(p => p != null);
    public bool IsIndexValid(int index) => index >= 0 && index < MaxCapacity;

    public PokemonInstance GetAt(int index) => IsIndexValid(index) ? slots[index] : null;

    public PokemonInstance RemoveAt(int index)
    {
        if (!IsIndexValid(index)) return null;
        var p = slots[index];
        slots[index] = null;
        return p;
    }

    public bool TryAdd(PokemonInstance p)
    {
        for (int i = 0; i < MaxCapacity; i++)
            if (slots[i] == null) { slots[i] = p; return true; }
        return false;
    }

    public bool TryInsertAt(int index, PokemonInstance p, out PokemonInstance displaced)
    {
        displaced = null;
        if (!IsIndexValid(index)) return false;
        displaced = slots[index];
        slots[index] = p;
        return true;
    }

    public void Swap(int a, int b)
    {
        if (!IsIndexValid(a) || !IsIndexValid(b)) return;
        (slots[a], slots[b]) = (slots[b], slots[a]);
    }

    public List<PokemonInstance> ToList() => new List<PokemonInstance>(slots);

    public void SetFromMasked(List<PokemonInstance> list, List<bool> occupied)
    {
        for (int i = 0; i < MaxCapacity; i++)
        {
            bool occ = (occupied != null && i < occupied.Count) ? occupied[i] : (list != null && i < list.Count && list[i] != null);
            var p = (list != null && i < list.Count) ? list[i] : null;
            slots[i] = (occ && p != null && p.species != null) ? p : null;
        }
    }
}

// --------------- PC STORAGE ---------------
public class PCStorage
{
    private readonly List<PCBox> boxes = new List<PCBox>();
    public IReadOnlyList<PCBox> Boxes => boxes;

    public int ActiveBoxIndex { get; private set; } = 0;
    public PCBox ActiveBox => boxes[ActiveBoxIndex];

    public int UnlockedBoxCount { get; private set; }

    private static readonly (int threshold, int boxes)[] unlocks = new[]
    {
        (   0,  8),
        ( 230, 16),
        ( 470, 24),
        ( 710, 32),
    };

    public PCStorage()
    {
        UnlockedBoxCount = unlocks[0].boxes;
        for (int i = 0; i < UnlockedBoxCount; i++) boxes.Add(new PCBox());
    }

    public void SetFromSave(List<List<PokemonInstance>> pcBoxes)
    {
        boxes.Clear();
        if (pcBoxes != null) foreach (var bx in pcBoxes) boxes.Add(new PCBox(bx));
        if (boxes.Count == 0)
        {
            UnlockedBoxCount = unlocks[0].boxes;
            for (int i = 0; i < UnlockedBoxCount; i++) boxes.Add(new PCBox());
        }
        else UnlockedBoxCount = boxes.Count;

        ActiveBoxIndex = Mathf.Clamp(ActiveBoxIndex, 0, UnlockedBoxCount - 1);
    }

    public void SetFromMasked(List<(List<PokemonInstance> slots, List<bool> occupied)> dtoBoxes)
    {
        boxes.Clear();
        if (dtoBoxes != null && dtoBoxes.Count > 0)
            foreach (var (slots, occ) in dtoBoxes) { var b = new PCBox(); b.SetFromMasked(slots, occ); boxes.Add(b); }

        if (boxes.Count == 0)
        {
            UnlockedBoxCount = unlocks[0].boxes;
            for (int i = 0; i < UnlockedBoxCount; i++) boxes.Add(new PCBox());
        }
        else UnlockedBoxCount = boxes.Count;

        ActiveBoxIndex = Mathf.Clamp(ActiveBoxIndex, 0, UnlockedBoxCount - 1);
    }

    public void UpdateUnlocks(int totalPokemon)
    {
        int target = unlocks[0].boxes;
        foreach (var (thr, cnt) in unlocks) if (totalPokemon >= thr) target = cnt;

        if (target > UnlockedBoxCount)
        {
            for (int i = UnlockedBoxCount; i < target; i++) boxes.Add(new PCBox());
            UnlockedBoxCount = target;
        }
        ActiveBoxIndex = Mathf.Clamp(ActiveBoxIndex, 0, UnlockedBoxCount - 1);
    }

    public void SetActiveBox(int index) => ActiveBoxIndex = Mathf.Clamp(index, 0, UnlockedBoxCount - 1);

    public bool AddToFirstAvailable(PokemonInstance p)
    {
        for (int i = 0; i < UnlockedBoxCount; i++)
            if (boxes[i].TryAdd(p)) return true;
        return false;
    }

    public List<List<PokemonInstance>> ToSave() => boxes.Select(b => b.ToList()).ToList();

    // >>> NUEVO helper limpio para opción B:
    public IPokemonStorage GetActiveBox() => ActiveBox; // devuelve la caja activa como IPokemonStorage
}
