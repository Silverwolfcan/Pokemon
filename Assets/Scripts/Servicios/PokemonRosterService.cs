using System;
using System.Collections.Generic;
using UnityEngine;

/// Servicio fuente de verdad para la party. Mantiene selección única.
public class PokemonRosterService
{
    private readonly PokemonStorageManager storage;
    private int selectedIndex = -1;

    public event Action OnPartyChanged;
    public event Action<int, PokemonInstance> OnSelectedChanged;

    public PokemonRosterService(PokemonStorageManager storageManager)
    {
        storage = storageManager ?? throw new ArgumentNullException(nameof(storageManager));
        storage.OnPartyChanged += HandlePartyChanged;
        EnsureCompacted();
        EnsureValidSelection();
    }

    private PokemonParty Party => storage.PlayerParty;
    public int MaxSlots => Party?.MaxCapacity ?? 6;
    public PokemonInstance Selected => GetAt(selectedIndex);

    public IReadOnlyList<PokemonInstance> Snapshot()
    {
        if (Party == null) return Array.Empty<PokemonInstance>();
        return Party.ToList();
    }

    public PokemonInstance GetAt(int index)
    {
        if (Party == null) return null;
        return Party.GetAt(index);
    }

    public int GetSelectedIndex() => selectedIndex;

    public int FirstAliveIndex()
    {
        if (Party == null) return -1;
        for (int i = 0; i < Party.MaxCapacity; i++)
        {
            var p = Party.GetAt(i);
            if (p != null && p.currentHP > 0) return i;
        }
        for (int i = 0; i < Party.MaxCapacity; i++)
            if (Party.GetAt(i) != null) return i;
        return -1;
    }

    public int FindIndex(PokemonInstance p)
    {
        if (Party == null || p == null) return -1;
        for (int i = 0; i < Party.MaxCapacity; i++)
            if (Party.GetAt(i) == p) return i;
        return -1;
    }

    public void Select(int index)
    {
        if (Party == null || !Party.IsIndexValid(index)) return;
        var target = Party.GetAt(index);
        if (target == null) return;

        if (selectedIndex == index) return;
        selectedIndex = index;
        OnSelectedChanged?.Invoke(selectedIndex, target);
        Logger.Storage($"Selected party index = {selectedIndex} ({target.DisplayName})");
    }

    public bool SelectFirstAlive()
    {
        EnsureCompacted();
        int idx = FirstAliveIndex();
        if (idx >= 0) { Select(idx); return true; }
        // No hay pokémon en party
        selectedIndex = -1;
        OnSelectedChanged?.Invoke(-1, null);
        return false;
    }

    public bool SelectFirstSlot()
    {
        EnsureCompacted();
        if (Party == null) { selectedIndex = -1; OnSelectedChanged?.Invoke(-1, null); return false; }
        if (Party.GetAt(0) != null) { Select(0); return true; }

        // Party compacta implica que si slot 0 es null, no hay ninguno
        selectedIndex = -1;
        OnSelectedChanged?.Invoke(-1, null);
        return false;
    }

    public void EnsureCompacted()
    {
        if (Party == null) return;
        bool changed = Party.Compact();
        if (changed)
        {
            OnPartyChanged?.Invoke();
            GameEventBus.RaisePartyChanged();
            Logger.Storage("Party compacted (nulls moved to the end).");
        }
    }

    private void HandlePartyChanged()
    {
        EnsureCompacted();
        EnsureValidSelection();
        OnPartyChanged?.Invoke();
        GameEventBus.RaisePartyChanged();
    }

    private void EnsureValidSelection()
    {
        if (Party == null)
        {
            selectedIndex = -1;
            OnSelectedChanged?.Invoke(-1, null);
            return;
        }

        if (Party.IsIndexValid(selectedIndex) && Party.GetAt(selectedIndex) != null)
        {
            OnSelectedChanged?.Invoke(selectedIndex, Party.GetAt(selectedIndex));
            return;
        }

        selectedIndex = FirstAliveIndex();
        OnSelectedChanged?.Invoke(selectedIndex, GetAt(selectedIndex));
    }
}
